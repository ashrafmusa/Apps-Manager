using System;

namespace ExcellCore.Domain.Entities;

public sealed class AgreementRate
{
    public Guid AgreementRateId { get; set; }
    public Guid AgreementId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public decimal BaseAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal? CopayPercent { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
