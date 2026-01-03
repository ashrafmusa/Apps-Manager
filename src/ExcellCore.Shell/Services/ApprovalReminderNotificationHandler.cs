using System;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Events;

namespace ExcellCore.Shell.Services;

public sealed class ApprovalReminderNotificationHandler : IAppEventHandler<ApprovalReminderEvent>
{
    private readonly INotificationCenter _notificationCenter;

    public ApprovalReminderNotificationHandler(INotificationCenter notificationCenter)
    {
        _notificationCenter = notificationCenter ?? throw new ArgumentNullException(nameof(notificationCenter));
    }

    public Task HandleAsync(ApprovalReminderEvent appEvent, CancellationToken cancellationToken = default)
    {
        if (appEvent is null)
        {
            throw new ArgumentNullException(nameof(appEvent));
        }

        var message = FormattableString.Invariant($"Reminder sent to {appEvent.Approver} for agreement {appEvent.AgreementId}. Original request {appEvent.RequestedOnUtc:yyyy-MM-dd HH:mm} UTC.");
        _notificationCenter.Publish(message, NotificationSeverity.Info);
        return Task.CompletedTask;
    }
}
