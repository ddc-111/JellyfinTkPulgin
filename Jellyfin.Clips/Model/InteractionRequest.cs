using Jellyfin.Clips.Data.Entities;

namespace Jellyfin.Clips.Model;

public class InteractionRequest
{
    public string ClipId { get; set; } = string.Empty;
    public InteractionType Type { get; set; }
    public long DwellTimeMs { get; set; }
    public double CompletionRate { get; set; }
}

public class ClipDetailResponse
{
    public ClipDto Clip { get; set; } = new();
    public string? OriginalItemId { get; set; }
    public string? OriginalItemName { get; set; }
    public string? OriginalItemPlaybackUrl { get; set; }
    public IReadOnlyList<ChapterInfoDto>? Chapters { get; set; }
}

public class ChapterInfoDto
{
    public string? Name { get; set; }
    public long StartPositionTicks { get; set; }
    public string? ImagePath { get; set; }
}

public class GenerationStatusResponse
{
    public bool IsRunning { get; set; }
    public int ProcessedCount { get; set; }
    public int QueuedCount { get; set; }
    public string? CurrentItem { get; set; }
    public DateTime? StartedAt { get; set; }
}

public class AdminGenerateRequest
{
    public string? SourceItemId { get; set; }
    public bool ForceRegenerate { get; set; }
}
