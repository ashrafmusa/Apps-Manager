using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Module.Extensions.Sync.Models;

namespace ExcellCore.Module.Extensions.Sync.Services;

public interface IDeltaSyncProvider
{
    Task<IReadOnlyList<SyncDelta>> CaptureLocalChangesAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task ApplyIncomingDeltasAsync(IEnumerable<SyncDelta> deltas, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncTriageItem>> GetTriageAsync(int take = 50, CancellationToken cancellationToken = default);
}
