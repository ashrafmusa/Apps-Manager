using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Module.Extensions.Sync.Models;

namespace ExcellCore.Module.Extensions.Sync.Services;

public sealed class NoOpConflictResolverService : IConflictResolverService
{
    public Task<ConflictResolutionResult> ResolveAsync(SyncDelta delta, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ConflictResolutionResult(ConflictResolutionOutcome.Unhandled, delta));
    }
}
