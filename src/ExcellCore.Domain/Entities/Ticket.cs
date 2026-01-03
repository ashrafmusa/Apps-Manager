using System;

namespace ExcellCore.Domain.Entities;

public sealed class Ticket
{
    public Guid TicketId { get; set; }
    public Guid RetailTransactionId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string Channel { get; set; } = "In-Store";
    public DateTime RaisedOnUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedOnUtc { get; set; }
    public RetailTransaction? RetailTransaction { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
