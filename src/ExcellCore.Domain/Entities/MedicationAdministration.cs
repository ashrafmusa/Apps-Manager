using System;

namespace ExcellCore.Domain.Entities;

public sealed class MedicationAdministration
{
    public Guid MedicationAdministrationId { get; set; }
    public Guid MedicationOrderId { get; set; }
    public DateTime ScheduledForUtc { get; set; }
    public DateTime? AdministeredOnUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? PerformedBy { get; set; }
    public string? Notes { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
