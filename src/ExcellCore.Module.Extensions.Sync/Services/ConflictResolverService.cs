using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Data;
using ExcellCore.Module.Extensions.Sync.Models;
using Microsoft.EntityFrameworkCore;

namespace ExcellCore.Module.Extensions.Sync.Services;

public sealed class ConflictResolverService : IConflictResolverService
{
    private readonly IDbContextFactory<ExcellCoreContext> _contextFactory;

    public ConflictResolverService(IDbContextFactory<ExcellCoreContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public async Task<ConflictResolutionResult> ResolveAsync(SyncDelta delta, CancellationToken cancellationToken = default)
    {
        if (delta is null)
        {
            throw new ArgumentNullException(nameof(delta));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await context.EnsureDatabaseMigratedAsync(cancellationToken).ConfigureAwait(false);

        var existingState = await context.SyncStates
            .AsNoTracking()
            .Where(s => s.AggregateType == delta.AggregateType && s.AggregateId == delta.AggregateId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        var currentClock = existingState is null
            ? VectorClockStamp.Empty
            : VectorClockSerializer.Deserialize(existingState.VectorClockJson);

        var comparison = delta.VectorClock.Compare(currentClock);
        if (comparison == VectorClockComparison.Dominated)
        {
            return new ConflictResolutionResult(ConflictResolutionOutcome.Deferred, delta);
        }

        var merged = currentClock.Merge(delta.VectorClock);

        if (existingState is null)
        {
            context.SyncStates.Add(new Domain.Entities.SyncState
            {
                SyncStateId = Guid.NewGuid(),
                AggregateType = delta.AggregateType,
                AggregateId = delta.AggregateId,
                VectorClockJson = VectorClockSerializer.Serialize(merged),
                UpdatedOnUtc = DateTime.UtcNow,
                Audit = new Domain.Entities.AuditTrail
                {
                    CreatedBy = "sync",
                    SourceModule = "Extensions.Sync"
                }
            });
        }
        else
        {
            var tracked = await context.SyncStates.FirstAsync(s => s.SyncStateId == existingState.SyncStateId, cancellationToken).ConfigureAwait(false);
            tracked.VectorClockJson = VectorClockSerializer.Serialize(merged);
            tracked.UpdatedOnUtc = DateTime.UtcNow;
            tracked.Audit ??= new Domain.Entities.AuditTrail();
            tracked.Audit.ModifiedBy = "sync";
            tracked.Audit.ModifiedOnUtc = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new ConflictResolutionResult(ConflictResolutionOutcome.Applied, delta);
    }
}
