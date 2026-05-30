using System.Text.Json.Serialization;

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
    [JsonPropertyName("clips")]
    public IReadOnlyList<ClipDto> Clips { get; set; } = Array.Empty<ClipDto>();
    [JsonPropertyName("nextCursor")]
    public string? NextCursor { get; set; }
    [JsonPropertyName("totalAvailable")]
    public int TotalAvailable { get; set; }
}
