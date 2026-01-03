using System;

namespace ExcellCore.Domain.Entities;

public sealed class TelemetryEvent
{
    public Guid TelemetryEventId { get; set; } = Guid.NewGuid();
    public string EventType { get; set; } = "Query";
    public string CommandText { get; set; } = string.Empty;
    public double DurationMilliseconds { get; set; }
    public DateTime OccurredOnUtc { get; set; } = DateTime.UtcNow;
    public AuditTrail Audit { get; set; } = new();
}
