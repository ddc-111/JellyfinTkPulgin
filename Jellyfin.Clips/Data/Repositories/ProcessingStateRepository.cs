using Microsoft.EntityFrameworkCore;
using Jellyfin.Clips.Data.Entities;

namespace Jellyfin.Clips.Data.Repositories;

public interface IProcessingStateRepository
{
    Task<ProcessingState?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<ProcessingState?> GetBySourceItemIdAsync(string sourceItemId, CancellationToken ct = default);
    Task<IReadOnlyList<ProcessingState>> GetInterruptedTasksAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ProcessingState>> GetPendingTasksAsync(CancellationToken ct = default);
    Task<ProcessingState> CreateAsync(ProcessingState state, CancellationToken ct = default);
    Task UpdateAsync(ProcessingState state, CancellationToken ct = default);
    Task MarkInterruptedAsync(CancellationToken ct = default);
    Task CleanupOldStatesAsync(TimeSpan maxAge, CancellationToken ct = default);
}

public class ProcessingStateRepository : IProcessingStateRepository
{
    private readonly IDbContextFactory<ClipsDbContext> _dbFactory;

    public ProcessingStateRepository(IDbContextFactory<ClipsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<ProcessingState?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.ProcessingStates.FindAsync([id], ct).ConfigureAwait(false);
    }

    public async Task<ProcessingState?> GetBySourceItemIdAsync(string sourceItemId, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.ProcessingStates
            .Where(p => p.SourceItemId == sourceItemId && 
                   (p.Status == ProcessingStatus.InProgress || p.Status == ProcessingStatus.Pending))
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessingState>> GetInterruptedTasksAsync(CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.ProcessingStates
            .Where(p => p.Status == ProcessingStatus.InProgress || p.Status == ProcessingStatus.Interrupted)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProcessingState>> GetPendingTasksAsync(CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.ProcessingStates
            .Where(p => p.Status == ProcessingStatus.Pending)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<ProcessingState> CreateAsync(ProcessingState state, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.ProcessingStates.Add(state);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return state;
    }

    public async Task UpdateAsync(ProcessingState state, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        state.UpdatedAt = DateTime.UtcNow;
        db.ProcessingStates.Update(state);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task MarkInterruptedAsync(CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var inProgress = await db.ProcessingStates
            .Where(p => p.Status == ProcessingStatus.InProgress)
            .ToListAsync(ct).ConfigureAwait(false);

        foreach (var state in inProgress)
        {
            state.Status = ProcessingStatus.Interrupted;
            state.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task CleanupOldStatesAsync(TimeSpan maxAge, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var cutoff = DateTime.UtcNow - maxAge;
        var oldStates = await db.ProcessingStates
            .Where(p => p.UpdatedAt < cutoff && 
                   (p.Status == ProcessingStatus.Completed || 
                    p.Status == ProcessingStatus.Failed || 
                    p.Status == ProcessingStatus.Cancelled))
            .ToListAsync(ct).ConfigureAwait(false);

        db.ProcessingStates.RemoveRange(oldStates);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
