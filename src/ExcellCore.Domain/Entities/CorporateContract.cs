using System;

namespace ExcellCore.Domain.Entities;

public sealed class CorporateContract
{
    public Guid CorporateContractId { get; set; }
    public string ContractCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal ContractValue { get; set; }
    public DateTime RenewalDate { get; set; } = DateTime.UtcNow.Date;
    public string Category { get; set; } = "New";
    public string Program { get; set; } = string.Empty;
    public decimal AllocationRatio { get; set; }
    public string AllocationStatus { get; set; } = "On track";
    public AuditTrail Audit { get; set; } = new();
}
