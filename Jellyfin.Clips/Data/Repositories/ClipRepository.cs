using Microsoft.EntityFrameworkCore;
using Jellyfin.Clips.Data.Entities;

namespace Jellyfin.Clips.Data.Repositories;

public interface IClipRepository
{
    Task<Clip?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<Clip>> GetBySourceItemIdAsync(string sourceItemId, CancellationToken ct = default);
    Task<IReadOnlyList<Clip>> GetRandomClipsAsync(int count, CancellationToken ct = default);
    Task<IReadOnlyList<Clip>> GetClipsByGenreAsync(string genre, int count, CancellationToken ct = default);
    Task<IReadOnlyList<Clip>> GetRecentClipsAsync(int count, CancellationToken ct = default);
    Task<IReadOnlyList<Clip>> GetUnprocessedClipsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Clip>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Clip clip, CancellationToken ct = default);
    Task UpdateAsync(Clip clip, CancellationToken ct = default);
    Task DeleteAsync(string id, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
    Task<long> GetTotalFileSizeAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Clip>> GetClipsForSourceItemsAsync(IEnumerable<string> sourceItemIds, CancellationToken ct = default);
}

public class ClipRepository : IClipRepository
{
    private readonly IDbContextFactory<ClipsDbContext> _dbFactory;

    public ClipRepository(IDbContextFactory<ClipsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<Clip?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Clips.FindAsync([id], ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetBySourceItemIdAsync(string sourceItemId, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Clips
            .Where(c => c.SourceItemId == sourceItemId)
            .OrderByDescending(c => c.SceneScore)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetRandomClipsAsync(int count, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Clips
            .Where(c => c.IsProcessed)
            .OrderBy(_ => EF.Functions.Random())
            .Take(count)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetClipsByGenreAsync(string genre, int count, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Clips
            .Where(c => c.IsProcessed && c.Genre == genre)
            .OrderByDescending(c => c.SceneScore)
            .Take(count)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetRecentClipsAsync(int count, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Clips
            .Where(c => c.IsProcessed)
            .OrderByDescending(c => c.CreatedAt)
            .Take(count)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetUnprocessedClipsAsync(CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Clips
            .Where(c => !c.IsProcessed)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetAllAsync(CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Clips.ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task AddAsync(Clip clip, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Clips.Add(clip);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Clip clip, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Clips.Update(clip);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var clip = await db.Clips.FindAsync([id], ct).ConfigureAwait(false);
        if (clip is not null)
        {
            db.Clips.Remove(clip);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Clips.CountAsync(ct).ConfigureAwait(false);
    }

    public async Task<long> GetTotalFileSizeAsync(CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Clips.SumAsync(c => c.FileSizeBytes, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetClipsForSourceItemsAsync(
        IEnumerable<string> sourceItemIds, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var ids = sourceItemIds.ToList();
        return await db.Clips
            .Where(c => ids.Contains(c.SourceItemId) && c.IsProcessed)
            .OrderByDescending(c => c.SceneScore)
            .ToListAsync(ct).ConfigureAwait(false);
    }
}
