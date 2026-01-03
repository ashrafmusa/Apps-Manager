using System;

namespace ExcellCore.Domain.Entities;

public sealed class SyncState
{
    public Guid SyncStateId { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string VectorClockJson { get; set; } = string.Empty;
    public DateTime UpdatedOnUtc { get; set; } = DateTime.UtcNow;
    public AuditTrail Audit { get; set; } = new();
}
