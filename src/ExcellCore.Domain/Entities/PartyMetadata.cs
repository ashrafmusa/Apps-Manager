using System;

namespace ExcellCore.Domain.Entities;

public sealed class PartyMetadata
{
    public Guid PartyMetadataId { get; set; }
    public Guid PartyId { get; set; }
    public string Context { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
