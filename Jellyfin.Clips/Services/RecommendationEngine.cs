using System.Text.Json;
using Microsoft.Extensions.Logging;
using Jellyfin.Clips.Configuration;
using Jellyfin.Clips.Data.Entities;
using Jellyfin.Clips.Data.Repositories;

namespace Jellyfin.Clips.Services;

public interface IRecommendationEngine
{
    Task<IReadOnlyList<ScoredClip>> GetRecommendedClipsAsync(string userId, int count, string? genre = null, CancellationToken ct = default);
    Task UpdateUserProfileAsync(string userId, CancellationToken ct = default);
}

public class ScoredClip
{
    public Clip Clip { get; set; } = null!;
    public double Score { get; set; }
}

public class RecommendationEngine : IRecommendationEngine
{
    private readonly IClipRepository _clipRepository;
    private readonly IInteractionRepository _interactionRepository;
    private readonly ILogger<RecommendationEngine> _logger;
    private readonly RecommendationWeights _weights;

    private static readonly Random _random = new();

    public RecommendationEngine(
        IClipRepository clipRepository,
        IInteractionRepository interactionRepository,
        ILogger<RecommendationEngine> logger)
    {
        _clipRepository = clipRepository;
        _interactionRepository = interactionRepository;
        _logger = logger;
        _weights = Plugin.Instance?.Configuration?.RecommendationWeights ?? new RecommendationWeights();
    }

    public async Task<IReadOnlyList<ScoredClip>> GetRecommendedClipsAsync(
        string userId, int count, string? genre = null, CancellationToken ct = default)
    {
        var allClips = genre is null
            ? await _clipRepository.GetAllAsync(ct).ConfigureAwait(false)
            : await _clipRepository.GetClipsByGenreAsync(genre, 200, ct).ConfigureAwait(false);

        var processedClips = allClips.Where(c => c.IsProcessed).ToList();
        if (processedClips.Count == 0)
        {
            return Array.Empty<ScoredClip>();
        }

        var genrePrefs = await _interactionRepository.GetUserGenrePreferencesAsync(userId, ct).ConfigureAwait(false);
        var likedClipIds = new HashSet<string>(await _interactionRepository.GetLikedClipIdsAsync(userId, ct).ConfigureAwait(false));
        var recentInteractions = await _interactionRepository.GetUserInteractionsAsync(userId, 50, ct).ConfigureAwait(false);
        var recentClipIds = new HashSet<string>(recentInteractions.Select(i => i.ClipId));

        var scored = new List<ScoredClip>();

        foreach (var clip in processedClips)
        {
            if (recentClipIds.Contains(clip.Id)) continue;

            var score = CalculateScore(clip, genrePrefs, likedClipIds);
            scored.Add(new ScoredClip { Clip = clip, Score = score });
        }

        var result = scored
            .OrderByDescending(s => s.Score + _random.NextDouble() * 0.1)
            .Take(count)
            .ToList();

        _logger.LogDebug("Recommended {Count} clips for user {UserId}", result.Count, userId);
        return result;
    }

    public async Task UpdateUserProfileAsync(string userId, CancellationToken ct = default)
    {
        var interactions = await _interactionRepository.GetUserInteractionsAsync(userId, 200, ct).ConfigureAwait(false);
        if (interactions.Count == 0) return;

        var genreCounts = new Dictionary<string, double>();
        var totalDwell = 0L;
        var count = 0;

        foreach (var interaction in interactions)
        {
            var clip = await _clipRepository.GetByIdAsync(interaction.ClipId, ct).ConfigureAwait(false);
            if (clip?.Genre is null) continue;

            if (!genreCounts.ContainsKey(clip.Genre))
            {
                genreCounts[clip.Genre] = 0;
            }

            var weight = interaction.InteractionType switch
            {
                InteractionType.Like => 3.0,
                InteractionType.Rewatch => 2.5,
                InteractionType.ClickThrough => 2.0,
                InteractionType.View => interaction.CompletionRate,
                InteractionType.Skip => -0.5,
                InteractionType.Dislike => -2.0,
                _ => 0.5
            };

            genreCounts[clip.Genre] += weight;
            totalDwell += interaction.DwellTimeMs;
            count++;
        }

        var totalWeight = genreCounts.Values.Sum(Math.Abs);
        if (totalWeight > 0)
        {
            foreach (var key in genreCounts.Keys.ToList())
            {
                genreCounts[key] = Math.Max(0, genreCounts[key]) / totalWeight;
            }
        }

        var profile = new UserProfile
        {
            UserId = userId,
            GenrePreferencesJson = JsonSerializer.Serialize(genreCounts),
            AvgDwellTimeMs = count > 0 ? (double)totalDwell / count : 0,
            TotalInteractions = interactions.Count,
            LastUpdated = DateTime.UtcNow
        };

        await _interactionRepository.UpdateUserProfileAsync(profile, ct).ConfigureAwait(false);
    }

    private double CalculateScore(Clip clip, Dictionary<string, double> genrePrefs, HashSet<string> likedClipIds)
    {
        double score = 0;

        if (clip.Genre is not null && genrePrefs.TryGetValue(clip.Genre, out var genreWeight))
        {
            score += genreWeight * _weights.GenrePreference;
        }

        score += clip.SceneScore * _weights.SceneScore;

        var ageHours = (DateTime.UtcNow - clip.CreatedAt).TotalHours;
        var recencyBonus = Math.Exp(-ageHours / 168);
        score += recencyBonus * _weights.RecencyBonus;

        score += _random.NextDouble() * _weights.DiversityBonus;

        if (likedClipIds.Contains(clip.Id))
        {
            score -= 1.0;
        }

        return Math.Max(0, score);
    }
}
