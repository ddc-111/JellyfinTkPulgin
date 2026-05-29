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
    Task<Dictionary<string, double>> GetUserSemanticTagPreferencesAsync(string userId, CancellationToken ct = default);
    Task<Dictionary<string, double>> GetUserMoodTagPreferencesAsync(string userId, CancellationToken ct = default);
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
        var semanticTagPrefs = await GetUserSemanticTagPreferencesAsync(userId, ct).ConfigureAwait(false);
        var moodTagPrefs = await GetUserMoodTagPreferencesAsync(userId, ct).ConfigureAwait(false);
        var likedClipIds = new HashSet<string>(await _interactionRepository.GetLikedClipIdsAsync(userId, ct).ConfigureAwait(false));
        var recentInteractions = await _interactionRepository.GetUserInteractionsAsync(userId, 50, ct).ConfigureAwait(false);
        var recentClipIds = new HashSet<string>(recentInteractions.Select(i => i.ClipId));

        var scored = new List<ScoredClip>();

        foreach (var clip in processedClips)
        {
            if (recentClipIds.Contains(clip.Id)) continue;

            var score = CalculateScore(clip, genrePrefs, semanticTagPrefs, moodTagPrefs, likedClipIds);
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
        var semanticTagCounts = new Dictionary<string, double>();
        var moodTagCounts = new Dictionary<string, double>();
        var totalDwell = 0L;
        var count = 0;

        foreach (var interaction in interactions)
        {
            var clip = await _clipRepository.GetByIdAsync(interaction.ClipId, ct).ConfigureAwait(false);
            if (clip == null) continue;

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

            if (clip.Genre is not null)
            {
                if (!genreCounts.ContainsKey(clip.Genre))
                {
                    genreCounts[clip.Genre] = 0;
                }
                genreCounts[clip.Genre] += weight;
            }

            if (!string.IsNullOrEmpty(clip.SemanticTags))
            {
                var tags = clip.SemanticTags.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var tag in tags)
                {
                    var trimmedTag = tag.Trim();
                    if (string.IsNullOrEmpty(trimmedTag)) continue;

                    if (!semanticTagCounts.ContainsKey(trimmedTag))
                    {
                        semanticTagCounts[trimmedTag] = 0;
                    }
                    semanticTagCounts[trimmedTag] += weight;
                }
            }

            if (!string.IsNullOrEmpty(clip.MoodTag))
            {
                if (!moodTagCounts.ContainsKey(clip.MoodTag))
                {
                    moodTagCounts[clip.MoodTag] = 0;
                }
                moodTagCounts[clip.MoodTag] += weight;
            }

            totalDwell += interaction.DwellTimeMs;
            count++;
        }

        NormalizePreferences(genreCounts);
        NormalizePreferences(semanticTagCounts);
        NormalizePreferences(moodTagCounts);

        var profile = new UserProfile
        {
            UserId = userId,
            GenrePreferencesJson = JsonSerializer.Serialize(genreCounts),
            SemanticTagPreferencesJson = JsonSerializer.Serialize(semanticTagCounts),
            MoodTagPreferencesJson = JsonSerializer.Serialize(moodTagCounts),
            AvgDwellTimeMs = count > 0 ? (double)totalDwell / count : 0,
            TotalInteractions = interactions.Count,
            LastUpdated = DateTime.UtcNow
        };

        await _interactionRepository.UpdateUserProfileAsync(profile, ct).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, double>> GetUserSemanticTagPreferencesAsync(string userId, CancellationToken ct = default)
    {
        var profile = await _interactionRepository.GetUserProfileAsync(userId, ct).ConfigureAwait(false);
        if (profile is null || string.IsNullOrEmpty(profile.SemanticTagPreferencesJson))
        {
            return new Dictionary<string, double>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(profile.SemanticTagPreferencesJson) ?? new Dictionary<string, double>();
        }
        catch
        {
            return new Dictionary<string, double>();
        }
    }

    public async Task<Dictionary<string, double>> GetUserMoodTagPreferencesAsync(string userId, CancellationToken ct = default)
    {
        var profile = await _interactionRepository.GetUserProfileAsync(userId, ct).ConfigureAwait(false);
        if (profile is null || string.IsNullOrEmpty(profile.MoodTagPreferencesJson))
        {
            return new Dictionary<string, double>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, double>>(profile.MoodTagPreferencesJson) ?? new Dictionary<string, double>();
        }
        catch
        {
            return new Dictionary<string, double>();
        }
    }

    private static void NormalizePreferences(Dictionary<string, double> preferences)
    {
        var totalWeight = preferences.Values.Sum(Math.Abs);
        if (totalWeight > 0)
        {
            foreach (var key in preferences.Keys.ToList())
            {
                preferences[key] = Math.Max(0, preferences[key]) / totalWeight;
            }
        }
    }

    private double CalculateScore(Clip clip, Dictionary<string, double> genrePrefs, 
        Dictionary<string, double> semanticTagPrefs, Dictionary<string, double> moodTagPrefs, 
        HashSet<string> likedClipIds)
    {
        double score = 0;

        if (clip.Genre is not null && genrePrefs.TryGetValue(clip.Genre, out var genreWeight))
        {
            score += genreWeight * _weights.GenrePreference;
        }

        if (!string.IsNullOrEmpty(clip.SemanticTags))
        {
            var tags = clip.SemanticTags.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var tagScore = 0.0;
            var matchedTags = 0;

            foreach (var tag in tags)
            {
                var trimmedTag = tag.Trim();
                if (!string.IsNullOrEmpty(trimmedTag) && semanticTagPrefs.TryGetValue(trimmedTag, out var tagWeight))
                {
                    tagScore += tagWeight;
                    matchedTags++;
                }
            }

            if (matchedTags > 0)
            {
                score += (tagScore / matchedTags) * _weights.SemanticTagPreference;
            }
        }

        if (!string.IsNullOrEmpty(clip.MoodTag) && moodTagPrefs.TryGetValue(clip.MoodTag, out var moodWeight))
        {
            score += moodWeight * _weights.MoodTagPreference;
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
