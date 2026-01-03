using System;

namespace ExcellCore.Domain.Entities;

public sealed class RetailSuspendedTransaction
{
    public Guid RetailSuspendedTransactionId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SuspendedOnUtc { get; set; }
    public DateTime? ResumedOnUtc { get; set; }
    public string? Notes { get; set; }
    public string? PayloadJson { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
