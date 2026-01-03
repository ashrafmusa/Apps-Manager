using System;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ExcellCore.Shell.Services;

public sealed class AgreementEscalationMonitor : IDisposable
{
    private readonly IAgreementService _agreementService;
    private readonly ILogger<AgreementEscalationMonitor> _logger;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _monitorTask;
    private readonly object _statusGate = new();
    private EscalationMonitorStatus _status = EscalationMonitorStatus.Stopped;
    private string? _lastError;

    public AgreementEscalationMonitor(IAgreementService agreementService, ILogger<AgreementEscalationMonitor> logger)
        : this(agreementService, logger, TimeSpan.FromHours(1))
    {
    }

    internal AgreementEscalationMonitor(IAgreementService agreementService, ILogger<AgreementEscalationMonitor> logger, TimeSpan interval)
    {
        _agreementService = agreementService ?? throw new ArgumentNullException(nameof(agreementService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = interval;
    }

    public event EventHandler<MonitorStatusChangedEventArgs>? StatusChanged;

    public EscalationMonitorStatus Status
    {
        get
        {
            lock (_statusGate)
            {
                return _status;
            }
        }
    }

    public string? LastError
    {
        get
        {
            lock (_statusGate)
            {
                return _lastError;
            }
        }
    }

    public void Start()
    {
        if (_monitorTask is not null)
        {
            return;
        }

        UpdateStatus(EscalationMonitorStatus.Running);
        _monitorTask = Task.Run(RunAsync, CancellationToken.None);
    }

    private async Task RunAsync()
    {
        var token = _cancellationTokenSource.Token;
        var timer = new PeriodicTimer(_interval);

        try
        {
            await ExecuteAsync(token).ConfigureAwait(false);

            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                await ExecuteAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            UpdateStatus(EscalationMonitorStatus.Stopped);
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agreement escalation monitor terminated unexpectedly.");
            UpdateStatus(EscalationMonitorStatus.Faulted, ex.Message);
        }
        finally
        {
            timer.Dispose();
        }
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var escalated = await _agreementService.EscalatePendingApprovalsAsync(cancellationToken).ConfigureAwait(false);
            if (escalated > 0)
            {
                _logger.LogInformation("Escalated {Count} pending approvals.", escalated);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to escalate pending approvals.");
            UpdateStatus(EscalationMonitorStatus.Faulted, ex.Message);
            return;
        }

        UpdateStatus(EscalationMonitorStatus.Running);
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();

        if (_monitorTask is not null)
        {
            try
            {
                _monitorTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // expected during cancellation
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error while waiting for escalation monitor shutdown.");
            }
        }

        _cancellationTokenSource.Dispose();
        UpdateStatus(EscalationMonitorStatus.Stopped);
    }

    private void UpdateStatus(EscalationMonitorStatus status, string? error = null)
    {
        string? normalizedError = string.IsNullOrWhiteSpace(error) ? null : error;

        MonitorStatusChangedEventArgs? args;

        lock (_statusGate)
        {
            if (_status == status && string.Equals(_lastError, normalizedError, StringComparison.Ordinal))
            {
                return;
            }

            _status = status;
            _lastError = normalizedError;
            args = new MonitorStatusChangedEventArgs(status, normalizedError);
        }

        StatusChanged?.Invoke(this, args);
    }
}

public enum EscalationMonitorStatus
{
    Stopped,
    Running,
    Faulted
}

public sealed record MonitorStatusChangedEventArgs(EscalationMonitorStatus Status, string? ErrorMessage);
