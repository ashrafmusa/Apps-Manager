namespace ExcellCore.Domain.Entities;

public sealed class AuditTrail
{
    public DateTime CreatedOnUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public DateTime? ModifiedOnUtc { get; set; }
    public string? ModifiedBy { get; set; }
    public string SourceModule { get; set; } = "core";
}
