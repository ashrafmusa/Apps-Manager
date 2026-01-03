using System;
using System.Text.Json.Serialization;

namespace ExcellCore.Domain.Entities;

public sealed class Bed
{
    public Guid BedId { get; set; }
    public Guid RoomId { get; set; }
    public string BedNumber { get; set; } = string.Empty;
    public string? Status { get; set; }
    public bool IsIsolation { get; set; }
    public Guid? OccupiedByPartyId { get; set; }
    public DateTime? OccupiedOnUtc { get; set; }
    public Room Room { get; set; } = null!;

    [JsonConstructor]
    public Bed()
    {
    }

    public AuditTrail Audit { get; set; } = AuditTrail.ForCreate("system");
}
