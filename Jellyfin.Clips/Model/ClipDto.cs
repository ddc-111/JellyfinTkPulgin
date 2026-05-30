using System.Text.Json.Serialization;

namespace Jellyfin.Clips.Model;

public class ClipDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
    [JsonPropertyName("sourceItemId")]
    public string SourceItemId { get; set; } = string.Empty;
    [JsonPropertyName("sourceItemName")]
    public string SourceItemName { get; set; } = string.Empty;
    [JsonPropertyName("sourceItemOverview")]
    public string? SourceItemOverview { get; set; }
    [JsonPropertyName("startTimeTicks")]
    public long StartTimeTicks { get; set; }
    [JsonPropertyName("endTimeTicks")]
    public long EndTimeTicks { get; set; }
    [JsonPropertyName("durationSeconds")]
    public int DurationSeconds { get; set; }
    [JsonPropertyName("genre")]
    public string? Genre { get; set; }
    [JsonPropertyName("sceneScore")]
    public double SceneScore { get; set; }
    [JsonPropertyName("thumbnailUrl")]
    public string? ThumbnailUrl { get; set; }
    [JsonPropertyName("streamUrl")]
    public string? StreamUrl { get; set; }
    [JsonPropertyName("likeCount")]
    public int LikeCount { get; set; }
    [JsonPropertyName("avgCompletionRate")]
    public double AvgCompletionRate { get; set; }
    [JsonPropertyName("hasLiked")]
    public bool HasLiked { get; set; }
    [JsonPropertyName("aiTitle")]
    public string? AiTitle { get; set; }
    [JsonPropertyName("aiDescription")]
    public string? AiDescription { get; set; }
    [JsonPropertyName("semanticTags")]
    public List<string> SemanticTags { get; set; } = new();
    [JsonPropertyName("moodTag")]
    public string? MoodTag { get; set; }
}
