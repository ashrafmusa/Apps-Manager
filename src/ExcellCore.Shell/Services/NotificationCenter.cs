using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace ExcellCore.Shell.Services;

public enum NotificationSeverity
{
    Info,
    Warning,
    Error
}

public sealed record NotificationMessage(DateTime TimestampUtc, string Message, NotificationSeverity Severity);

public interface INotificationCenter
{
    ReadOnlyObservableCollection<NotificationMessage> Notifications { get; }
    void Publish(NotificationMessage notification);
    void Publish(string message, NotificationSeverity severity = NotificationSeverity.Info);
}

public sealed class NotificationCenter : INotificationCenter
{
    private readonly ObservableCollection<NotificationMessage> _notifications = new();
    private readonly ReadOnlyObservableCollection<NotificationMessage> _readOnly;
    private const int MaxNotifications = 25;

    public NotificationCenter()
    {
        _readOnly = new ReadOnlyObservableCollection<NotificationMessage>(_notifications);
    }

    public ReadOnlyObservableCollection<NotificationMessage> Notifications => _readOnly;

    public void Publish(NotificationMessage notification)
    {
        if (notification is null)
        {
            throw new ArgumentNullException(nameof(notification));
        }

        void PublishCore()
        {
            _notifications.Insert(0, notification);
            if (_notifications.Count > MaxNotifications)
            {
                _notifications.RemoveAt(_notifications.Count - 1);
            }
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(PublishCore);
        }
        else
        {
            PublishCore();
        }
    }

    public void Publish(string message, NotificationSeverity severity = NotificationSeverity.Info)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Publish(new NotificationMessage(DateTime.UtcNow, message.Trim(), severity));
    }
}
