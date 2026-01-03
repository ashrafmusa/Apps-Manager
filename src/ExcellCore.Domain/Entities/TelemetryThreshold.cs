using System;

namespace ExcellCore.Domain.Entities;

public sealed class TelemetryThreshold
{
    public Guid TelemetryThresholdId { get; set; } = Guid.NewGuid();
    public string MetricKey { get; set; } = string.Empty;
    public double WarningThresholdMs { get; set; }
    public double CriticalThresholdMs { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
