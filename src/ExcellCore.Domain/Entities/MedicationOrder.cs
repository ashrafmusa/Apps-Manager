using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Entities;

public sealed class MedicationOrder
{
    public Guid MedicationOrderId { get; set; }
    public Guid PartyId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Medication { get; set; } = string.Empty;
    public string Dose { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string OrderingProvider { get; set; } = string.Empty;
    public DateTime OrderedOnUtc { get; set; }
    public DateTime? StartOnUtc { get; set; }
    public DateTime? EndOnUtc { get; set; }
    public string? Notes { get; set; }
    public ICollection<MedicationAdministration> Administrations { get; set; } = new List<MedicationAdministration>();
    public ICollection<DispenseEvent> DispenseEvents { get; set; } = new List<DispenseEvent>();
    public AuditTrail Audit { get; set; } = new();
}
