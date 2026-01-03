using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using ExcellCore.Domain.Services;
using ExcellCore.Module.Abstractions;
using ExcellCore.Shell.Services;

namespace ExcellCore.Shell.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IModuleCatalog _moduleCatalog;
    private readonly IModuleHost _moduleHost;
    private readonly INotificationCenter _notificationCenter;
    private readonly AgreementEscalationMonitor _escalationMonitor;
    private readonly TelemetryAggregationWorker _telemetryWorker;
    private readonly ILocalizationContext _localizationContext;
    private ModuleInfo? _selectedModule;
    private object? _activeModuleView;
    private string _escalationStatus = "Escalation monitor: initializing";
    private string _telemetrySeverity = "Unknown";
    private string _telemetryMessage = "Telemetry is initializing.";
    private string _telemetrySummary = "Awaiting first health snapshot.";
    private string _selectedLocalizationContext;

    public MainWindowViewModel(IModuleCatalog moduleCatalog, IModuleHost moduleHost, INotificationCenter notificationCenter, AgreementEscalationMonitor escalationMonitor, TelemetryAggregationWorker telemetryWorker, ILocalizationContext localizationContext)
    {
        _moduleCatalog = moduleCatalog;
        _moduleHost = moduleHost;
        _notificationCenter = notificationCenter;
        _escalationMonitor = escalationMonitor;
        _telemetryWorker = telemetryWorker;
        _localizationContext = localizationContext ?? throw new ArgumentNullException(nameof(localizationContext));

        _moduleHost.ModuleViewRegistered += OnModuleViewRegistered;
        _escalationMonitor.StatusChanged += OnMonitorStatusChanged;
        _telemetryWorker.HealthChanged += OnTelemetryHealthChanged;

        _escalationStatus = FormatEscalation(_escalationMonitor.Status, _escalationMonitor.LastError);
        if (_telemetryWorker.CurrentHealth is not null)
        {
            ApplyTelemetry(_telemetryWorker.CurrentHealth);
        }

        AvailableLocalizationContexts = new ObservableCollection<string>(new[]
        {
            "Core.Agreements",
            "Core.Identity.Clinical",
            "Core.Identity.Retail",
            "Core.Identity.Corporate",
            "Default"
        });

        _selectedLocalizationContext = AvailableLocalizationContexts.First();
        _localizationContext.SetContexts(new[] { _selectedLocalizationContext, "Default" });

        LoadModules();
    }

    public ObservableCollection<ModuleInfo> LoadedModules { get; } = new();

    public ObservableCollection<string> AvailableLocalizationContexts { get; }

    public string StatusMessage => LoadedModules.Count > 0
        ? SelectedModule is null
            ? $"{LoadedModules.Count} module(s) loaded"
            : $"Active module: {SelectedModule.DisplayName}"
        : "No modules loaded";

    public string SelectedLocalizationContext
    {
        get => _selectedLocalizationContext;
        set
        {
            if (SetProperty(ref _selectedLocalizationContext, value))
            {
                var chosen = string.IsNullOrWhiteSpace(value) ? Array.Empty<string>() : new[] { value, "Default" };
                _localizationContext.SetContexts(chosen);
            }
        }
    }

    public ReadOnlyObservableCollection<NotificationMessage> Notifications => _notificationCenter.Notifications;

    public string EscalationStatus
    {
        get => _escalationStatus;
        private set => SetProperty(ref _escalationStatus, value);
    }

    public string TelemetrySeverity
    {
        get => _telemetrySeverity;
        private set => SetProperty(ref _telemetrySeverity, value);
    }

    public string TelemetryMessage
    {
        get => _telemetryMessage;
        private set => SetProperty(ref _telemetryMessage, value);
    }

    public string TelemetrySummary
    {
        get => _telemetrySummary;
        private set => SetProperty(ref _telemetrySummary, value);
    }

    public ModuleInfo? SelectedModule
    {
        get => _selectedModule;
        set
        {
            if (SetProperty(ref _selectedModule, value))
            {
                UpdateActiveModuleView();
                OnPropertyChanged(nameof(StatusMessage));
            }
        }
    }

    public object? ActiveModuleView
    {
        get => _activeModuleView;
        private set => SetProperty(ref _activeModuleView, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void LoadModules()
    {
        LoadedModules.Clear();
        foreach (var module in _moduleCatalog.Modules)
        {
            LoadedModules.Add(new ModuleInfo(module.ModuleId, module.DisplayName, module.Version.ToString()));
        }
        SelectedModule ??= LoadedModules.FirstOrDefault();
        UpdateActiveModuleView();
        OnPropertyChanged(nameof(StatusMessage));
    }

    private void OnModuleViewRegistered(object? sender, ModuleViewRegisteredEventArgs e)
    {
        if (LoadedModules.All(m => m.ModuleId != e.ModuleId))
        {
            return;
        }

        if (SelectedModule is null)
        {
            SelectedModule = LoadedModules.FirstOrDefault(m => m.ModuleId == e.ModuleId) ?? LoadedModules.FirstOrDefault();
        }
        else if (SelectedModule.ModuleId == e.ModuleId)
        {
            UpdateActiveModuleView();
        }
    }

    private void OnMonitorStatusChanged(object? sender, MonitorStatusChangedEventArgs e)
    {
        EscalationStatus = FormatEscalation(e.Status, e.ErrorMessage);
    }

    private void OnTelemetryHealthChanged(object? sender, TelemetryHealthChangedEventArgs e)
    {
        if (Application.Current is { Dispatcher: { } dispatcher } && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => ApplyTelemetry(e.Health));
            return;
        }

        ApplyTelemetry(e.Health);
    }

    private void UpdateActiveModuleView()
    {
        if (SelectedModule is null)
        {
            ActiveModuleView = null;
            return;
        }

        if (_moduleHost.TryGetView(SelectedModule.ModuleId, out var view))
        {
            ActiveModuleView = view;
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void ApplyTelemetry(TelemetryHealthSnapshotDto health)
    {
        TelemetrySeverity = health.Status;
        TelemetryMessage = health.Message;
        TelemetrySummary = health.SampleCount == 0
            ? "No slow-query telemetry captured during the last window."
            : $"P95 {health.P95DurationMs:F0}ms · Max {health.MaxDurationMs:F0}ms · {health.SampleCount} event(s)";
    }

    private static string FormatEscalation(EscalationMonitorStatus status, string? error)
    {
        return status switch
        {
            EscalationMonitorStatus.Running => "Escalation monitor: running",
            EscalationMonitorStatus.Faulted => string.IsNullOrWhiteSpace(error)
                ? "Escalation monitor: faulted"
                : $"Escalation monitor: faulted ({error})",
            _ => "Escalation monitor: stopped"
        };
    }
}

public sealed record ModuleInfo(string ModuleId, string DisplayName, string Version);
