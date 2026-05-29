using Microsoft.Extensions.Logging;

namespace Jellyfin.Clips.Services;

public interface IHighlightDetectionService
{
    Task<IReadOnlyList<HighlightSegment>> DetectHighlightsAsync(
        string filePath, double sceneThreshold, int minDuration, int maxDuration, int maxClips, CancellationToken ct = default);
}

public class HighlightSegment
{
    public double StartTimeSeconds { get; set; }
    public double EndTimeSeconds { get; set; }
    public double Score { get; set; }
    public long StartTicks => (long)(StartTimeSeconds * 10_000_000);
    public long EndTicks => (long)(EndTimeSeconds * 10_000_000);
    public int DurationSeconds => (int)(EndTimeSeconds - StartTimeSeconds);
}

public class HighlightDetectionService : IHighlightDetectionService
{
    private readonly IFfmpegWrapper _ffmpeg;
    private readonly ILogger<HighlightDetectionService> _logger;

    public HighlightDetectionService(IFfmpegWrapper ffmpeg, ILogger<HighlightDetectionService> logger)
    {
        _ffmpeg = ffmpeg;
        _logger = logger;
    }

    public async Task<IReadOnlyList<HighlightSegment>> DetectHighlightsAsync(
        string filePath, double sceneThreshold, int minDuration, int maxDuration, int maxClips, CancellationToken ct = default)
    {
        _logger.LogInformation("Detecting highlights in {Path} with threshold {Threshold}", filePath, sceneThreshold);

        var sceneTimestamps = await _ffmpeg.DetectSceneChangesAsync(filePath, sceneThreshold, ct).ConfigureAwait(false);

        if (sceneTimestamps.Length == 0)
        {
            _logger.LogInformation("No scene changes detected, falling back to uniform sampling");
            return await FallbackUniformSamplingAsync(filePath, minDuration, maxDuration, maxClips, ct).ConfigureAwait(false);
        }

        var totalDuration = await _ffmpeg.GetDurationAsync(filePath, ct).ConfigureAwait(false);
        var totalSeconds = totalDuration.TotalSeconds;

        var segments = BuildSegmentsFromScenes(sceneTimestamps, totalSeconds, minDuration, maxDuration);
        var scored = ScoreSegments(segments, sceneTimestamps);
        var selected = scored
            .OrderByDescending(s => s.Score)
            .Take(maxClips)
            .OrderBy(s => s.StartTimeSeconds)
            .ToList();

        _logger.LogInformation("Detected {Count} highlight segments", selected.Count);
        return selected;
    }

    private List<HighlightSegment> BuildSegmentsFromScenes(
        double[] sceneTimestamps, double totalSeconds, int minDuration, int maxDuration)
    {
        var segments = new List<HighlightSegment>();

        for (var i = 0; i < sceneTimestamps.Length; i++)
        {
            var start = sceneTimestamps[i];
            var end = i + 1 < sceneTimestamps.Length ? sceneTimestamps[i + 1] : totalSeconds;

            if (end - start < minDuration)
            {
                var extension = minDuration - (end - start);
                start = Math.Max(0, start - extension / 2);
                end = Math.Min(totalSeconds, end + extension / 2);
            }

            if (end - start > maxDuration)
            {
                end = start + maxDuration;
            }

            if (end - start >= minDuration && start >= 0)
            {
                segments.Add(new HighlightSegment
                {
                    StartTimeSeconds = start,
                    EndTimeSeconds = end
                });
            }
        }

        return segments;
    }

    private List<HighlightSegment> ScoreSegments(
        List<HighlightSegment> segments, double[] sceneTimestamps)
    {
        foreach (var segment in segments)
        {
            var scenesInRange = sceneTimestamps.Count(t =>
                t >= segment.StartTimeSeconds && t <= segment.EndTimeSeconds);
            var sceneDensity = (double)scenesInRange / segment.DurationSeconds;
            var positionBonus = 1.0 - Math.Abs(segment.StartTimeSeconds - sceneTimestamps.FirstOrDefault()) / 100.0;
            segment.Score = sceneDensity * 0.7 + Math.Max(0, positionBonus) * 0.3;
        }

        return segments;
    }

    private async Task<IReadOnlyList<HighlightSegment>> FallbackUniformSamplingAsync(
        string filePath, int minDuration, int maxDuration, int maxClips, CancellationToken ct)
    {
        var totalDuration = await _ffmpeg.GetDurationAsync(filePath, ct).ConfigureAwait(false);
        var totalSeconds = totalDuration.TotalSeconds;
        var segmentDuration = Math.Min(maxDuration, Math.Max(minDuration, totalSeconds / (maxClips + 1)));
        var segments = new List<HighlightSegment>();

        for (var i = 0; i < maxClips; i++)
        {
            var start = segmentDuration * i + segmentDuration * 0.1;
            if (start + segmentDuration > totalSeconds) break;

            segments.Add(new HighlightSegment
            {
                StartTimeSeconds = start,
                EndTimeSeconds = start + segmentDuration,
                Score = 0.5
            });
        }

        return segments;
    }
}
