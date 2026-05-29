namespace Jellyfin.Clips.Model;

public class ClipDto
{
    public string Id { get; set; } = string.Empty;
    public string SourceItemId { get; set; } = string.Empty;
    public string SourceItemName { get; set; } = string.Empty;
    public string? SourceItemOverview { get; set; }
    public long StartTimeTicks { get; set; }
    public long EndTimeTicks { get; set; }
    public int DurationSeconds { get; set; }
    public string? Genre { get; set; }
    public double SceneScore { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? StreamUrl { get; set; }
    public int LikeCount { get; set; }
    public double AvgCompletionRate { get; set; }
    public bool HasLiked { get; set; }
    public string? AiTitle { get; set; }
    public string? AiDescription { get; set; }
    public List<string> SemanticTags { get; set; } = new();
    public string? MoodTag { get; set; }
}
