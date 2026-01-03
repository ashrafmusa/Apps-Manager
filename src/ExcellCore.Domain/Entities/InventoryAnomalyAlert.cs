using System;

namespace ExcellCore.Domain.Entities;

public sealed class InventoryAnomalyAlert
{
    public Guid InventoryAnomalyAlertId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string SignalType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public double VelocityPerDay { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderPoint { get; set; }
    public DateTime DetectedOnUtc { get; set; } = DateTime.UtcNow;
    public DateTime WindowStartUtc { get; set; } = DateTime.UtcNow;
    public DateTime WindowEndUtc { get; set; } = DateTime.UtcNow;
    public AuditTrail Audit { get; set; } = new();
}
