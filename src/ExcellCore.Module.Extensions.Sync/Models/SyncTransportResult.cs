using System;

namespace ExcellCore.Module.Extensions.Sync.Models;

public sealed record SyncTransportResult(
    int AppliedCount,
    int TriagedCount,
    DateTime ReceivedOnUtc,
    string? SourceSiteId,
    string? SourceDeviceId,
    string StatusMessage);
