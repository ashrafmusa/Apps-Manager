using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Entities;

public sealed class RetailTransaction
{
    public Guid RetailTransactionId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Channel { get; set; } = "In-Store";
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Completed";
    public DateTime OccurredOnUtc { get; set; } = DateTime.UtcNow;
    public bool LoyaltyEnrollment { get; set; }
    public AuditTrail Audit { get; set; } = new();
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
