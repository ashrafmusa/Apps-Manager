using System;

namespace ExcellCore.Domain.Entities;

public sealed class AuditTrail
{
    public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public DateTime? ModifiedOnUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public string SourceModule { get; set; } = "core";

    public static AuditTrail ForCreate(string createdBy, string sourceModule = "core") => new()
    {
        CreatedOnUtc = DateTime.UtcNow,
        CreatedBy = createdBy,
        SourceModule = sourceModule
    };

    public AuditTrail Update(string modifiedBy, string sourceModule = "core") => new()
    {
        CreatedOnUtc = CreatedOnUtc,
        CreatedBy = CreatedBy,
        ModifiedOnUtc = DateTime.UtcNow,
        ModifiedBy = modifiedBy,
        SourceModule = sourceModule
    };
}
