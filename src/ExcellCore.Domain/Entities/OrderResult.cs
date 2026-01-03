using System;

namespace ExcellCore.Domain.Entities;

public sealed class OrderResult
{
    public Guid OrderResultId { get; set; }
    public Guid LabOrderId { get; set; }
    public string ResultCode { get; set; } = string.Empty;
    public string ResultValue { get; set; } = string.Empty;
    public string ResultStatus { get; set; } = "Preliminary";
    public string? Units { get; set; }
    public string? ReferenceRange { get; set; }
    public DateTime CollectedOnUtc { get; set; } = DateTime.UtcNow;
    public string? PerformedBy { get; set; }
    public LabOrder LabOrder { get; set; } = null!;
    public AuditTrail Audit { get; set; } = AuditTrail.ForCreate("system", "orders");
}
