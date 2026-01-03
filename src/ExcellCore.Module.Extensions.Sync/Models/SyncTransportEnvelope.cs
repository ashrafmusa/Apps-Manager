using System;
using System.Collections.Generic;

namespace ExcellCore.Module.Extensions.Sync.Models;

public sealed record SyncTransportEnvelope(
    string SourceSiteId,
    string? SourceDeviceId,
    DateTime CreatedOnUtc,
    IReadOnlyList<SyncDelta> Deltas);
