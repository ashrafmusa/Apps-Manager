using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using ExcellCore.Domain.Services;
using ExcellCore.Module.Extensions.Reporting.Commands;

namespace ExcellCore.Module.Extensions.Reporting.ViewModels;

public sealed class SlaWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly ISlaReportingService _slaReportingService;
    private readonly AsyncRelayCommand _refreshCommand;

    private bool _initialized;
    private bool _isBusy;
    private string _statusMessage = "Loading SLA metrics...";
    private string _lastUpdated = string.Empty;

    public SlaWorkspaceViewModel(ISlaReportingService slaReportingService)
    {
        _slaReportingService = slaReportingService ?? throw new ArgumentNullException(nameof(slaReportingService));
        SummaryCards = new ObservableCollection<SlaSummaryCardModel>();
        HeatMap = new ObservableCollection<SlaHeatMapCellModel>();
        Escalations = new ObservableCollection<SlaEscalationItemModel>();
        PredictiveCards = new ObservableCollection<SlaPredictiveCardModel>();
        _refreshCommand = new AsyncRelayCommand(RefreshAsync, () => !_isBusy);
    }

    public string Title => "SLA Dashboard";
    public string Subtitle => "Escalation health across modules.";

    public ObservableCollection<SlaSummaryCardModel> SummaryCards { get; }
    public ObservableCollection<SlaHeatMapCellModel> HeatMap { get; }
    public ObservableCollection<SlaEscalationItemModel> Escalations { get; }
    public ObservableCollection<SlaPredictiveCardModel> PredictiveCards { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string LastUpdated
    {
        get => _lastUpdated;
        private set => SetProperty(ref _lastUpdated, value);
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
        StatusMessage = "Loading SLA metrics...";

        try
        {
            var snapshot = await _slaReportingService.GetSnapshotAsync().ConfigureAwait(false);
            UpdateSummary(snapshot.Summary);
            UpdateHeatMap(snapshot.HeatMap);
            UpdateEscalations(snapshot.Escalations);
            UpdatePredictiveCards(snapshot.PredictiveCards);
            LastUpdated = FormattableString.Invariant($"Refreshed {DateTime.Now:t}");
            StatusMessage = "Predictive triage is live";
        }
        catch (Exception ex)
        {
            StatusMessage = FormattableString.Invariant($"Failed to load SLA metrics: {ex.Message}");
        }
        finally
        {
            _isBusy = false;
            _refreshCommand.RaiseCanExecuteChanged();
        }
    }

    private void UpdateSummary(SlaWorkspaceSummaryDto summary)
    {
        SummaryCards.Clear();

        SummaryCards.Add(new SlaSummaryCardModel(
            "Pending approvals",
            summary.PendingApprovals.ToString(CultureInfo.CurrentCulture),
            "Awaiting decision across modules",
            SlaSummarySeverity.Neutral));

        SummaryCards.Add(new SlaSummaryCardModel(
            "Breaches (≥24h)",
            summary.BreachedApprovals.ToString(CultureInfo.CurrentCulture),
            "Approvals over the escalation threshold",
            summary.BreachedApprovals > 0 ? SlaSummarySeverity.Warning : SlaSummarySeverity.Success));

        SummaryCards.Add(new SlaSummaryCardModel(
            "Reminders (7 days)",
            summary.RemindersLast7Days.ToString(CultureInfo.CurrentCulture),
            "Reminder nudges dispatched",
            SlaSummarySeverity.Neutral));

        SummaryCards.Add(new SlaSummaryCardModel(
            "Fast-tracks (7 days)",
            summary.FastTracksLast7Days.ToString(CultureInfo.CurrentCulture),
            "Escalations executed",
            summary.FastTracksLast7Days > 0 ? SlaSummarySeverity.Warning : SlaSummarySeverity.Neutral));
    }

    private void UpdateHeatMap(IReadOnlyCollection<SlaHeatMapCellDto> cells)
    {
        HeatMap.Clear();

        foreach (var cell in cells)
        {
            HeatMap.Add(new SlaHeatMapCellModel(cell));
        }
    }

    private void UpdateEscalations(IReadOnlyCollection<SlaEscalationDetailDto> escalations)
    {
        Escalations.Clear();

        foreach (var escalation in escalations)
        {
            Escalations.Add(new SlaEscalationItemModel(escalation));
        }
    }

    private void UpdatePredictiveCards(IReadOnlyCollection<SlaPredictiveCardDto> predictiveCards)
    {
        PredictiveCards.Clear();

        foreach (var card in predictiveCards)
        {
            PredictiveCards.Add(new SlaPredictiveCardModel(card));
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

public static class SlaSummarySeverity
{
    public const string Neutral = "Neutral";
    public const string Warning = "Warning";
    public const string Success = "Success";
}

public sealed class SlaSummaryCardModel
{
    public SlaSummaryCardModel(string title, string value, string description, string severity)
    {
        Title = title;
        Value = value;
        Description = description;
        Severity = severity;
    }

    public string Title { get; }
    public string Value { get; }
    public string Description { get; }
    public string Severity { get; }
}

public sealed class SlaHeatMapCellModel
{
    public SlaHeatMapCellModel(SlaHeatMapCellDto dto)
    {
        Module = dto.Module;
        PendingCount = dto.PendingCount;
        BreachCount = dto.BreachCount;
        PotentialValue = dto.PotentialValue;
        HeatBrush = ResolveBrush(dto.PendingCount, dto.BreachCount);
    }

    public string Module { get; }
    public int PendingCount { get; }
    public int BreachCount { get; }
    public decimal PotentialValue { get; }
    public Brush HeatBrush { get; }

    public string PendingDisplay => PendingCount switch
    {
        0 => "No approvals",
        1 => "1 approval",
        _ => FormattableString.Invariant($"{PendingCount} approvals")
    };

    public string BreachDisplay => BreachCount switch
    {
        0 => "No breaches",
        1 => "1 breach",
        _ => FormattableString.Invariant($"{BreachCount} breaches")
    };

    public string PotentialDisplay => PotentialValue <= 0m
        ? "No value scored"
        : PotentialValue.ToString("C0", CultureInfo.CurrentCulture);

    private static Brush ResolveBrush(int pendingCount, int breachCount)
    {
        if (pendingCount <= 0)
        {
            return Brushes.LightGreen;
        }

        var breachRate = (double)breachCount / pendingCount;

        if (breachRate >= 0.75d)
        {
            return Brushes.IndianRed;
        }

        if (breachRate >= 0.5d)
        {
            return Brushes.OrangeRed;
        }

        if (breachRate >= 0.25d)
        {
            return Brushes.Orange;
        }

        return breachCount > 0 ? Brushes.Goldenrod : Brushes.LightGreen;
    }
}

public sealed class SlaEscalationItemModel
{
    public SlaEscalationItemModel(SlaEscalationDetailDto dto)
    {
        AgreementId = dto.AgreementId;
        ApprovalId = dto.ApprovalId;
        AgreementName = dto.AgreementName;
        Module = dto.Module;
        Approver = dto.Approver;
        RequestedOnUtc = dto.RequestedOnUtc;
        Age = dto.Age;
        PotentialValue = dto.PotentialValue;
        ImpactedParties = dto.ImpactedParties;
    }

    public Guid AgreementId { get; }
    public Guid ApprovalId { get; }
    public string AgreementName { get; }
    public string Module { get; }
    public string Approver { get; }
    public DateTime RequestedOnUtc { get; }
    public TimeSpan Age { get; }
    public decimal PotentialValue { get; }
    public IReadOnlyList<string> ImpactedParties { get; }

    public string AgeDisplay
    {
        get
        {
            var totalHours = Math.Floor(Age.TotalHours);
            var minutes = Math.Max(0, Age.Minutes);
            return FormattableString.Invariant($"{totalHours:0}h {minutes:00}m");
        }
    }

    public string RequestedDisplay => DateTime.SpecifyKind(RequestedOnUtc, DateTimeKind.Utc).ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public string PotentialDisplay => PotentialValue <= 0m
        ? "--"
        : PotentialValue.ToString("C0", CultureInfo.CurrentCulture);

    public string ImpactedSummary => ImpactedParties.Count == 0
        ? "No impacted identities"
        : string.Join(", ", ImpactedParties);
}

public sealed class SlaPredictiveCardModel
{
    public SlaPredictiveCardModel(SlaPredictiveCardDto dto)
    {
        AgreementId = dto.AgreementId;
        ApprovalId = dto.ApprovalId;
        AgreementName = dto.AgreementName;
        Module = dto.Module;
        Approver = dto.Approver;
        Age = dto.Age;
        EstimatedTimeToBreach = dto.EstimatedTimeToBreach;
        PotentialValue = dto.PotentialValue;
        RiskScore = dto.RiskScore;
        RiskLevel = dto.RiskLevel;
        DriverSummary = dto.DriverSummary;
        ImpactedParties = dto.ImpactedParties;
        ActionHint = ResolveActionHint(dto.RiskLevel, dto.EstimatedTimeToBreach);
    }

    public Guid AgreementId { get; }
    public Guid ApprovalId { get; }
    public string AgreementName { get; }
    public string Module { get; }
    public string Approver { get; }
    public TimeSpan Age { get; }
    public TimeSpan EstimatedTimeToBreach { get; }
    public decimal PotentialValue { get; }
    public double RiskScore { get; }
    public string RiskLevel { get; }
    public string DriverSummary { get; }
    public IReadOnlyList<string> ImpactedParties { get; }
    public string ActionHint { get; }

    public string RiskDisplay => FormattableString.Invariant($"{RiskLevel} · {RiskScore:P0}");

    public string EtaDisplay => EstimatedTimeToBreach == TimeSpan.Zero
        ? "Breach imminent"
        : FormattableString.Invariant($"ETA ~{Math.Max(0, Math.Floor(EstimatedTimeToBreach.TotalHours)):0}h");

    public string AgeDisplay
    {
        get
        {
            var totalHours = Math.Floor(Age.TotalHours);
            var minutes = Math.Max(0, Age.Minutes);
            return FormattableString.Invariant($"{totalHours:0}h {minutes:00}m");
        }
    }

    public string PotentialDisplay => PotentialValue <= 0m
        ? "--"
        : PotentialValue.ToString("C0", CultureInfo.CurrentCulture);

    public string ImpactedSummary => ImpactedParties.Count == 0
        ? "No impacted identities"
        : string.Join(", ", ImpactedParties);

    private static string ResolveActionHint(string riskLevel, TimeSpan eta)
    {
        if (riskLevel.Equals("High", StringComparison.OrdinalIgnoreCase))
        {
            return eta <= TimeSpan.FromHours(1)
                ? "Escalate approver now; consider fast-track"
                : "Ping approver and add fast-track if no response";
        }

        if (riskLevel.Equals("Medium", StringComparison.OrdinalIgnoreCase))
        {
            return "Send reminder and review impacted parties";
        }

        return "Monitor; no immediate action";
    }
}
