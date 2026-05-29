namespace Jellyfin.Clips.Data.Entities;

public class UserInteraction
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ClipId { get; set; } = string.Empty;
    public InteractionType InteractionType { get; set; }
    public long DwellTimeMs { get; set; }
    public double CompletionRate { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public Clip? Clip { get; set; }
}

public enum InteractionType
{
    View = 0,
    Like = 1,
    Dislike = 2,
    Skip = 3,
    Rewatch = 4,
    ClickThrough = 5,
    Share = 6
}
