using System;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Module.Extensions.Sync.Models;

namespace ExcellCore.Module.Extensions.Sync.Services;

public interface ISyncTransportAdapter
{
    Task<string> ExportAsync(DateTime sinceUtc, CancellationToken cancellationToken = default);
    Task<SyncTransportResult> ImportAsync(string payload, CancellationToken cancellationToken = default);
}
