using System;

namespace ExcellCore.Domain.Entities;

public sealed class DispenseEvent
{
    public Guid DispenseEventId { get; set; }
    public Guid MedicationOrderId { get; set; }
    public string? InventoryItem { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime DispensedOnUtc { get; set; }
    public string? DispensedBy { get; set; }
    public string? Location { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
