using System;
using System.Collections.Generic;

namespace ExcellCore.Domain.Entities;

public sealed class Ward
{
    public Guid WardId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public List<Room> Rooms { get; set; } = new();
    public AuditTrail Audit { get; set; } = AuditTrail.ForCreate("system");
}
