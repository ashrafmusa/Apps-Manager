using System;

namespace ExcellCore.Domain.Entities;

public sealed class TelemetryHealthSnapshot
{
    public Guid TelemetryHealthSnapshotId { get; set; } = Guid.NewGuid();
    public string MetricKey { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public string Message { get; set; } = string.Empty;
    public DateTime CapturedOnUtc { get; set; } = DateTime.UtcNow;
    public int SampleCount { get; set; }
    public int WarningCount { get; set; }
    public int CriticalCount { get; set; }
    public double P95DurationMs { get; set; }
    public double MaxDurationMs { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
