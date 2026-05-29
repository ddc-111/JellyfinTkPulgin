namespace Jellyfin.Clips.Data.Entities;

public class Clip
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceItemId { get; set; } = string.Empty;
    public string SourceItemName { get; set; } = string.Empty;
    public string? SourceItemOverview { get; set; }
    public long StartTimeTicks { get; set; }
    public long EndTimeTicks { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string? ThumbnailPath { get; set; }
    public string? Genre { get; set; }
    public double SceneScore { get; set; }
    public int DurationSeconds { get; set; }
    public string CropMode { get; set; } = "center";
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsProcessed { get; set; }

    public ICollection<UserInteraction> Interactions { get; set; } = new List<UserInteraction>();
}
