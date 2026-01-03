using System;

namespace ExcellCore.Domain.Entities;

public sealed class RetailReturn
{
    public Guid RetailReturnId { get; set; }
    public Guid? RetailTransactionId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime ReturnedOnUtc { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
