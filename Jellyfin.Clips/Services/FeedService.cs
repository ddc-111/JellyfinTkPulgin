using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;
using Jellyfin.Clips.Data.Repositories;
using Jellyfin.Clips.Model;

namespace Jellyfin.Clips.Services;

public interface IFeedService
{
    Task<FeedResponse> GetFeedAsync(string userId, FeedRequest request, CancellationToken ct = default);
    Task<ClipDetailResponse?> GetClipDetailAsync(string clipId, string userId, CancellationToken ct = default);
    Task<bool> ToggleLikeAsync(string userId, string clipId, CancellationToken ct = default);
    Task RecordInteractionAsync(string userId, InteractionRequest request, CancellationToken ct = default);
}

public class FeedService : IFeedService
{
    private readonly IClipRepository _clipRepository;
    private readonly IInteractionRepository _interactionRepository;
    private readonly IRecommendationEngine _recommendationEngine;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<FeedService> _logger;

    public FeedService(
        IClipRepository clipRepository,
        IInteractionRepository interactionRepository,
        IRecommendationEngine recommendationEngine,
        ILibraryManager libraryManager,
        ILogger<FeedService> logger)
    {
        _clipRepository = clipRepository;
        _interactionRepository = interactionRepository;
        _recommendationEngine = recommendationEngine;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public async Task<FeedResponse> GetFeedAsync(string userId, FeedRequest request, CancellationToken ct = default)
    {
        var count = Math.Min(request.Count, 50);
        var scored = await _recommendationEngine.GetRecommendedClipsAsync(userId, count + 5, request.Genre, ct).ConfigureAwait(false);

        if (request.ExcludeClipId is not null)
        {
            scored = scored.Where(s => s.Clip.Id != request.ExcludeClipId).ToList();
        }

        var clips = new List<ClipDto>();
        foreach (var sc in scored.Take(count))
        {
            var hasLiked = await _interactionRepository.HasUserLikedClipAsync(userId, sc.Clip.Id, ct).ConfigureAwait(false);
            var likeCount = await _interactionRepository.GetLikeCountAsync(sc.Clip.Id, ct).ConfigureAwait(false);
            var avgCompletion = await _interactionRepository.GetAverageCompletionRateAsync(sc.Clip.Id, ct).ConfigureAwait(false);

            clips.Add(MapToDto(sc.Clip, hasLiked, likeCount, avgCompletion));
        }

        var totalCount = await _clipRepository.GetTotalCountAsync(ct).ConfigureAwait(false);
        var lastId = clips.LastOrDefault()?.Id;

        return new FeedResponse
        {
            Clips = clips,
            NextCursor = lastId,
            TotalAvailable = totalCount
        };
    }

    public async Task<ClipDetailResponse?> GetClipDetailAsync(string clipId, string userId, CancellationToken ct = default)
    {
        var clip = await _clipRepository.GetByIdAsync(clipId, ct).ConfigureAwait(false);
        if (clip is null) return null;

        var hasLiked = await _interactionRepository.HasUserLikedClipAsync(userId, clipId, ct).ConfigureAwait(false);
        var likeCount = await _interactionRepository.GetLikeCountAsync(clipId, ct).ConfigureAwait(false);
        var avgCompletion = await _interactionRepository.GetAverageCompletionRateAsync(clipId, ct).ConfigureAwait(false);

        var response = new ClipDetailResponse
        {
            Clip = MapToDto(clip, hasLiked, likeCount, avgCompletion),
            OriginalItemId = clip.SourceItemId,
            OriginalItemName = clip.SourceItemName
        };

        try
        {
            var item = _libraryManager.GetItemById(new Guid(clip.SourceItemId));
            if (item is not null)
            {
                response.OriginalItemPlaybackUrl = $"/web/index.html#!/details?id={clip.SourceItemId}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get original item for clip {ClipId}", clipId);
        }

        return response;
    }

    public async Task<bool> ToggleLikeAsync(string userId, string clipId, CancellationToken ct = default)
    {
        var hasLiked = await _interactionRepository.HasUserLikedClipAsync(userId, clipId, ct).ConfigureAwait(false);

        var interaction = new Data.Entities.UserInteraction
        {
            UserId = userId,
            ClipId = clipId,
            InteractionType = hasLiked ? Data.Entities.InteractionType.Dislike : Data.Entities.InteractionType.Like,
            Timestamp = DateTime.UtcNow
        };

        await _interactionRepository.RecordInteractionAsync(interaction, ct).ConfigureAwait(false);
        await _recommendationEngine.UpdateUserProfileAsync(userId, ct).ConfigureAwait(false);

        return !hasLiked;
    }

    public async Task RecordInteractionAsync(string userId, InteractionRequest request, CancellationToken ct = default)
    {
        var interaction = new Data.Entities.UserInteraction
        {
            UserId = userId,
            ClipId = request.ClipId,
            InteractionType = request.Type,
            DwellTimeMs = request.DwellTimeMs,
            CompletionRate = request.CompletionRate,
            Timestamp = DateTime.UtcNow
        };

        await _interactionRepository.RecordInteractionAsync(interaction, ct).ConfigureAwait(false);
        await _recommendationEngine.UpdateUserProfileAsync(userId, ct).ConfigureAwait(false);
    }

    private static ClipDto MapToDto(Data.Entities.Clip clip, bool hasLiked, int likeCount, double avgCompletion)
    {
        return new ClipDto
        {
            Id = clip.Id,
            SourceItemId = clip.SourceItemId,
            SourceItemName = clip.SourceItemName,
            SourceItemOverview = clip.SourceItemOverview,
            StartTimeTicks = clip.StartTimeTicks,
            EndTimeTicks = clip.EndTimeTicks,
            DurationSeconds = clip.DurationSeconds,
            Genre = clip.Genre,
            SceneScore = clip.SceneScore,
            ThumbnailUrl = $"/Plugins/Clips/Clip/{clip.Id}/thumbnail",
            StreamUrl = $"/Plugins/Clips/Clip/{clip.Id}/stream",
            LikeCount = likeCount,
            AvgCompletionRate = avgCompletion,
            HasLiked = hasLiked
        };
    }
}
