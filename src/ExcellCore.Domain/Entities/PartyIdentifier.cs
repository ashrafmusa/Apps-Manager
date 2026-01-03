namespace ExcellCore.Domain.Entities;

public sealed class PartyIdentifier
{
    public Guid PartyIdentifierId { get; set; }
    public string Scheme { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public Guid PartyId { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
