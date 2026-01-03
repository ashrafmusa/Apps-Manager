using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ExcellCore.Domain.Entities;

public sealed class Room
{
    public Guid RoomId { get; set; }
    public Guid WardId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public Ward Ward { get; set; } = null!;
    public List<Bed> Beds { get; set; } = new();

    [JsonConstructor]
    public Room()
    {
    }

    public AuditTrail Audit { get; set; } = AuditTrail.ForCreate("system");
}
