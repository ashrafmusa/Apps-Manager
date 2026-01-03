using System;

namespace ExcellCore.Domain.Entities;

public sealed class AgreementImpactedParty
{
    public Guid AgreementImpactedPartyId { get; set; }
    public Guid AgreementId { get; set; }
    public Guid PartyId { get; set; }
    public string Relationship { get; set; } = string.Empty;
    public AuditTrail Audit { get; set; } = new();
}
