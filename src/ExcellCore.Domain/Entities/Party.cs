using System.Collections.Generic;

namespace ExcellCore.Domain.Entities;

public sealed class Party
{
    public Guid PartyId { get; set; }
    public string PartyType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? NationalId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public ICollection<PartyIdentifier> Identifiers { get; set; } = new List<PartyIdentifier>();
    public AuditTrail Audit { get; set; } = new();
}
