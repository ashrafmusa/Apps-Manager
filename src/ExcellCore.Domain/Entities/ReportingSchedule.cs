using System;

namespace ExcellCore.Domain.Entities;

public sealed class ReportingSchedule
{
    public Guid ReportingScheduleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public TimeSpan Cadence { get; set; }
    public DateTime NextRunUtc { get; set; }
    public bool IsEnabled { get; set; } = true;
    public AuditTrail Audit { get; set; } = new();
}
