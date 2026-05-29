using System.Net.Mime;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Jellyfin.Clips.Data.Repositories;
using Jellyfin.Clips.Services;

namespace Jellyfin.Clips.Api;

[ApiController]
[Authorize]
[Route("Plugins/Clips/Clip")]
[Produces(MediaTypeNames.Application.Json)]
public class ClipController : ControllerBase
{
    private readonly IClipRepository _clipRepository;
    private readonly IFeedService _feedService;
    private readonly ILogger<ClipController> _logger;

    public ClipController(
        IClipRepository clipRepository,
        IFeedService feedService,
        ILogger<ClipController> logger)
    {
        _clipRepository = clipRepository;
        _feedService = feedService;
        _logger = logger;
    }

    [HttpGet("{clipId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClipDetail(string clipId, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        var detail = await _feedService.GetClipDetailAsync(clipId, userId, ct).ConfigureAwait(false);
        if (detail is null) return NotFound();

        return Ok(detail);
    }

    [HttpGet("{clipId}/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> StreamClip(string clipId, CancellationToken ct = default)
    {
        var clip = await _clipRepository.GetByIdAsync(clipId, ct).ConfigureAwait(false);
        if (clip is null || !System.IO.File.Exists(clip.FilePath))
        {
            return NotFound();
        }

        var stream = new System.IO.FileStream(clip.FilePath, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
        return File(stream, "video/mp4", enableRangeProcessing: true);
    }

    [HttpGet("{clipId}/thumbnail")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetThumbnail(string clipId, CancellationToken ct = default)
    {
        var clip = await _clipRepository.GetByIdAsync(clipId, ct).ConfigureAwait(false);
        if (clip?.ThumbnailPath is null || !System.IO.File.Exists(clip.ThumbnailPath))
        {
            return NotFound();
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(clip.ThumbnailPath, ct).ConfigureAwait(false);
        return File(bytes, "image/jpeg");
    }

    private string? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return claim?.Value;
    }
}
