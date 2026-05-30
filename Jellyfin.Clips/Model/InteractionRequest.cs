using System.Text.Json.Serialization;
using Jellyfin.Clips.Data.Entities;

namespace Jellyfin.Clips.Model;

public class InteractionRequest
{
    [JsonPropertyName("clipId")]
    public string ClipId { get; set; } = string.Empty;
    [JsonPropertyName("type")]
    public InteractionType Type { get; set; }
    [JsonPropertyName("dwellTimeMs")]
    public long DwellTimeMs { get; set; }
    [JsonPropertyName("completionRate")]
    public double CompletionRate { get; set; }
}

public class ClipDetailResponse
{
    [JsonPropertyName("clip")]
    public ClipDto Clip { get; set; } = new();
    [JsonPropertyName("originalItemId")]
    public string? OriginalItemId { get; set; }
    [JsonPropertyName("originalItemName")]
    public string? OriginalItemName { get; set; }
    [JsonPropertyName("originalItemPlaybackUrl")]
    public string? OriginalItemPlaybackUrl { get; set; }
    [JsonPropertyName("chapters")]
    public IReadOnlyList<ChapterInfoDto>? Chapters { get; set; }
}

public class ChapterInfoDto
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("startPositionTicks")]
    public long StartPositionTicks { get; set; }
    [JsonPropertyName("imagePath")]
    public string? ImagePath { get; set; }
}

public class GenerationStatusResponse
{
    [JsonPropertyName("isRunning")]
    public bool IsRunning { get; set; }
    [JsonPropertyName("processedCount")]
    public int ProcessedCount { get; set; }
    [JsonPropertyName("queuedCount")]
    public int QueuedCount { get; set; }
    [JsonPropertyName("currentItem")]
    public string? CurrentItem { get; set; }
    [JsonPropertyName("startedAt")]
    public DateTime? StartedAt { get; set; }
}

public class AdminGenerateRequest
{
    [JsonPropertyName("sourceItemId")]
    public string? SourceItemId { get; set; }
    [JsonPropertyName("forceRegenerate")]
    public bool ForceRegenerate { get; set; }
}
