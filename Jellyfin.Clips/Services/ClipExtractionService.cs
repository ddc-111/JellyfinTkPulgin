using MediaBrowser.Controller.Library;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Jellyfin.Clips.Configuration;
using Jellyfin.Clips.Data;
using Jellyfin.Clips.Data.Entities;
using Jellyfin.Clips.Data.Repositories;

namespace Jellyfin.Clips.Services;

public interface IClipExtractionService
{
    Task<int> ExtractClipsFromItemAsync(string sourceItemId, bool forceRegenerate, CancellationToken ct = default);
    Task<int> ProcessQueueAsync(CancellationToken ct = default);
    Task<int> RecoverInterruptedTasksAsync(CancellationToken ct = default);
    Task CleanupTempFilesAsync(CancellationToken ct = default);
}

public class ClipExtractionService : IClipExtractionService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IFfmpegWrapper _ffmpeg;
    private readonly IHighlightDetectionService _highlightDetection;
    private readonly IMultimodalAnalysisService? _multimodalAnalysis;
    private readonly IClipRepository _clipRepository;
    private readonly IProcessingStateRepository _processingStateRepository;
    private readonly IDbContextFactory<ClipsDbContext> _dbFactory;
    private readonly ILogger<ClipExtractionService> _logger;
    private readonly PluginConfiguration _config;

    public ClipExtractionService(
        ILibraryManager libraryManager,
        IFfmpegWrapper ffmpeg,
        IHighlightDetectionService highlightDetection,
        IClipRepository clipRepository,
        IProcessingStateRepository processingStateRepository,
        IDbContextFactory<ClipsDbContext> dbFactory,
        ILogger<ClipExtractionService> logger,
        IMultimodalAnalysisService? multimodalAnalysis = null)
    {
        _libraryManager = libraryManager;
        _ffmpeg = ffmpeg;
        _highlightDetection = highlightDetection;
        _clipRepository = clipRepository;
        _processingStateRepository = processingStateRepository;
        _dbFactory = dbFactory;
        _logger = logger;
        _config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        _multimodalAnalysis = multimodalAnalysis;
    }

    public async Task<int> ExtractClipsFromItemAsync(string sourceItemId, bool forceRegenerate, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var isDeleted = await db.DeletedSourceItems.AnyAsync(d => d.SourceItemId == sourceItemId, ct).ConfigureAwait(false);
        if (isDeleted)
        {
            _logger.LogInformation("Item {Id} was previously deleted by user, skipping", sourceItemId);
            return 0;
        }

        var item = _libraryManager.GetItemById(new Guid(sourceItemId));
        if (item is null)
        {
            _logger.LogWarning("Item {Id} not found", sourceItemId);
            return 0;
        }

        if (string.IsNullOrEmpty(item.Path) || !File.Exists(item.Path))
        {
            _logger.LogWarning("Item {Id} has no valid file path", sourceItemId);
            return 0;
        }

        if (!forceRegenerate)
        {
            var existing = await _clipRepository.GetBySourceItemIdAsync(sourceItemId, ct).ConfigureAwait(false);
            if (existing.Count >= _config.MaxClipsPerVideo)
            {
                _logger.LogInformation("Item {Id} already has {Count} clips, skipping", sourceItemId, existing.Count);
                return 0;
            }
        }

        var processingState = await _processingStateRepository.GetBySourceItemIdAsync(sourceItemId, ct).ConfigureAwait(false);
        if (processingState != null && processingState.Status == ProcessingStatus.InProgress)
        {
            _logger.LogInformation("Item {Id} is already being processed, skipping", sourceItemId);
            return 0;
        }

        processingState = new ProcessingState
        {
            SourceItemId = sourceItemId,
            SourceItemName = item.Name,
            Status = ProcessingStatus.InProgress,
            Phase = ProcessingPhase.Extraction
        };
        await _processingStateRepository.CreateAsync(processingState, ct).ConfigureAwait(false);

        try
        {
            _logger.LogInformation("Extracting clips from {Name} ({Path})", item.Name, item.Path);

            var highlights = await _highlightDetection.DetectHighlightsAsync(
                item.Path,
                _config.SceneDetectionThreshold,
                _config.MinClipDurationSeconds,
                _config.MaxClipDurationSeconds,
                _config.MaxClipsPerVideo,
                ct).ConfigureAwait(false);

            processingState.TotalClips = highlights.Count;
            await _processingStateRepository.UpdateAsync(processingState, ct).ConfigureAwait(false);

            var clipDir = GetClipDirectory();
            var genre = item.Genres?.FirstOrDefault();
            var extracted = 0;

            foreach (var highlight in highlights)
            {
                ct.ThrowIfCancellationRequested();

                var clipId = Guid.NewGuid().ToString();
                var clipFileName = $"{clipId}.mp4";
                var thumbFileName = $"{clipId}.jpg";
                var clipPath = Path.Combine(clipDir, clipFileName);
                var thumbPath = Path.Combine(clipDir, thumbFileName);

                processingState.CurrentClipId = clipId;
                processingState.TempFilePath = clipPath;
                processingState.Phase = ProcessingPhase.Extraction;
                await _processingStateRepository.UpdateAsync(processingState, ct).ConfigureAwait(false);

                var success = await _ffmpeg.ExtractClipAsync(
                    item.Path, clipPath, highlight.StartTicks, highlight.EndTicks,
                    _config.VerticalCropMode, _config.TargetResolution, ct).ConfigureAwait(false);

                if (!success)
                {
                    _logger.LogWarning("Failed to extract clip from {Name} at {Start}", item.Name, highlight.StartTimeSeconds);
                    CleanupFile(clipPath);
                    continue;
                }

                processingState.Phase = ProcessingPhase.ThumbnailGeneration;
                await _processingStateRepository.UpdateAsync(processingState, ct).ConfigureAwait(false);

                var midTicks = highlight.StartTicks + (highlight.EndTicks - highlight.StartTicks) / 2;
                await _ffmpeg.GenerateThumbnailAsync(item.Path, thumbPath, midTicks, ct).ConfigureAwait(false);

                var clip = new Clip
                {
                    Id = clipId,
                    SourceItemId = sourceItemId,
                    SourceItemName = item.Name,
                    SourceItemOverview = item.Overview,
                    StartTimeTicks = highlight.StartTicks,
                    EndTimeTicks = highlight.EndTicks,
                    FilePath = clipPath,
                    ThumbnailPath = File.Exists(thumbPath) ? thumbPath : null,
                    Genre = genre,
                    SceneScore = highlight.Score,
                    DurationSeconds = highlight.DurationSeconds,
                    CropMode = _config.VerticalCropMode,
                    FileSizeBytes = File.Exists(clipPath) ? new FileInfo(clipPath).Length : 0,
                    IsProcessed = true
                };

                if (_multimodalAnalysis != null && _config.MultimodalConfig.EnableMultimodalAnalysis)
                {
                    processingState.Phase = ProcessingPhase.MultimodalAnalysis;
                    await _processingStateRepository.UpdateAsync(processingState, ct).ConfigureAwait(false);

                    try
                    {
                        var analysisResult = await _multimodalAnalysis.AnalyzeClipAsync(
                            item.Path, highlight.StartTicks, highlight.EndTicks, ct).ConfigureAwait(false);

                        if (analysisResult != null && analysisResult.IsSuccess)
                        {
                            clip.AiTitle = analysisResult.Title;
                            clip.AiDescription = analysisResult.Description;
                            clip.SemanticTags = string.Join(",", analysisResult.SemanticTags);
                            clip.MoodTag = analysisResult.MoodTag;
                            clip.IsMultimodalAnalyzed = true;
                            clip.MultimodalAnalyzedAt = DateTime.UtcNow;

                            _logger.LogInformation("Multimodal analysis succeeded for clip {ClipId}: {Title}",
                                clipId, analysisResult.Title);
                        }
                        else if (analysisResult != null)
                        {
                            _logger.LogWarning("Multimodal analysis failed for clip {ClipId}: {Reason}",
                                clipId, analysisResult.FailureReason);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Multimodal analysis error for clip {ClipId}", clipId);
                    }
                }

                await _clipRepository.AddAsync(clip, ct).ConfigureAwait(false);
                extracted++;
                processingState.ProcessedClips = extracted;
                processingState.TempFilePath = null;
                await _processingStateRepository.UpdateAsync(processingState, ct).ConfigureAwait(false);

                _logger.LogInformation("Extracted clip {ClipId} from {Name} ({Start:F1}s - {End:F1}s)",
                    clipId, item.Name, highlight.StartTimeSeconds, highlight.EndTimeSeconds);
            }

            processingState.Status = ProcessingStatus.Completed;
            processingState.CompletedAt = DateTime.UtcNow;
            await _processingStateRepository.UpdateAsync(processingState, ct).ConfigureAwait(false);

            return extracted;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Extraction cancelled for {Id}", sourceItemId);
            processingState.Status = ProcessingStatus.Cancelled;
            await _processingStateRepository.UpdateAsync(processingState, ct).ConfigureAwait(false);

            if (processingState.TempFilePath != null)
            {
                CleanupFile(processingState.TempFilePath);
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Extraction failed for {Id}", sourceItemId);
            processingState.Status = ProcessingStatus.Failed;
            processingState.ErrorMessage = ex.Message;
            await _processingStateRepository.UpdateAsync(processingState, ct).ConfigureAwait(false);

            if (processingState.TempFilePath != null)
            {
                CleanupFile(processingState.TempFilePath);
            }
            throw;
        }
    }

    public async Task<int> ProcessQueueAsync(CancellationToken ct = default)
    {
        var unprocessed = await _clipRepository.GetUnprocessedClipsAsync(ct).ConfigureAwait(false);
        var processed = 0;

        foreach (var clip in unprocessed)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (File.Exists(clip.FilePath))
                {
                    clip.IsProcessed = true;
                    await _clipRepository.UpdateAsync(clip, ct).ConfigureAwait(false);
                    processed++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing clip {ClipId}", clip.Id);
            }
        }

        return processed;
    }

    public async Task<int> RecoverInterruptedTasksAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Recovering interrupted tasks...");

        await _processingStateRepository.MarkInterruptedAsync(ct).ConfigureAwait(false);

        var interruptedTasks = await _processingStateRepository.GetInterruptedTasksAsync(ct).ConfigureAwait(false);
        var recovered = 0;

        foreach (var task in interruptedTasks)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                if (task.RetryCount >= 3)
                {
                    _logger.LogWarning("Task for {Id} exceeded max retries, marking as failed", task.SourceItemId);
                    task.Status = ProcessingStatus.Failed;
                    task.ErrorMessage = "Exceeded maximum retry count";
                    await _processingStateRepository.UpdateAsync(task, ct).ConfigureAwait(false);
                    continue;
                }

                if (task.TempFilePath != null)
                {
                    CleanupFile(task.TempFilePath);
                }

                task.RetryCount++;
                task.Status = ProcessingStatus.Pending;
                task.Phase = ProcessingPhase.Extraction;
                task.TempFilePath = null;
                await _processingStateRepository.UpdateAsync(task, ct).ConfigureAwait(false);

                _logger.LogInformation("Recovered task for {Id}, retry {Count}", task.SourceItemId, task.RetryCount);
                recovered++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to recover task for {Id}", task.SourceItemId);
            }
        }

        await CleanupOldTempFilesAsync(ct).ConfigureAwait(false);

        return recovered;
    }

    public async Task CleanupTempFilesAsync(CancellationToken ct = default)
    {
        await CleanupOldTempFilesAsync(ct).ConfigureAwait(false);
        await _processingStateRepository.CleanupOldStatesAsync(TimeSpan.FromDays(7), ct).ConfigureAwait(false);
    }

    private async Task CleanupOldTempFilesAsync(CancellationToken ct)
    {
        var clipDir = GetClipDirectory();
        try
        {
            var tempFiles = Directory.GetFiles(clipDir, "*.tmp");
            foreach (var file in tempFiles)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTimeUtc < DateTime.UtcNow.AddHours(-1))
                    {
                        File.Delete(file);
                        _logger.LogInformation("Cleaned up temp file {File}", file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temp file {File}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate temp files");
        }
    }

    private static void CleanupFile(string? filePath)
    {
        if (filePath == null) return;
        try
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
        catch { }
    }

    private string GetClipDirectory()
    {
        var baseDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "jellyfin", "plugins", "clips", "media");

        if (!Directory.Exists(baseDir))
        {
            Directory.CreateDirectory(baseDir);
        }

        return baseDir;
    }
}
