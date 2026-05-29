using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Jellyfin.Clips.Services;

namespace Jellyfin.Clips.Tasks;

public class ClipGenerationTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly IClipExtractionService _clipExtractionService;
    private readonly ILogger<ClipGenerationTask> _logger;

    public ClipGenerationTask(
        ILibraryManager libraryManager,
        IClipExtractionService clipExtractionService,
        ILogger<ClipGenerationTask> logger)
    {
        _libraryManager = libraryManager;
        _clipExtractionService = clipExtractionService;
        _logger = logger;
    }

    public string Name => "Generate Clips";
    public string Description => "Scans the video library and extracts highlight clips for the feed.";
    public string Category => "Clips";
    public string Key => "ClipsGeneration";

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfo.TriggerDaily,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
            }
        };
    }

    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting scheduled clip generation task");

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { MediaBrowser.Model.Entities.BaseItemKind.Movie, MediaBrowser.Model.Entities.BaseItemKind.Episode },
            IsVirtualItem = false,
            OrderBy = new[] { (MediaBrowser.Model.Entities.ItemSortBy.DateCreated, MediaBrowser.Model.Entities.SortOrder.Descending) }
        };

        var items = _libraryManager.GetItemList(query);
        var total = items.Count;
        var processed = 0;
        var extracted = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var count = await _clipExtractionService.ExtractClipsFromItemAsync(
                    item.Id.ToString(), false, cancellationToken).ConfigureAwait(false);
                extracted += count;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract clips from {Name}", item.Name);
            }

            processed++;
            progress.Report((double)processed / total * 100);
        }

        _logger.LogInformation("Clip generation task completed. Processed {Processed} items, extracted {Extracted} clips",
            processed, extracted);
    }
}
