using System;

namespace ExcellCore.Domain.Entities;

public sealed class OrderSet
{
    public Guid OrderSetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public string? Description { get; set; }
    public string ItemsJson { get; set; } = "[]";
    public AuditTrail Audit { get; set; } = AuditTrail.ForCreate("system", "orders");
}
