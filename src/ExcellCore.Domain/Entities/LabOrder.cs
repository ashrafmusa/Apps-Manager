using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Entities;

public sealed class LabOrder
{
    public Guid LabOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string TestCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? ExternalSystem { get; set; }
    public string? ExternalOrderId { get; set; }
    public Guid? PartyId { get; set; }
    public string? OrderingProvider { get; set; }
    public string? Notes { get; set; }
    public DateTime OrderedOnUtc { get; set; } = DateTime.UtcNow;
    public List<OrderResult> Results { get; set; } = new();
    public AuditTrail Audit { get; set; } = AuditTrail.ForCreate("system", "orders");
}
