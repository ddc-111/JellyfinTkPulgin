using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Jellyfin.Clips.Model;
using Jellyfin.Clips.Services;

namespace Jellyfin.Clips.Api;

[ApiController]
[Route("Plugins/Clips")]
[Produces(MediaTypeNames.Application.Json)]
public class FeedController : ControllerBase
{
    private readonly IFeedService _feedService;

    public FeedController(IFeedService feedService)
    {
        _feedService = feedService;
    }

    [AllowAnonymous]
    [HttpGet("Feed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<FeedResponse>> GetFeed(
        [FromQuery] int count = 20,
        [FromQuery] string? cursor = null,
        [FromQuery] string? genre = null,
        CancellationToken ct = default)
    {
        var request = new FeedRequest
        {
            Count = count,
            Cursor = cursor,
            Genre = genre
        };

        var response = await _feedService.GetFeedAsync("anonymous", request, ct).ConfigureAwait(false);
        return Ok(response);
    }
}
