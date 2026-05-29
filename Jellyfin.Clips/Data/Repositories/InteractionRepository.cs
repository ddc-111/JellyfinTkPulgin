using Jellyfin.Clips.Data.Entities;

namespace Jellyfin.Clips.Data.Repositories;

public interface IInteractionRepository
{
    Task RecordInteractionAsync(UserInteraction interaction, CancellationToken ct = default);
    Task<IReadOnlyList<UserInteraction>> GetUserInteractionsAsync(string userId, int limit = 100, CancellationToken ct = default);
    Task<IReadOnlyList<UserInteraction>> GetClipInteractionsAsync(string clipId, CancellationToken ct = default);
    Task<UserProfile?> GetUserProfileAsync(string userId, CancellationToken ct = default);
    Task UpdateUserProfileAsync(UserProfile profile, CancellationToken ct = default);
    Task<Dictionary<string, double>> GetUserGenrePreferencesAsync(string userId, CancellationToken ct = default);
    Task<double> GetAverageCompletionRateAsync(string clipId, CancellationToken ct = default);
    Task<int> GetLikeCountAsync(string clipId, CancellationToken ct = default);
    Task<bool> HasUserLikedClipAsync(string userId, string clipId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetLikedClipIdsAsync(string userId, CancellationToken ct = default);
}

public class InteractionRepository : IInteractionRepository
{
    private readonly ClipsDbContext _db;

    public InteractionRepository(ClipsDbContext db)
    {
        _db = db;
    }

    public async Task RecordInteractionAsync(UserInteraction interaction, CancellationToken ct = default)
    {
        _db.UserInteractions.Add(interaction);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserInteraction>> GetUserInteractionsAsync(
        string userId, int limit = 100, CancellationToken ct = default)
    {
        return await _db.UserInteractions
            .Where(i => i.UserId == userId)
            .OrderByDescending(i => i.Timestamp)
            .Take(limit)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserInteraction>> GetClipInteractionsAsync(
        string clipId, CancellationToken ct = default)
    {
        return await _db.UserInteractions
            .Where(i => i.ClipId == clipId)
            .OrderByDescending(i => i.Timestamp)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<UserProfile?> GetUserProfileAsync(string userId, CancellationToken ct = default)
    {
        return await _db.UserProfiles.FindAsync([userId], ct).ConfigureAwait(false);
    }

    public async Task UpdateUserProfileAsync(UserProfile profile, CancellationToken ct = default)
    {
        var existing = await _db.UserProfiles.FindAsync([profile.UserId], ct).ConfigureAwait(false);
        if (existing is null)
        {
            _db.UserProfiles.Add(profile);
        }
        else
        {
            _db.Entry(existing).CurrentValues.SetValues(profile);
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<Dictionary<string, double>> GetUserGenrePreferencesAsync(
        string userId, CancellationToken ct = default)
    {
        var profile = await GetUserProfileAsync(userId, ct).ConfigureAwait(false);
        if (profile is null || string.IsNullOrEmpty(profile.GenrePreferencesJson))
        {
            return new Dictionary<string, double>();
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(
                       profile.GenrePreferencesJson) ?? new Dictionary<string, double>();
        }
        catch
        {
            return new Dictionary<string, double>();
        }
    }

    public async Task<double> GetAverageCompletionRateAsync(string clipId, CancellationToken ct = default)
    {
        var interactions = await _db.UserInteractions
            .Where(i => i.ClipId == clipId && i.InteractionType == InteractionType.View)
            .ToListAsync(ct).ConfigureAwait(false);

        return interactions.Count == 0 ? 0 : interactions.Average(i => i.CompletionRate);
    }

    public async Task<int> GetLikeCountAsync(string clipId, CancellationToken ct = default)
    {
        return await _db.UserInteractions
            .CountAsync(i => i.ClipId == clipId && i.InteractionType == InteractionType.Like, ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> HasUserLikedClipAsync(string userId, string clipId, CancellationToken ct = default)
    {
        return await _db.UserInteractions
            .AnyAsync(i => i.UserId == userId && i.ClipId == clipId && i.InteractionType == InteractionType.Like, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetLikedClipIdsAsync(string userId, CancellationToken ct = default)
    {
        return await _db.UserInteractions
            .Where(i => i.UserId == userId && i.InteractionType == InteractionType.Like)
            .Select(i => i.ClipId)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);
    }
}
