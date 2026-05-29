namespace Jellyfin.Clips.Data.Entities;

public class ProcessingState
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceItemId { get; set; } = string.Empty;
    public string SourceItemName { get; set; } = string.Empty;
    public ProcessingStatus Status { get; set; } = ProcessingStatus.Pending;
    public ProcessingPhase Phase { get; set; } = ProcessingPhase.Extraction;
    public string? CurrentClipId { get; set; }
    public int ProcessedClips { get; set; }
    public int TotalClips { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TempFilePath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public int RetryCount { get; set; }
}

public enum ProcessingStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled,
    Interrupted
}

public enum ProcessingPhase
{
    Extraction,
    ThumbnailGeneration,
    MultimodalAnalysis,
    Cleanup
}
