using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Entities;

public sealed class Agreement
{
    public Guid AgreementId { get; set; }
    public string AgreementName { get; set; } = string.Empty;
    public string PayerName { get; set; } = string.Empty;
    public string CoverageType { get; set; } = string.Empty;
    public string Status { get; set; } = AgreementStatus.Draft;
    public bool RequiresApproval { get; set; }
    public bool AutoRenew { get; set; }
    public DateOnly EffectiveFrom { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    public DateOnly? EffectiveTo { get; set; }
    public DateOnly? RenewalDate { get; set; }
    public DateOnly? LastRenewedOn { get; set; }
    public ICollection<AgreementRate> Rates { get; set; } = new List<AgreementRate>();
    public ICollection<AgreementApproval> Approvals { get; set; } = new List<AgreementApproval>();
    public ICollection<AgreementImpactedParty> ImpactedParties { get; set; } = new List<AgreementImpactedParty>();
    public AuditTrail Audit { get; set; } = new();
}

public static class AgreementStatus
{
    public const string Draft = "Draft";
    public const string PendingApproval = "Pending Approval";
    public const string Approved = "Approved";
    public const string Active = "Active";
    public const string Expired = "Expired";
}
