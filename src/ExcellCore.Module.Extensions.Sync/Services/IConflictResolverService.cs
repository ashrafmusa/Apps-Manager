using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Module.Extensions.Sync.Models;

namespace ExcellCore.Module.Extensions.Sync.Services;

public interface IConflictResolverService
{
    Task<ConflictResolutionResult> ResolveAsync(SyncDelta delta, CancellationToken cancellationToken = default);
}

public sealed class ConflictResolutionResult
{
    public ConflictResolutionResult(ConflictResolutionOutcome outcome, SyncDelta delta)
    {
        Outcome = outcome;
        Delta = delta;
    }

    public ConflictResolutionOutcome Outcome { get; }
    public SyncDelta Delta { get; }
}

public enum ConflictResolutionOutcome
{
    Applied,
    Deferred,
    Unhandled
}
