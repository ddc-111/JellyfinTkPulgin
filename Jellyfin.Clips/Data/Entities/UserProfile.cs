namespace Jellyfin.Clips.Data.Entities;

public class UserProfile
{
    public string UserId { get; set; } = string.Empty;
    public string GenrePreferencesJson { get; set; } = "{}";
    public double AvgDwellTimeMs { get; set; }
    public int TotalInteractions { get; set; }
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
