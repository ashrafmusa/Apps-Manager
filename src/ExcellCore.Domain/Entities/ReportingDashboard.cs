using System;

namespace ExcellCore.Domain.Entities;

public sealed class ReportingDashboard
{
    public Guid ReportingDashboardId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public AuditTrail Audit { get; set; } = new();
}
