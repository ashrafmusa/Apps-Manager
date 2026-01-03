using System;
using System.Collections.Generic;

namespace ExcellCore.Module.Extensions.Sync.Models;

public sealed class SyncDelta
{
    [System.Text.Json.Serialization.JsonConstructor]
    public SyncDelta(string aggregateType, Guid aggregateId, IReadOnlyList<SyncFieldChange> changes, VectorClockStamp vectorClock, SyncOrigin origin)
    {
        if (string.IsNullOrWhiteSpace(aggregateType))
        {
            throw new ArgumentException("Aggregate type is required.", nameof(aggregateType));
        }

        AggregateType = aggregateType;
        AggregateId = aggregateId;
        Changes = changes ?? Array.Empty<SyncFieldChange>();
        VectorClock = vectorClock ?? throw new ArgumentNullException(nameof(vectorClock));
        Origin = origin ?? throw new ArgumentNullException(nameof(origin));
    }

    public string AggregateType { get; }
    public Guid AggregateId { get; }
    public IReadOnlyList<SyncFieldChange> Changes { get; }
    public VectorClockStamp VectorClock { get; }
    public SyncOrigin Origin { get; }
}
