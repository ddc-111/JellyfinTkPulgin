using System.Net.Mime;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Jellyfin.Clips.Model;
using Jellyfin.Clips.Services;

namespace Jellyfin.Clips.Api;

[ApiController]
[Authorize]
[Route("Plugins/Clips")]
[Produces(MediaTypeNames.Application.Json)]
public class FeedController : ControllerBase
{
    private readonly IFeedService _feedService;
    private readonly ILogger<FeedController> _logger;

    public FeedController(IFeedService feedService, ILogger<FeedController> logger)
    {
        _feedService = feedService;
        _logger = logger;
    }

    [HttpGet("Feed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<FeedResponse>> GetFeed(
        [FromQuery] int count = 20,
        [FromQuery] string? cursor = null,
        [FromQuery] string? genre = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var request = new FeedRequest
        {
            Count = count,
            Cursor = cursor,
            Genre = genre
        };

        var response = await _feedService.GetFeedAsync(userId, request, ct).ConfigureAwait(false);
        return Ok(response);
    }

    private string? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim?.Value;
    }
}
