using System;

namespace ExcellCore.Domain.Entities;

public static class ActionLogType
{
    public const string Reminder = "ReminderSent";
    public const string FastTrack = "FastTracked";
}

public sealed class ActionLog
{
    public Guid ActionLogId { get; set; }
    public Guid AgreementId { get; set; }
    public Guid? AgreementApprovalId { get; set; }
    public string ActionType { get; set; } = ActionLogType.Reminder;
    public string PerformedBy { get; set; } = "desktop";
    public DateTime ActionedOnUtc { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public AuditTrail Audit { get; set; } = new();
}
