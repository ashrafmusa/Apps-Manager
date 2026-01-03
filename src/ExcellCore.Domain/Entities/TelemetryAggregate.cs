using System;

namespace ExcellCore.Domain.Entities;

public sealed class TelemetryAggregate
{
    public Guid TelemetryAggregateId { get; set; } = Guid.NewGuid();
    public string MetricKey { get; set; } = string.Empty;
    public DateTime PeriodStartUtc { get; set; } = DateTime.UtcNow;
    public DateTime PeriodEndUtc { get; set; } = DateTime.UtcNow;
    public int SampleCount { get; set; }
    public double AverageDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
    public double P95DurationMs { get; set; }
    public int WarningCount { get; set; }
    public int CriticalCount { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
