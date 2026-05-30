using System.Net.Mime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Jellyfin.Clips.Data;
using Jellyfin.Clips.Data.Entities;
using Jellyfin.Clips.Data.Repositories;
using Jellyfin.Clips.Services;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Clips.Api;

[ApiController]
[Route("Plugins/Clips/Clip")]
[Produces(MediaTypeNames.Application.Json)]
public class ClipController : ControllerBase
{
    private readonly IClipRepository _clipRepository;
    private readonly IFeedService _feedService;
    private readonly IDbContextFactory<ClipsDbContext> _dbFactory;

    public ClipController(
        IClipRepository clipRepository,
        IFeedService feedService,
        IDbContextFactory<ClipsDbContext> dbFactory)
    {
        _clipRepository = clipRepository;
        _feedService = feedService;
        _dbFactory = dbFactory;
    }

    [AllowAnonymous]
    [HttpDelete("{clipId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteClip(string clipId, CancellationToken ct = default)
    {
        var clip = await _clipRepository.GetByIdAsync(clipId, ct).ConfigureAwait(false);
        if (clip is null) return NotFound();

        var sourceItemId = clip.SourceItemId;

        try { if (System.IO.File.Exists(clip.FilePath)) System.IO.File.Delete(clip.FilePath); } catch { }
        try { if (clip.ThumbnailPath != null && System.IO.File.Exists(clip.ThumbnailPath)) System.IO.File.Delete(clip.ThumbnailPath); } catch { }

        await _clipRepository.DeleteAsync(clipId, ct).ConfigureAwait(false);

        var remaining = await _clipRepository.GetBySourceItemIdAsync(sourceItemId, ct).ConfigureAwait(false);
        if (remaining.Count == 0)
        {
            using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var exists = await db.DeletedSourceItems.AnyAsync(d => d.SourceItemId == sourceItemId, ct).ConfigureAwait(false);
            if (!exists)
            {
                db.DeletedSourceItems.Add(new DeletedSourceItem { SourceItemId = sourceItemId });
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
            }
        }

        return Ok();
    }

    [AllowAnonymous]
    [HttpGet("{clipId}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClipDetail(string clipId, CancellationToken ct = default)
    {
        var detail = await _feedService.GetClipDetailAsync(clipId, "anonymous", ct).ConfigureAwait(false);
        if (detail is null) return NotFound();
        return Ok(detail);
    }

    [AllowAnonymous]
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

    [AllowAnonymous]
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
}
