using System;

namespace ExcellCore.Domain.Entities;

public sealed class SyncChangeLedgerEntry
{
    public Guid SyncChangeLedgerEntryId { get; set; }
    public string AggregateType { get; set; } = string.Empty;
    public Guid AggregateId { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string OriginSiteId { get; set; } = string.Empty;
    public string OriginDeviceId { get; set; } = string.Empty;
    public DateTime ObservedOnUtc { get; set; } = DateTime.UtcNow;
    public string VectorClockJson { get; set; } = string.Empty;
    public AuditTrail Audit { get; set; } = new();
}
