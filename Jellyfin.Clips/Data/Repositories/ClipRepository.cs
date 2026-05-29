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
    private readonly ClipsDbContext _db;

    public ClipRepository(ClipsDbContext db)
    {
        _db = db;
    }

    public async Task<Clip?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _db.Clips.FindAsync([id], ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetBySourceItemIdAsync(string sourceItemId, CancellationToken ct = default)
    {
        return await _db.Clips
            .Where(c => c.SourceItemId == sourceItemId)
            .OrderByDescending(c => c.SceneScore)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetRandomClipsAsync(int count, CancellationToken ct = default)
    {
        return await _db.Clips
            .Where(c => c.IsProcessed)
            .OrderBy(_ => EF.Functions.Random())
            .Take(count)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetClipsByGenreAsync(string genre, int count, CancellationToken ct = default)
    {
        return await _db.Clips
            .Where(c => c.IsProcessed && c.Genre == genre)
            .OrderByDescending(c => c.SceneScore)
            .Take(count)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetRecentClipsAsync(int count, CancellationToken ct = default)
    {
        return await _db.Clips
            .Where(c => c.IsProcessed)
            .OrderByDescending(c => c.CreatedAt)
            .Take(count)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetUnprocessedClipsAsync(CancellationToken ct = default)
    {
        return await _db.Clips
            .Where(c => !c.IsProcessed)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Clips.ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task AddAsync(Clip clip, CancellationToken ct = default)
    {
        _db.Clips.Add(clip);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Clip clip, CancellationToken ct = default)
    {
        _db.Clips.Update(clip);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var clip = await _db.Clips.FindAsync([id], ct).ConfigureAwait(false);
        if (clip is not null)
        {
            _db.Clips.Remove(clip);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        return await _db.Clips.CountAsync(ct).ConfigureAwait(false);
    }

    public async Task<long> GetTotalFileSizeAsync(CancellationToken ct = default)
    {
        return await _db.Clips.SumAsync(c => c.FileSizeBytes, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Clip>> GetClipsForSourceItemsAsync(
        IEnumerable<string> sourceItemIds, CancellationToken ct = default)
    {
        var ids = sourceItemIds.ToList();
        return await _db.Clips
            .Where(c => ids.Contains(c.SourceItemId) && c.IsProcessed)
            .OrderByDescending(c => c.SceneScore)
            .ToListAsync(ct).ConfigureAwait(false);
    }
}
