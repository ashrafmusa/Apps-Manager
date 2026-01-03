using System;

namespace ExcellCore.Domain.Events;

public sealed record ApprovalRequiredEvent(Guid AgreementId, string Approver);

public sealed record ApprovalEscalatedEvent(Guid AgreementId, string Approver, DateTime RequestedOnUtc);

public sealed record ApprovalReminderEvent(Guid AgreementId, Guid AgreementApprovalId, string Approver, DateTime RequestedOnUtc, DateTime ReminderSentUtc);
