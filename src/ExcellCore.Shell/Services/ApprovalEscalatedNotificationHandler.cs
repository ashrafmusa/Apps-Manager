using System;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Events;

namespace ExcellCore.Shell.Services;

public sealed class ApprovalEscalatedNotificationHandler : IAppEventHandler<ApprovalEscalatedEvent>
{
    private readonly INotificationCenter _notificationCenter;

    public ApprovalEscalatedNotificationHandler(INotificationCenter notificationCenter)
    {
        _notificationCenter = notificationCenter ?? throw new ArgumentNullException(nameof(notificationCenter));
    }

    public Task HandleAsync(ApprovalEscalatedEvent appEvent, CancellationToken cancellationToken = default)
    {
        if (appEvent is null)
        {
            throw new ArgumentNullException(nameof(appEvent));
        }

        var message = FormattableString.Invariant($"Approval request for {appEvent.Approver} is escalated. Requested {appEvent.RequestedOnUtc:yyyy-MM-dd HH:mm} UTC (Agreement {appEvent.AgreementId}).");

        _notificationCenter.Publish(message, NotificationSeverity.Warning);
        return Task.CompletedTask;
    }
}
