using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ExcellCore.Module.Abstractions;
using ExcellCore.Domain.Services;
using ExcellCore.Module.Extensions.Reporting.Commands;

namespace ExcellCore.Module.Extensions.Reporting.ViewModels;

public sealed class TelemetryWorkspaceViewModel : INotifyPropertyChanged
{
    private static readonly TimeSpan SeverityWindow = TimeSpan.FromHours(24);
    private static readonly IReadOnlyDictionary<string, string> DefaultLocalization = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Title"] = "System Telemetry",
        ["Subtitle"] = "Slow-query health and performance trends.",
        ["TrendLabel"] = "Telemetry Trend",
        ["SeverityBreakdownLabel"] = "Severity Breakdown",
        ["RefreshButton"] = "Refresh",
        ["LoadingStatus"] = "Loading telemetry health...",
        ["FailedStatus"] = "Failed to load telemetry: {0}",
        ["UpdatedStatus"] = "Telemetry updated {0}"
    };
    private static readonly IReadOnlyList<string> LocalizationContexts = new[] { "Extensions.Reporting.Telemetry", "Default" };

    private readonly ITelemetryService _telemetryService;
    private readonly ILocalizationService _localizationService;
    private readonly ILocalizationContext _localizationContext;
    private readonly AsyncRelayCommand _refreshCommand;
    private IReadOnlyDictionary<string, string> _localization = DefaultLocalization;

    private bool _initialized;
    private bool _isBusy;
    private TelemetryHealthModel _health = TelemetryHealthModel.Empty;
    private string _statusMessage = DefaultLocalization["LoadingStatus"];

    public TelemetryWorkspaceViewModel(ITelemetryService telemetryService, ILocalizationService localizationService, ILocalizationContext localizationContext)
    {
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _localizationContext = localizationContext ?? throw new ArgumentNullException(nameof(localizationContext));
        Aggregates = new ObservableCollection<TelemetryAggregateItemModel>();
        SeverityBreakdown = new ObservableCollection<TelemetrySeverityBucketModel>();
        _refreshCommand = new AsyncRelayCommand(RefreshAsync, () => !_isBusy);

        _localizationContext.ContextsChanged += OnLocalizationContextsChanged;
        Localization = BuildLocalization(_localizationService, _localizationContext.Contexts);
        _statusMessage = GetLocalization("LoadingStatus", DefaultLocalization["LoadingStatus"]);
    }

    public string Title => GetLocalization("Title", DefaultLocalization["Title"]);
    public string Subtitle => GetLocalization("Subtitle", DefaultLocalization["Subtitle"]);
    public string TrendLabel => GetLocalization("TrendLabel", DefaultLocalization["TrendLabel"]);
    public string SeverityBreakdownLabel => GetLocalization("SeverityBreakdownLabel", DefaultLocalization["SeverityBreakdownLabel"]);
    public string RefreshLabel => GetLocalization("RefreshButton", DefaultLocalization["RefreshButton"]);

    public ObservableCollection<TelemetryAggregateItemModel> Aggregates { get; }
    public ObservableCollection<TelemetrySeverityBucketModel> SeverityBreakdown { get; }

    public IReadOnlyDictionary<string, string> Localization
    {
        get => _localization;
        private set
        {
            if (EqualityComparer<IReadOnlyDictionary<string, string>>.Default.Equals(_localization, value))
            {
                return;
            }

            _localization = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Localization)));
            RaiseLocalizationProperties();
        }
    }

    public TelemetryHealthModel Health
    {
        get => _health;
        private set => SetProperty(ref _health, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand RefreshCommand => _refreshCommand;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await RefreshAsync().ConfigureAwait(false);
        _initialized = true;
    }

    private async Task RefreshAsync()
    {
        if (_isBusy)
        {
            return;
        }

        _isBusy = true;
        _refreshCommand.RaiseCanExecuteChanged();
        StatusMessage = GetLocalization("LoadingStatus", DefaultLocalization["LoadingStatus"]);

        try
        {
            var overview = await _telemetryService.GetOverviewAsync(48).ConfigureAwait(false);
            var severityBuckets = await _telemetryService.GetSeverityBreakdownAsync(SeverityWindow).ConfigureAwait(false);

            UpdateHealth(overview.Health);
            UpdateAggregates(overview.Aggregates);
            UpdateSeverityBuckets(severityBuckets);

            var updatedTemplate = GetLocalization("UpdatedStatus", DefaultLocalization["UpdatedStatus"]);
            StatusMessage = string.Format(CultureInfo.CurrentCulture, updatedTemplate, DateTime.Now.ToString("t", CultureInfo.CurrentCulture));
        }
        catch (Exception ex)
        {
            var failedTemplate = GetLocalization("FailedStatus", DefaultLocalization["FailedStatus"]);
            StatusMessage = string.Format(CultureInfo.CurrentCulture, failedTemplate, ex.Message);
        }
        finally
        {
            _isBusy = false;
            _refreshCommand.RaiseCanExecuteChanged();
        }
    }

    private void UpdateHealth(TelemetryHealthSnapshotDto healthDto)
    {
        var capturedLocal = DateTime.SpecifyKind(healthDto.CapturedOnUtc, DateTimeKind.Utc).ToLocalTime();
        var summary = healthDto.SampleCount == 0
            ? "No events captured in the last window."
            : $"P95 {healthDto.P95DurationMs:F0}ms · Max {healthDto.MaxDurationMs:F0}ms · {healthDto.SampleCount} event(s)";

        Health = new TelemetryHealthModel(
            healthDto.Status,
            healthDto.Message,
            summary,
            capturedLocal);
    }

    private void UpdateAggregates(IReadOnlyList<TelemetryAggregateDto> aggregates)
    {
        Aggregates.Clear();

        foreach (var aggregate in aggregates.OrderBy(a => a.PeriodStartUtc))
        {
            var windowStartLocal = DateTime.SpecifyKind(aggregate.PeriodStartUtc, DateTimeKind.Utc).ToLocalTime();
            var windowEndLocal = DateTime.SpecifyKind(aggregate.PeriodEndUtc, DateTimeKind.Utc).ToLocalTime();

            Aggregates.Add(new TelemetryAggregateItemModel(
                windowStartLocal,
                windowEndLocal,
                aggregate.Severity,
                aggregate.SampleCount,
                aggregate.P95DurationMs,
                aggregate.MaxDurationMs,
                aggregate.WarningCount,
                aggregate.CriticalCount));
        }
    }

    private void UpdateSeverityBuckets(IReadOnlyList<TelemetrySeverityBucketDto> buckets)
    {
        SeverityBreakdown.Clear();

        foreach (var bucket in buckets)
        {
            SeverityBreakdown.Add(new TelemetrySeverityBucketModel(bucket.Severity, bucket.Count));
        }
    }

    private void OnLocalizationContextsChanged(object? sender, EventArgs e)
    {
        Localization = BuildLocalization(_localizationService, _localizationContext.Contexts);
    }

    private static IReadOnlyDictionary<string, string> BuildLocalization(ILocalizationService localizationService, IEnumerable<string> contexts)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var activeContexts = (contexts ?? Array.Empty<string>()).ToList();
        if (activeContexts.Count == 0)
        {
            activeContexts.AddRange(LocalizationContexts);
        }

        foreach (var entry in DefaultLocalization)
        {
            var localized = localizationService?.GetString(entry.Key, activeContexts) ?? entry.Value;
            map[entry.Key] = localized == entry.Key ? entry.Value : localized;
        }

        return map;
    }

    private void RaiseLocalizationProperties()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Subtitle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TrendLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SeverityBreakdownLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RefreshLabel)));
    }

    private string GetLocalization(string key, string fallback)
    {
        return Localization.TryGetValue(key, out var value) ? value : fallback;
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record TelemetryHealthModel(string Severity, string Message, string Summary, DateTime CapturedOnLocal)
{
    public static TelemetryHealthModel Empty { get; } = new("Unknown", "Telemetry is initializing.", "Awaiting snapshot.", DateTime.MinValue);
    public string CapturedDisplay => CapturedOnLocal == DateTime.MinValue ? "--" : CapturedOnLocal.ToString("g");
}

public sealed record TelemetryAggregateItemModel(
    DateTime PeriodStartLocal,
    DateTime PeriodEndLocal,
    string Severity,
    int SampleCount,
    double P95DurationMs,
    double MaxDurationMs,
    int WarningCount,
    int CriticalCount)
{
    public string WindowLabel => $"{PeriodStartLocal:t} - {PeriodEndLocal:t}";
    public string DetailLabel => $"P95 {P95DurationMs:F0}ms · Max {MaxDurationMs:F0}ms";
}

public sealed record TelemetrySeverityBucketModel(string Severity, int Count)
{
    public string CountDisplay => Count.ToString();
}
