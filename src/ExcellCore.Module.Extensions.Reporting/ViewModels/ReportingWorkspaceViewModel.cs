using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;

namespace ExcellCore.Module.Extensions.Reporting.ViewModels;

public sealed class ReportingWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly IReportingService _reportingService;
    private bool _initialized;
    private ReportingSummaryModel _summary = ReportingSummaryModel.Empty;
    private string _statusMessage = "Loading reporting pipelines...";

    public ReportingWorkspaceViewModel(IReportingService reportingService)
    {
        _reportingService = reportingService ?? throw new ArgumentNullException(nameof(reportingService));
        Dashboards = new ObservableCollection<ReportingDashboardModel>();
        ExportSchedules = new ObservableCollection<ReportingScheduleModel>();
    }

    public string Title => "Reporting Hub";
    public string Subtitle => "Dashboards, exports, and analytics jobs.";

    public ObservableCollection<ReportingDashboardModel> Dashboards { get; }

    public ObservableCollection<ReportingScheduleModel> ExportSchedules { get; }
    public ReportingSummaryModel Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await LoadAsync();
        _initialized = true;
    }

    private async Task LoadAsync()
    {
        StatusMessage = "Loading reporting pipelines...";

        try
        {
            var snapshot = await _reportingService.GetWorkspaceAsync();

            Dashboards.Clear();
            foreach (var dashboard in snapshot.Dashboards)
            {
                Dashboards.Add(new ReportingDashboardModel(dashboard.Name, dashboard.Description, dashboard.Domain));
            }

            ExportSchedules.Clear();
            foreach (var schedule in snapshot.Schedules)
            {
                var nextRunLocal = DateTime.SpecifyKind(schedule.NextRunUtc, DateTimeKind.Utc).ToLocalTime();
                ExportSchedules.Add(new ReportingScheduleModel(schedule.Name, schedule.Format, schedule.Cadence, nextRunLocal));
            }

            Summary = new ReportingSummaryModel(
                snapshot.Summary.ActiveDashboards,
                snapshot.Summary.ScheduledExports,
                snapshot.Summary.ImminentRuns);

            StatusMessage = $"Pipelines refreshed {DateTime.Now:t}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load reporting pipelines: {ex.Message}";
        }
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

public sealed record ReportingSummaryModel(int ActiveDashboards, int ScheduledExports, int ImminentRuns)
{
    public static ReportingSummaryModel Empty { get; } = new(0, 0, 0);
}

public sealed record ReportingDashboardModel(string Name, string Description, string Domain);

public sealed record ReportingScheduleModel(string Name, string Format, TimeSpan Cadence, DateTime NextRunLocal)
{
    public string CadenceDisplay => Cadence.TotalHours >= 24
        ? $"Every {Cadence.TotalHours / 24:0.#} day(s)"
        : $"Every {Cadence.TotalHours:0.#} hour(s)";

    public string NextRunDisplay => NextRunLocal.ToString("g");
}
