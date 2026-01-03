using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ExcellCore.Shell.Services;

public sealed class InventoryAnomalyWorker : IDisposable
{
    private readonly IInventoryAnalyticsService _analyticsService;
    private readonly INotificationCenter _notificationCenter;
    private readonly ILogger<InventoryAnomalyWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly object _signalsGate = new();
    private Task? _worker;
    private IReadOnlyList<InventoryAnomalySignalDto> _latestSignals = Array.Empty<InventoryAnomalySignalDto>();

    public InventoryAnomalyWorker(IInventoryAnalyticsService analyticsService, INotificationCenter notificationCenter, ILogger<InventoryAnomalyWorker> logger)
        : this(analyticsService, notificationCenter, logger, TimeSpan.FromMinutes(10))
    {
    }

    internal InventoryAnomalyWorker(IInventoryAnalyticsService analyticsService, INotificationCenter notificationCenter, ILogger<InventoryAnomalyWorker> logger, TimeSpan interval)
    {
        _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        _notificationCenter = notificationCenter ?? throw new ArgumentNullException(nameof(notificationCenter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = interval;
    }

    public IReadOnlyList<InventoryAnomalySignalDto> LatestSignals
    {
        get
        {
            lock (_signalsGate)
            {
                return _latestSignals;
            }
        }
    }

    public void Start()
    {
        if (_worker is not null)
        {
            return;
        }

        _worker = Task.Run(RunAsync, CancellationToken.None);
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
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inventory anomaly worker terminated unexpectedly.");
        }
        finally
        {
            timer.Dispose();
        }
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        InventoryAnomalyResultDto? result = null;

        try
        {
            result = await _analyticsService.AnalyzeAsync(null, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Inventory anomaly analysis failed.");
        }

        if (result is null)
        {
            return;
        }

        UpdateSignals(result.Signals);
        PublishNotifications(result.Signals);
    }

    private void UpdateSignals(IReadOnlyList<InventoryAnomalySignalDto> signals)
    {
        lock (_signalsGate)
        {
            _latestSignals = signals;
        }
    }

    private void PublishNotifications(IEnumerable<InventoryAnomalySignalDto> signals)
    {
        foreach (var signal in signals.Take(5))
        {
            var severity = signal.Severity switch
            {
                "Critical" => NotificationSeverity.Error,
                "Warning" => NotificationSeverity.Warning,
                _ => NotificationSeverity.Info
            };

            var message = FormattableString.Invariant($"Inventory {signal.SignalType}: {signal.ItemName} @ {signal.Location} → {signal.Message}");
            _notificationCenter.Publish(message, severity);
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();

        if (_worker is not null)
        {
            try
            {
                _worker.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
            {
                // expected during cancellation
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error while waiting for inventory anomaly worker shutdown.");
            }
        }

        _cancellationTokenSource.Dispose();
    }
}
