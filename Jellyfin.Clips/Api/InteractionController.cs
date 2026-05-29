using System.Net.Mime;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Jellyfin.Clips.Data.Entities;
using Jellyfin.Clips.Data.Repositories;
using Jellyfin.Clips.Model;
using Jellyfin.Clips.Services;

namespace Jellyfin.Clips.Api;

[ApiController]
[Authorize]
[Route("Plugins/Clips")]
[Produces(MediaTypeNames.Application.Json)]
public class InteractionController : ControllerBase
{
    private readonly IFeedService _feedService;
    private readonly IClipExtractionService _clipExtractionService;
    private readonly IClipRepository _clipRepository;
    private readonly ILogger<InteractionController> _logger;

    public InteractionController(
        IFeedService feedService,
        IClipExtractionService clipExtractionService,
        IClipRepository clipRepository,
        ILogger<InteractionController> logger)
    {
        _feedService = feedService;
        _clipExtractionService = clipExtractionService;
        _clipRepository = clipRepository;
        _logger = logger;
    }

    [HttpPost("Interaction")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordInteraction(
        [FromBody] InteractionRequest request,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        await _feedService.RecordInteractionAsync(userId, request, ct).ConfigureAwait(false);
        return Ok();
    }

    [HttpPost("Like/{clipId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ToggleLike(string clipId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var nowLiked = await _feedService.ToggleLikeAsync(userId, clipId, ct).ConfigureAwait(false);
        return Ok(new { liked = nowLiked });
    }

    [HttpPost("Admin/Generate")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateClips(
        [FromBody] AdminGenerateRequest request,
        CancellationToken ct = default)
    {
        if (request.SourceItemId is not null)
        {
            var count = await _clipExtractionService.ExtractClipsFromItemAsync(
                request.SourceItemId, request.ForceRegenerate, ct).ConfigureAwait(false);
            return Ok(new { extracted = count });
        }

        var processed = await _clipExtractionService.ProcessQueueAsync(ct).ConfigureAwait(false);
        return Ok(new { processed });
    }

    [HttpGet("Admin/Status")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct = default)
    {
        var totalCount = await _clipRepository.GetTotalCountAsync(ct).ConfigureAwait(false);
        var totalSize = await _clipRepository.GetTotalFileSizeAsync(ct).ConfigureAwait(false);
        var unprocessed = await _clipRepository.GetUnprocessedClipsAsync(ct).ConfigureAwait(false);

        return Ok(new GenerationStatusResponse
        {
            IsRunning = false,
            ProcessedCount = totalCount,
            QueuedCount = unprocessed.Count
        });
    }

    private string? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim?.Value;
    }
}
