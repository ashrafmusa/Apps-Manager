using System;

namespace ExcellCore.Domain.Entities;

public sealed class AgreementApproval
{
    public Guid AgreementApprovalId { get; set; }
    public Guid AgreementId { get; set; }
    public string Approver { get; set; } = string.Empty;
    public string Decision { get; set; } = ApprovalDecision.Pending;
    public string? Comments { get; set; }
    public DateTime RequestedOnUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedOnUtc { get; set; }
    public DateTime? EscalatedOnUtc { get; set; }
    public AuditTrail Audit { get; set; } = new();
}

public static class ApprovalDecision
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
