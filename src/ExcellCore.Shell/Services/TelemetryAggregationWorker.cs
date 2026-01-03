using System;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;
using Microsoft.Extensions.Logging;

namespace ExcellCore.Shell.Services;

public sealed class TelemetryAggregationWorker : IDisposable
{
    private readonly ITelemetryService _telemetryService;
    private readonly ILogger<TelemetryAggregationWorker> _logger;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly object _healthGate = new();
    private Task? _worker;
    private TelemetryHealthSnapshotDto? _currentHealth;

    public TelemetryAggregationWorker(ITelemetryService telemetryService, ILogger<TelemetryAggregationWorker> logger)
        : this(telemetryService, logger, TimeSpan.FromMinutes(5))
    {
    }

    internal TelemetryAggregationWorker(ITelemetryService telemetryService, ILogger<TelemetryAggregationWorker> logger, TimeSpan interval)
    {
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = interval;
    }

    public event EventHandler<TelemetryHealthChangedEventArgs>? HealthChanged;

    public TelemetryHealthSnapshotDto? CurrentHealth
    {
        get
        {
            lock (_healthGate)
            {
                return _currentHealth;
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
            await ExecuteAggregationAsync(token).ConfigureAwait(false);

            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
            {
                await ExecuteAggregationAsync(token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // graceful shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telemetry aggregation worker terminated unexpectedly.");
        }
        finally
        {
            timer.Dispose();
        }
    }

    private async Task ExecuteAggregationAsync(CancellationToken cancellationToken)
    {
        TelemetryAggregationOutcomeDto? outcome = null;

        try
        {
            outcome = await _telemetryService.AggregateAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telemetry aggregation run failed; falling back to last known health snapshot.");
        }

        TelemetryHealthSnapshotDto? health = outcome?.Health;

        if (health is null)
        {
            try
            {
                health = await _telemetryService.GetLatestHealthAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to retrieve telemetry health snapshot.");
                return;
            }
        }

        UpdateHealth(health);
    }

    private void UpdateHealth(TelemetryHealthSnapshotDto health)
    {
        bool changed;
        lock (_healthGate)
        {
            if (_currentHealth is not null && _currentHealth.Equals(health))
            {
                return;
            }

            _currentHealth = health;
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        HealthChanged?.Invoke(this, new TelemetryHealthChangedEventArgs(health));
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
                _logger.LogDebug(ex, "Error while waiting for telemetry worker shutdown.");
            }
        }

        _cancellationTokenSource.Dispose();
    }
}

public sealed record TelemetryHealthChangedEventArgs(TelemetryHealthSnapshotDto Health);
