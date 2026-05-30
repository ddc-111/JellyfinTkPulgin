namespace Jellyfin.Clips.Data.Entities;

public class DeletedSourceItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceItemId { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
}
