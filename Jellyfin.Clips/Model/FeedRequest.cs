namespace Jellyfin.Clips.Model;

public class FeedRequest
{
    public int Count { get; set; } = 20;
    public string? Cursor { get; set; }
    public string? Genre { get; set; }
    public string? ExcludeClipId { get; set; }
}

public class FeedResponse
{
    public IReadOnlyList<ClipDto> Clips { get; set; } = Array.Empty<ClipDto>();
    public string? NextCursor { get; set; }
    public int TotalAvailable { get; set; }
}
