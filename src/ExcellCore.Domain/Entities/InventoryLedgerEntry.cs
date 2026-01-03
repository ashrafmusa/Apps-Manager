using System;

namespace ExcellCore.Domain.Entities;

public sealed class InventoryLedgerEntry
{
    public Guid InventoryLedgerEntryId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal QuantityDelta { get; set; }
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal OnOrder { get; set; }
    public DateTime OccurredOnUtc { get; set; } = DateTime.UtcNow;
    public string? SourceReference { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
