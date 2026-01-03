using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using ExcellCore.Domain.Services;
using ExcellCore.Domain.Entities;
using ExcellCore.Module.Core.Agreements.Commands;
using ExcellCore.Module.Abstractions;

namespace ExcellCore.Module.Core.Agreements.ViewModels;

public sealed class AgreementWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly IAgreementService _agreementService;
    private readonly IPartyService _partyService;
    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly AsyncRelayCommand _calculateCommand;
    private readonly AsyncRelayCommand _requestApprovalCommand;
    private readonly AsyncRelayCommand _approveApprovalCommand;
    private readonly AsyncRelayCommand _rejectApprovalCommand;
    private readonly AsyncRelayCommand _scheduleRenewalCommand;
    private readonly AsyncRelayCommand _markRenewedCommand;
    private readonly RelayCommand _newCommand;
    private readonly RelayCommand _addRateCommand;
    private readonly RelayCommand _removeRateCommand;
    private static readonly IReadOnlyDictionary<string, string> DefaultLocalization = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["ImpactedPartyColumn"] = "Impacted Party",
        ["ImpactedPartyTypeColumn"] = "Type",
        ["ImpactedPartyRelationshipColumn"] = "Relationship",
        ["ValidationUnknownIdentity"] = "Select a known identity before saving impacted parties.",
        ["ValidationIncompleteAgreement"] = "Fill in required fields and add at least one rate before saving.",
        ["AgreementsTitle"] = "Agreement Engine",
        ["AgreementsModuleBadge"] = "(Core Module)",
        ["SummaryActiveAgreements"] = "Active Agreements",
        ["SummaryPendingApprovals"] = "Pending Approvals",
        ["SummaryRenewals"] = "Renewals (30d)",
        ["SummaryAvgDiscount"] = "Avg Discount",
        ["SummaryPricingRuns"] = "Pricing Runs",
        ["SlaHeatMapTitle"] = "SLA Heat Map",
        ["SlaHeatMapEmpty"] = "No pending approvals to score.",
        ["ReminderFastTrackTitle"] = "Reminder & Fast-Track Activity",
        ["ReminderFastTrackEmpty"] = "No recent reminder or fast-track activity.",
        ["SearchButton"] = "Search",
        ["NewButton"] = "New",
        ["SaveButton"] = "Save",
        ["ApprovalsHeader"] = "Approvals",
        ["ApproverHeader"] = "Approver",
        ["DecisionHeader"] = "Decision",
        ["RequestedHeader"] = "Requested",
        ["DecidedHeader"] = "Decided",
        ["ApproverLabel"] = "Approver",
        ["RequestApprovalButton"] = "Request Approval",
        ["ReviewerCommentsLabel"] = "Reviewer Comments",
        ["ApproveButton"] = "Approve",
        ["RejectButton"] = "Reject",
        ["AddImpactButton"] = "Add Impact",
        ["RemoveButton"] = "Remove",
        ["AddRateButton"] = "Add Rate",
        ["PricingHeader"] = "Pricing",
        ["ServiceCodeLabel"] = "Service Code",
        ["QuantityLabel"] = "Quantity",
        ["ListPriceLabel"] = "List Price",
        ["CalculateButton"] = "Calculate",
        ["NetLabel"] = "Net",
        ["DiscountLabel"] = "Discount",
        ["CopayLabel"] = "Co-pay",
        ["PricingHistoryHeader"] = "Pricing History",
        ["PricingHistoryWhen"] = "When",
        ["PricingHistoryService"] = "Service",
        ["PricingHistoryQty"] = "Qty",
        ["PricingHistoryList"] = "List",
        ["PricingHistoryNet"] = "Net",
        ["PricingHistoryDiscount"] = "Discount",
        ["PricingHistoryCopay"] = "Co-pay",
        ["RenewalDateLabel"] = "Renewal Date",
        ["AutoRenewLabel"] = "Auto Renew",
        ["ScheduleRenewalButton"] = "Schedule",
        ["MarkRenewedButton"] = "Mark Renewed",
        ["StatusLabel"] = "Status",
        ["LastRenewedLabel"] = "Last Renewed",
        ["RequiresApprovalLabel"] = "Requires Approval",
        ["AgreementColumn"] = "Agreement",
        ["PayerColumn"] = "Payer",
        ["EffectiveColumn"] = "Effective",
        ["ExpiresColumn"] = "Expires",
        ["StatusColumn"] = "Status",
        ["RatesColumn"] = "Rates",
        ["AgreementDetailsHeader"] = "Agreement Details",
        ["AgreementNameLabel"] = "Agreement Name",
        ["PayerLabel"] = "Payer",
        ["CoverageTypeLabel"] = "Coverage Type",
        ["EffectiveFromLabel"] = "Effective From",
        ["EffectiveToLabel"] = "Effective To",
        ["RemindApproverButton"] = "Remind Approver",
        ["FastTrackButton"] = "Fast-Track"
    };
    private static readonly IReadOnlyList<string> LocalizationContexts = new[] { "Core.Agreements", "Default" };
    private readonly ILocalizationService _localizationService;
    private readonly ILocalizationContext _localizationContext;
    private readonly RelayCommand _addImpactedPartyCommand;
    private readonly RelayCommand _removeImpactedPartyCommand;
    private const int PricingHistoryCapacity = 20;
    private CancellationTokenSource? _partyLookupCts;
    private readonly Dictionary<string, IReadOnlyList<PartyLookupResultModel>> _partyLookupCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressLookupRefresh;
    private IReadOnlyDictionary<string, string> _localization = DefaultLocalization;
    private bool _isBusy;
    private string _searchText = string.Empty;
    private string _statusMessage = "Ready";
    private string? _notification;
    private AgreementSummaryModel? _selectedAgreement;
    private AgreementRateModel? _selectedRate;
    private AgreementApprovalModel? _selectedApproval;
    private AgreementImpactedPartyModel? _selectedImpactedParty;
    private string _approvalApprover = string.Empty;
    private string? _approvalComments;

    public AgreementWorkspaceViewModel(IAgreementService agreementService, IPartyService partyService, ILocalizationService localizationService, ILocalizationContext localizationContext)
    {
        _agreementService = agreementService ?? throw new ArgumentNullException(nameof(agreementService));
        _partyService = partyService ?? throw new ArgumentNullException(nameof(partyService));
        _localizationService = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _localizationContext = localizationContext ?? throw new ArgumentNullException(nameof(localizationContext));

        Agreements = new ObservableCollection<AgreementSummaryModel>();
        Form = new AgreementFormModel();
        Rates = new ObservableCollection<AgreementRateModel>();
        ImpactedParties = new ObservableCollection<AgreementImpactedPartyModel>();
        PartyLookupResults = new ObservableCollection<PartyLookupResultModel>();
        CoverageTypes = new ObservableCollection<string>(new[] { "Insurance", "Corporate", "Direct", "Partner" });
        WorkflowStatuses = new ObservableCollection<string>(new[]
        {
            AgreementStatus.Draft,
            AgreementStatus.PendingApproval,
            AgreementStatus.Approved,
            AgreementStatus.Active,
            AgreementStatus.Expired
        });
        Pricing = new PricingRequestModel();
        Dashboard = new AgreementDashboardModel();
        PricingHistory = new ObservableCollection<PricingHistoryModel>();
        Approvals = new ObservableCollection<AgreementApprovalModel>();
        TriageCards = new ObservableCollection<ApprovalTriageCardModel>();
        TriageHeatMap = new ObservableCollection<TriageHeatMapBucketModel>();
        TriageActionInsights = new ObservableCollection<TriageActionInsightModel>();

        Form.PropertyChanged += (_, _) => UpdateCommandStates();
        Rates.CollectionChanged += OnRatesCollectionChanged;
        Approvals.CollectionChanged += OnApprovalsCollectionChanged;
        ImpactedParties.CollectionChanged += OnImpactedPartiesCollectionChanged;

        _searchCommand = new AsyncRelayCommand(() => RefreshAsync(), () => !IsBusy);
        _saveCommand = new AsyncRelayCommand(SaveAsync, CanSave);
        _calculateCommand = new AsyncRelayCommand(CalculateAsync, CanCalculate);
        _requestApprovalCommand = new AsyncRelayCommand(RequestApprovalAsync, CanRequestApproval);
        _approveApprovalCommand = new AsyncRelayCommand(() => CompleteApprovalAsync(ApprovalDecision.Approved), CanCompleteApproval);
        _rejectApprovalCommand = new AsyncRelayCommand(() => CompleteApprovalAsync(ApprovalDecision.Rejected), CanCompleteApproval);
        _scheduleRenewalCommand = new AsyncRelayCommand(ScheduleRenewalAsync, CanScheduleRenewal);
        _markRenewedCommand = new AsyncRelayCommand(MarkRenewedAsync, CanMarkRenewed);
        _newCommand = new RelayCommand(NewAgreement);
        _addRateCommand = new RelayCommand(AddRate, () => !IsBusy);
        _removeRateCommand = new RelayCommand(RemoveSelectedRate, () => !IsBusy && SelectedRate is not null);
        _addImpactedPartyCommand = new RelayCommand(AddImpactedParty, () => !IsBusy);
        _removeImpactedPartyCommand = new RelayCommand(RemoveSelectedImpactedParty, () => !IsBusy && SelectedImpactedParty is not null);

        _localizationContext.ContextsChanged += OnLocalizationContextsChanged;
        Localization = BuildLocalization(_localizationService, _localizationContext.Contexts);

        AddRate();
        AddImpactedParty();
    }

    public ObservableCollection<AgreementSummaryModel> Agreements { get; }
    public ObservableCollection<AgreementRateModel> Rates { get; }
    public ObservableCollection<AgreementImpactedPartyModel> ImpactedParties { get; }
    public IReadOnlyDictionary<string, string> Localization
    {
        get => _localization;
        private set => SetProperty(ref _localization, value);
    }
    public ObservableCollection<string> CoverageTypes { get; }
    public ObservableCollection<string> WorkflowStatuses { get; }
    public ObservableCollection<PartyLookupResultModel> PartyLookupResults { get; }
    public AgreementFormModel Form { get; }
    public PricingRequestModel Pricing { get; }
    public AgreementDashboardModel Dashboard { get; }
    public ObservableCollection<PricingHistoryModel> PricingHistory { get; }
    public ObservableCollection<AgreementApprovalModel> Approvals { get; }
    public ObservableCollection<ApprovalTriageCardModel> TriageCards { get; }
    public ObservableCollection<TriageHeatMapBucketModel> TriageHeatMap { get; }
    public ObservableCollection<TriageActionInsightModel> TriageActionInsights { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public AgreementSummaryModel? SelectedAgreement
    {
        get => _selectedAgreement;
        set
        {
            var changed = SetProperty(ref _selectedAgreement, value);

            if (!changed && value is null)
            {
                Form.Reset();
                ClearRates();
                AddRate();
                ClearImpactedParties();
                AddImpactedParty();
                PricingHistory.Clear();
                Approvals.Clear();
                SelectedApproval = null;
                ApprovalApprover = string.Empty;
                ApprovalComments = string.Empty;
                UpdateCommandStates();
                return;
            }

            if (!changed && value is not null)
            {
                _ = LoadAgreementAsync(value.AgreementId);
                return;
            }

            if (value is null)
            {
                Form.Reset();
                ClearRates();
                AddRate();
                ClearImpactedParties();
                AddImpactedParty();
                PricingHistory.Clear();
                Approvals.Clear();
                SelectedApproval = null;
                ApprovalApprover = string.Empty;
                ApprovalComments = string.Empty;
            }
            else
            {
                _ = LoadAgreementAsync(value.AgreementId);
            }

            UpdateCommandStates();
        }
    }

    public AgreementRateModel? SelectedRate
    {
        get => _selectedRate;
        set
        {
            if (SetProperty(ref _selectedRate, value))
            {
                if (value is not null)
                {
                    Pricing.ServiceCode = value.ServiceCode;
                    if (value.BaseAmount > 0m)
                    {
                        Pricing.ListPrice = value.BaseAmount;
                    }
                }
                UpdateCommandStates();
            }
        }
    }

    public AgreementImpactedPartyModel? SelectedImpactedParty
    {
        get => _selectedImpactedParty;
        set
        {
            if (SetProperty(ref _selectedImpactedParty, value))
            {
                UpdateCommandStates();
                if (value is not null)
                {
                    _ = RefreshPartyLookupAsync(value);
                }
                else
                {
                    ClearPartyLookupResults();
                }
            }
        }
    }

    public AgreementApprovalModel? SelectedApproval
    {
        get => _selectedApproval;
        set
        {
            if (SetProperty(ref _selectedApproval, value))
            {
                ApprovalComments = value?.Comments ?? string.Empty;
                UpdateCommandStates();
            }
        }
    }

    public string ApprovalApprover
    {
        get => _approvalApprover;
        set
        {
            if (SetProperty(ref _approvalApprover, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public string? ApprovalComments
    {
        get => _approvalComments;
        set
        {
            if (SetProperty(ref _approvalComments, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string? Notification
    {
        get => _notification;
        private set => SetProperty(ref _notification, value);
    }

    public ICommand SearchCommand => _searchCommand;
    public ICommand SaveCommand => _saveCommand;
    public ICommand NewCommand => _newCommand;
    public ICommand AddRateCommand => _addRateCommand;
    public ICommand RemoveRateCommand => _removeRateCommand;
    public ICommand AddImpactedPartyCommand => _addImpactedPartyCommand;
    public ICommand RemoveImpactedPartyCommand => _removeImpactedPartyCommand;
    public ICommand CalculateCommand => _calculateCommand;
    public ICommand RequestApprovalCommand => _requestApprovalCommand;
    public ICommand ApproveApprovalCommand => _approveApprovalCommand;
    public ICommand RejectApprovalCommand => _rejectApprovalCommand;
    public ICommand ScheduleRenewalCommand => _scheduleRenewalCommand;
    public ICommand MarkRenewedCommand => _markRenewedCommand;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await WarmPartyLookupCacheAsync(cancellationToken);
        await RefreshAsync(cancellationToken: cancellationToken);
    }

    private async Task RefreshAsync(Guid? highlightId = null, CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        AgreementSummaryModel? selection = null;
        var refreshSucceeded = false;

        try
        {
            var agreements = await _agreementService.SearchAsync(SearchText, cancellationToken);
            Agreements.Clear();
            foreach (var agreement in agreements)
            {
                Agreements.Add(new AgreementSummaryModel(
                    agreement.AgreementId,
                    agreement.AgreementName,
                    agreement.PayerName,
                    agreement.EffectiveFrom,
                    agreement.EffectiveTo,
                    agreement.RateCount,
                        string.IsNullOrWhiteSpace(agreement.Status) ? AgreementStatus.Draft : agreement.Status,
                    agreement.RenewalDate));
            }

            await UpdateDashboardAsync(cancellationToken);
            await UpdateTriageAsync(cancellationToken);

            StatusMessage = Agreements.Count == 0
                ? "No agreements found."
                : $"Showing {Agreements.Count} agreement(s) | Catalog total {Dashboard.TotalAgreements}.";

            if (highlightId.HasValue)
            {
                selection = Agreements.FirstOrDefault(a => a.AgreementId == highlightId.Value);
            }
            else if (SelectedAgreement is not null)
            {
                selection = Agreements.FirstOrDefault(a => a.AgreementId == SelectedAgreement.AgreementId);
            }

            refreshSucceeded = true;
        }
        catch (Exception ex)
        {
            Notification = $"Agreement search failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        if (refreshSucceeded)
        {
            SelectedAgreement = selection ?? Agreements.FirstOrDefault();
        }
    }

    private async Task UpdateDashboardAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await _agreementService.GetDashboardAsync(cancellationToken);
            Dashboard.Apply(snapshot);
        }
        catch (Exception ex)
        {
            Notification = $"Failed to load agreement metrics: {ex.Message}";
        }
    }

    private async Task UpdateTriageAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var triageSnapshot = await _agreementService.GetApprovalTriageAsync(cancellationToken);
            TriageCards.Clear();

            foreach (var triageItem in triageSnapshot.Items)
            {
                var capturedItem = triageItem;
                TriageCards.Add(new ApprovalTriageCardModel(
                    capturedItem,
                    () => SendApprovalReminderFromCardAsync(capturedItem.AgreementId, capturedItem.AgreementApprovalId),
                    () => FastTrackApprovalFromCardAsync(capturedItem.AgreementId, capturedItem.AgreementApprovalId)));
            }

            TriageHeatMap.Clear();
            foreach (var bucket in triageSnapshot.HeatMap)
            {
                TriageHeatMap.Add(new TriageHeatMapBucketModel(bucket));
            }

            TriageActionInsights.Clear();
            foreach (var insight in triageSnapshot.ActionInsights)
            {
                TriageActionInsights.Add(new TriageActionInsightModel(insight));
            }
        }
        catch (Exception ex)
        {
            Notification = $"Failed to load triage insights: {ex.Message}";
        }
    }

    private async Task SendApprovalReminderFromCardAsync(Guid agreementId, Guid approvalId)
    {
        if (IsBusy)
        {
            return;
        }

        var targetCard = TriageCards.FirstOrDefault(c => c.AgreementId == agreementId && c.ApprovalId == approvalId);
        var approverName = targetCard?.Approver ?? "approver";

        IsBusy = true;

        try
        {
            await _agreementService.SendApprovalReminderAsync(agreementId, approvalId);
            Notification = $"Reminder sent to {approverName}.";
            await UpdateTriageAsync();
        }
        catch (Exception ex)
        {
            Notification = $"Reminder failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task FastTrackApprovalFromCardAsync(Guid agreementId, Guid approvalId)
    {
        if (IsBusy)
        {
            return;
        }

        var targetCard = TriageCards.FirstOrDefault(c => c.AgreementId == agreementId && c.ApprovalId == approvalId);
        var approverName = targetCard?.Approver ?? "approver";
        var agreementName = targetCard?.AgreementName ?? "agreement";

        IsBusy = true;

        try
        {
            await _agreementService.FastTrackApprovalAsync(agreementId, approvalId);
            Notification = $"Fast-tracked approval for {agreementName} ({approverName}).";
            await UpdateTriageAsync();
        }
        catch (Exception ex)
        {
            Notification = $"Fast-track failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAgreementAsync(Guid agreementId)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var detail = await _agreementService.GetAsync(agreementId);
            if (detail is null)
            {
                Notification = "Agreement could not be loaded.";
                Form.Reset();
                ClearRates();
                AddRate();
                ClearImpactedParties();
                AddImpactedParty();
                return;
            }

            Form.LoadFrom(detail);

            ClearImpactedParties();
            if (detail.ImpactedParties is not null)
            {
                foreach (var impacted in detail.ImpactedParties)
                {
                    var impactedModel = new AgreementImpactedPartyModel
                    {
                        AgreementImpactedPartyId = impacted.AgreementImpactedPartyId,
                        PartyId = impacted.PartyId,
                        PartyName = impacted.PartyName,
                        PartyType = impacted.PartyType,
                        Relationship = impacted.Relationship
                    };

                    ImpactedParties.Add(impactedModel);
                }
            }

            if (ImpactedParties.Count == 0)
            {
                AddImpactedParty();
            }

            ClearRates();
            foreach (var rate in detail.Rates)
            {
                var model = new AgreementRateModel
                {
                    AgreementRateId = rate.AgreementRateId,
                    ServiceCode = rate.ServiceCode,
                    BaseAmount = rate.BaseAmount,
                    DiscountPercent = rate.DiscountPercent,
                    CopayPercent = rate.CoPayPercent
                };
                Rates.Add(model);
            }

            Approvals.Clear();
            foreach (var approval in detail.Approvals)
            {
                var approvalModel = new AgreementApprovalModel
                {
                    AgreementApprovalId = approval.AgreementApprovalId,
                    Approver = approval.Approver,
                    Decision = approval.Decision,
                    Comments = approval.Comments,
                    RequestedOn = approval.RequestedOnUtc.ToLocalTime(),
                    DecidedOn = approval.DecidedOnUtc?.ToLocalTime()
                };

                Approvals.Add(approvalModel);
            }

            PricingHistory.Clear();

            SelectedRate = Rates.FirstOrDefault();
            Pricing.ServiceCode = SelectedRate?.ServiceCode ?? string.Empty;
            SelectedApproval = Approvals.FirstOrDefault();
            SelectedImpactedParty = ImpactedParties.FirstOrDefault();
            ApprovalApprover = string.Empty;
        }
        catch (Exception ex)
        {
            Notification = $"Failed to load agreement: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (ImpactedParties.Any(p => !p.IsEmpty && !p.IsValid))
        {
            Notification = GetLocalization("ValidationUnknownIdentity", "Select a known identity before saving impacted parties.");
            return;
        }

        if (!CanSave())
        {
            Notification = GetLocalization("ValidationIncompleteAgreement", "Fill in required fields and add at least one rate before saving.");
            return;
        }

        IsBusy = true;

        Guid? savedAgreementId = null;

        try
        {
            var isNew = !Form.AgreementId.HasValue;
            var detail = Form.ToDetail(Rates, Approvals, ImpactedParties);
            var saved = await _agreementService.SaveAsync(detail);
            savedAgreementId = saved.AgreementId;
            Notification = isNew ? "Agreement created." : "Agreement updated.";
            Form.LoadFrom(saved);
            ClearRates();
            foreach (var rate in saved.Rates)
            {
                Rates.Add(new AgreementRateModel
                {
                    AgreementRateId = rate.AgreementRateId,
                    ServiceCode = rate.ServiceCode,
                    BaseAmount = rate.BaseAmount,
                    DiscountPercent = rate.DiscountPercent,
                    CopayPercent = rate.CoPayPercent
                });
            }

            Approvals.Clear();
            foreach (var approval in saved.Approvals)
            {
                Approvals.Add(new AgreementApprovalModel
                {
                    AgreementApprovalId = approval.AgreementApprovalId,
                    Approver = approval.Approver,
                    Decision = approval.Decision,
                    Comments = approval.Comments,
                    RequestedOn = approval.RequestedOnUtc.ToLocalTime(),
                    DecidedOn = approval.DecidedOnUtc?.ToLocalTime()
                });
            }

            ClearImpactedParties();
            if (saved.ImpactedParties is not null)
            {
                foreach (var impacted in saved.ImpactedParties)
                {
                    ImpactedParties.Add(new AgreementImpactedPartyModel
                    {
                        AgreementImpactedPartyId = impacted.AgreementImpactedPartyId,
                        PartyId = impacted.PartyId,
                        PartyName = impacted.PartyName,
                        PartyType = impacted.PartyType,
                        Relationship = impacted.Relationship
                    });
                }
            }

            if (ImpactedParties.Count == 0)
            {
                AddImpactedParty();
            }

            SelectedApproval = Approvals.FirstOrDefault();
            SelectedImpactedParty = ImpactedParties.FirstOrDefault();
            ApprovalApprover = string.Empty;
        }
        catch (Exception ex)
        {
            Notification = $"Failed to save agreement: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        if (savedAgreementId.HasValue)
        {
            await RefreshAsync(savedAgreementId);
        }
    }

    private void NewAgreement()
    {
        SelectedAgreement = null;
        Form.Reset();
        ClearRates();
        AddRate();
        ClearImpactedParties();
        AddImpactedParty();
        Pricing.Reset();
        PricingHistory.Clear();
        Approvals.Clear();
        SelectedApproval = null;
        ApprovalApprover = string.Empty;
        ApprovalComments = string.Empty;
        Notification = "Ready for new agreement details.";
    }

    private void AddRate()
    {
        if (IsBusy)
        {
            return;
        }

        var rate = new AgreementRateModel
        {
            ServiceCode = $"SRV-{Rates.Count + 1:000}",
            BaseAmount = 0m,
            DiscountPercent = 0m,
            CopayPercent = 0m
        };
        Rates.Add(rate);
        SelectedRate = rate;
    }

    private void AddImpactedParty()
    {
        var impacted = new AgreementImpactedPartyModel
        {
            Relationship = string.Empty
        };

        ImpactedParties.Add(impacted);
        SelectedImpactedParty = impacted;
    }

    private void RemoveSelectedRate()
    {
        if (SelectedRate is null)
        {
            return;
        }

        Rates.Remove(SelectedRate);
        SelectedRate = Rates.FirstOrDefault();
    }

    private void RemoveSelectedImpactedParty()
    {
        if (SelectedImpactedParty is null)
        {
            return;
        }

        ImpactedParties.Remove(SelectedImpactedParty);
        SelectedImpactedParty = ImpactedParties.FirstOrDefault();
    }

    private void ClearPartyLookupResults()
    {
        CancelActiveLookup();
        PartyLookupResults.Clear();
    }

    private async Task WarmPartyLookupCacheAsync(CancellationToken cancellationToken)
    {
        if (_partyLookupCache.ContainsKey(string.Empty))
        {
            return;
        }

        try
        {
            var results = await _partyService.LookupAsync(null, cancellationToken);
            var models = MapLookupResults(results);
            _partyLookupCache[string.Empty] = models;

            if (PartyLookupResults.Count == 0 && SelectedImpactedParty is not null)
            {
                ApplyLookupResults(models, SelectedImpactedParty);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Notification = $"Party lookup warmup failed: {ex.Message}";
        }
    }

    private async Task RefreshPartyLookupAsync(AgreementImpactedPartyModel impacted)
    {
        if (impacted is null)
        {
            ClearPartyLookupResults();
            return;
        }

        CancelActiveLookup();

        var rawSearch = impacted.PartyName?.Trim() ?? string.Empty;
        var cacheKey = rawSearch.Length >= 2 ? rawSearch : string.Empty;

        if (_partyLookupCache.TryGetValue(cacheKey, out var cachedResults))
        {
            ApplyLookupResults(cachedResults, impacted);
            return;
        }

        if (cacheKey.Length == 0)
        {
            await WarmPartyLookupCacheAsync(CancellationToken.None);
            if (_partyLookupCache.TryGetValue(cacheKey, out cachedResults))
            {
                ApplyLookupResults(cachedResults, impacted);
            }
            else
            {
                PartyLookupResults.Clear();
            }
            return;
        }

        var cts = new CancellationTokenSource();
        _partyLookupCts = cts;

        try
        {
            var results = await _partyService.LookupAsync(rawSearch, cts.Token);
            if (cts.IsCancellationRequested)
            {
                return;
            }

            var models = MapLookupResults(results);
            _partyLookupCache[cacheKey] = models;
            ApplyLookupResults(models, impacted);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Notification = $"Party lookup failed: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_partyLookupCts, cts))
            {
                _partyLookupCts = null;
            }

            cts.Dispose();
        }
    }

    private void CancelActiveLookup()
    {
        if (_partyLookupCts is null)
        {
            return;
        }

        try
        {
            _partyLookupCts.Cancel();
        }
        finally
        {
            _partyLookupCts.Dispose();
            _partyLookupCts = null;
        }
    }

    private void ApplyLookupResults(IReadOnlyList<PartyLookupResultModel> results, AgreementImpactedPartyModel? impacted)
    {
        PartyLookupResults.Clear();
        foreach (var result in results)
        {
            PartyLookupResults.Add(result);
        }

        if (impacted is null)
        {
            return;
        }

        _suppressLookupRefresh = true;
        try
        {
            if (impacted.PartyId.HasValue)
            {
                var match = results.FirstOrDefault(r => r.PartyId == impacted.PartyId.Value);
                if (match is not null)
                {
                    impacted.SelectedLookup = match;
                }
            }
        }
        finally
        {
            _suppressLookupRefresh = false;
        }
    }

    private void OnLocalizationContextsChanged(object? sender, EventArgs e)
    {
        Localization = BuildLocalization(_localizationService, _localizationContext.Contexts);
    }

    private static List<PartyLookupResultModel> MapLookupResults(IEnumerable<PartyLookupResultDto> source)
    {
        return source
            .Select(result => new PartyLookupResultModel(result.PartyId, result.DisplayName, result.PartyType, result.PrimaryIdentifier, result.RelationshipContext))
            .ToList();
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

    private string GetLocalization(string key, string fallback)
    {
        return Localization.TryGetValue(key, out var value) ? value : fallback;
    }

    private async Task CalculateAsync()
    {
        if (IsBusy || SelectedAgreement is null)
        {
            return;
        }

        var serviceCode = Pricing.ServiceCode;
        if (string.IsNullOrWhiteSpace(serviceCode))
        {
            serviceCode = SelectedRate?.ServiceCode ?? Rates.FirstOrDefault()?.ServiceCode ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(serviceCode))
        {
            Notification = "Enter a service code to price.";
            return;
        }

        var quantity = Pricing.Quantity <= 0 ? 1 : Pricing.Quantity;
        var listPrice = Pricing.ListPrice;

        if (listPrice <= 0m && SelectedRate is not null)
        {
            listPrice = SelectedRate.BaseAmount;
        }

        Pricing.ListPrice = listPrice;

        IsBusy = true;

        try
        {
            var result = await _agreementService.CalculatePriceAsync(
                SelectedAgreement.AgreementId,
                serviceCode,
                listPrice,
                quantity);

            Pricing.Result = new PricingResultModel(result.NetAmount, result.DiscountAmount, result.CopayAmount);
            Notification = $"Pricing calculated for {serviceCode}.";

            var historyEntry = new PricingHistoryModel(
                DateTime.Now,
                serviceCode,
                listPrice,
                quantity,
                result.NetAmount,
                result.DiscountAmount,
                result.CopayAmount);

            AppendPricingHistoryEntry(historyEntry);
        }
        catch (Exception ex)
        {
            Notification = $"Pricing failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RequestApprovalAsync()
    {
        if (!CanRequestApproval() || !Form.AgreementId.HasValue)
        {
            Notification ??= "Approval request unavailable.";
            return;
        }

        var agreementId = Form.AgreementId.Value;
        var approver = ApprovalApprover.Trim();

        IsBusy = true;

        try
        {
            await _agreementService.RequestApprovalAsync(agreementId, approver);
            Notification = $"Approval requested from {approver}.";
        }
        catch (Exception ex)
        {
            Notification = $"Approval request failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        ApprovalApprover = string.Empty;
        await RefreshAsync(agreementId);
    }

    private async Task CompleteApprovalAsync(string decision)
    {
        if (!CanCompleteApproval() || !Form.AgreementId.HasValue || SelectedApproval?.AgreementApprovalId is null)
        {
            Notification ??= "Select a pending approval to continue.";
            return;
        }

        var agreementId = Form.AgreementId.Value;
        var approvalId = SelectedApproval.AgreementApprovalId.Value;

        IsBusy = true;

        try
        {
            await _agreementService.CompleteApprovalAsync(agreementId, approvalId, decision, ApprovalComments);
            Notification = decision == ApprovalDecision.Approved ? "Agreement approved." : "Agreement rejected.";
        }
        catch (Exception ex)
        {
            Notification = $"Updating approval failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync(agreementId);
    }

    private async Task ScheduleRenewalAsync()
    {
        if (!CanScheduleRenewal() || !Form.AgreementId.HasValue || !Form.RenewalDate.HasValue)
        {
            Notification ??= "Set a renewal date first.";
            return;
        }

        var agreementId = Form.AgreementId.Value;
        var renewalDate = DateOnly.FromDateTime(Form.RenewalDate.Value.Date);

        IsBusy = true;

        try
        {
            await _agreementService.ScheduleRenewalAsync(agreementId, renewalDate, Form.AutoRenew);
            Notification = "Renewal scheduled.";
        }
        catch (Exception ex)
        {
            Notification = $"Scheduling renewal failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync(agreementId);
    }

    private async Task MarkRenewedAsync()
    {
        if (!CanMarkRenewed() || !Form.AgreementId.HasValue || !Form.LastRenewedOn.HasValue)
        {
            Notification ??= "Enter the renewal completion date.";
            return;
        }

        var agreementId = Form.AgreementId.Value;
        var renewedOn = DateOnly.FromDateTime(Form.LastRenewedOn.Value.Date);

        IsBusy = true;

        try
        {
            await _agreementService.MarkRenewedAsync(agreementId, renewedOn);
            Notification = "Agreement renewal recorded.";
        }
        catch (Exception ex)
        {
            Notification = $"Renewal update failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        await RefreshAsync(agreementId);
    }

    private bool CanSave()
    {
        return !IsBusy
            && Form.IsValid
            && Rates.Count > 0
            && Rates.All(r => r.IsValid)
            && ImpactedParties.All(p => p.IsValid || p.IsEmpty);
    }

    private bool CanCalculate()
    {
        return !IsBusy && SelectedAgreement is not null && (Rates.Count > 0);
    }

    private bool CanRequestApproval()
    {
        return !IsBusy && Form.AgreementId.HasValue && !string.IsNullOrWhiteSpace(ApprovalApprover);
    }

    private bool CanCompleteApproval()
    {
        return !IsBusy && Form.AgreementId.HasValue && SelectedApproval is not null && SelectedApproval.IsPending;
    }

    private bool CanScheduleRenewal()
    {
        return !IsBusy && Form.AgreementId.HasValue && Form.RenewalDate.HasValue;
    }

    private bool CanMarkRenewed()
    {
        return !IsBusy && Form.AgreementId.HasValue && Form.LastRenewedOn.HasValue;
    }

    private void ClearRates()
    {
        foreach (var rate in Rates)
        {
            rate.PropertyChanged -= RateOnPropertyChanged;
        }

        Rates.Clear();
        SelectedRate = null;
        UpdateCommandStates();
    }

    private void ClearImpactedParties()
    {
        foreach (var party in ImpactedParties)
        {
            party.PropertyChanged -= ImpactedPartyOnPropertyChanged;
        }

        ImpactedParties.Clear();
        SelectedImpactedParty = null;
        UpdateCommandStates();
    }

    private void OnRatesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (AgreementRateModel rate in e.OldItems)
            {
                rate.PropertyChanged -= RateOnPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (AgreementRateModel rate in e.NewItems)
            {
                rate.PropertyChanged += RateOnPropertyChanged;
            }
        }

        UpdateCommandStates();
    }

    private void OnImpactedPartiesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var impacted in ImpactedParties)
            {
                impacted.PropertyChanged -= ImpactedPartyOnPropertyChanged;
            }

            foreach (var impacted in ImpactedParties)
            {
                impacted.PropertyChanged += ImpactedPartyOnPropertyChanged;
            }

            UpdateCommandStates();
            return;
        }

        if (e.OldItems is not null)
        {
            foreach (AgreementImpactedPartyModel impacted in e.OldItems)
            {
                impacted.PropertyChanged -= ImpactedPartyOnPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (AgreementImpactedPartyModel impacted in e.NewItems)
            {
                impacted.PropertyChanged += ImpactedPartyOnPropertyChanged;
            }
        }

        UpdateCommandStates();
    }

    private void RateOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateCommandStates();
    }

    private void ImpactedPartyOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_suppressLookupRefresh && sender is AgreementImpactedPartyModel impacted)
        {
            if (e.PropertyName == nameof(AgreementImpactedPartyModel.PartyName) && ReferenceEquals(impacted, SelectedImpactedParty))
            {
                _ = RefreshPartyLookupAsync(impacted);
            }
        }

        UpdateCommandStates();
    }

    private void OnApprovalsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var approval in Approvals)
            {
                approval.PropertyChanged -= ApprovalOnPropertyChanged;
            }

            UpdateCommandStates();
            return;
        }

        if (e.OldItems is not null)
        {
            foreach (AgreementApprovalModel approval in e.OldItems)
            {
                approval.PropertyChanged -= ApprovalOnPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (AgreementApprovalModel approval in e.NewItems)
            {
                approval.PropertyChanged += ApprovalOnPropertyChanged;
            }
        }

        UpdateCommandStates();
    }

    private void ApprovalOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AgreementApprovalModel.Decision))
        {
            UpdateCommandStates();
        }
    }

    private void UpdateCommandStates()
    {
        _searchCommand.RaiseCanExecuteChanged();
        _saveCommand.RaiseCanExecuteChanged();
        _calculateCommand.RaiseCanExecuteChanged();
        _addRateCommand.RaiseCanExecuteChanged();
        _removeRateCommand.RaiseCanExecuteChanged();
        _addImpactedPartyCommand.RaiseCanExecuteChanged();
        _removeImpactedPartyCommand.RaiseCanExecuteChanged();
        _requestApprovalCommand.RaiseCanExecuteChanged();
        _approveApprovalCommand.RaiseCanExecuteChanged();
        _rejectApprovalCommand.RaiseCanExecuteChanged();
        _scheduleRenewalCommand.RaiseCanExecuteChanged();
        _markRenewedCommand.RaiseCanExecuteChanged();
    }

    private void AppendPricingHistoryEntry(PricingHistoryModel entry, bool trackRun = true)
    {
        PricingHistory.Insert(0, entry);
        if (PricingHistory.Count > PricingHistoryCapacity)
        {
            PricingHistory.RemoveAt(PricingHistory.Count - 1);
        }

        if (trackRun)
        {
            Dashboard.RegisterPricingRun();
        }
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}

public sealed class AgreementDashboardModel : INotifyPropertyChanged
{
    private int _totalAgreements;
    private int _activeAgreements;
    private int _expiringSoon;
    private int _pendingApprovals;
    private int _renewalsDueSoon;
    private int _totalRates;
    private decimal _averageDiscountPercent;
    private decimal _averageCopayPercent;
    private int _pricingRunsToday;

    public int TotalAgreements
    {
        get => _totalAgreements;
        private set => SetProperty(ref _totalAgreements, value);
    }

    public int ActiveAgreements
    {
        get => _activeAgreements;
        private set => SetProperty(ref _activeAgreements, value);
    }

    public int ExpiringSoon
    {
        get => _expiringSoon;
        private set => SetProperty(ref _expiringSoon, value);
    }

    public int TotalRates
    {
        get => _totalRates;
        private set => SetProperty(ref _totalRates, value);
    }

    public int PendingApprovals
    {
        get => _pendingApprovals;
        private set => SetProperty(ref _pendingApprovals, value);
    }

    public int RenewalsDueSoon
    {
        get => _renewalsDueSoon;
        private set => SetProperty(ref _renewalsDueSoon, value);
    }

    public decimal AverageDiscountPercent
    {
        get => _averageDiscountPercent;
        private set => SetProperty(ref _averageDiscountPercent, value);
    }

    public decimal AverageCopayPercent
    {
        get => _averageCopayPercent;
        private set => SetProperty(ref _averageCopayPercent, value);
    }

    public int PricingRunsToday
    {
        get => _pricingRunsToday;
        private set => SetProperty(ref _pricingRunsToday, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Apply(AgreementDashboardDto snapshot)
    {
        TotalAgreements = snapshot.TotalAgreements;
        ActiveAgreements = snapshot.ActiveAgreements;
        ExpiringSoon = snapshot.ExpiringSoon;
        PendingApprovals = snapshot.PendingApprovals;
        RenewalsDueSoon = snapshot.RenewalsDueSoon;
        TotalRates = snapshot.TotalRates;
        AverageDiscountPercent = snapshot.AverageDiscountPercent;
        AverageCopayPercent = snapshot.AverageCopayPercent;
        PricingRunsToday = 0;
    }

    public void RegisterPricingRun()
    {
        PricingRunsToday += 1;
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

public sealed class TriageHeatMapBucketModel
{
    public TriageHeatMapBucketModel(AgreementApprovalHeatMapBucketDto dto)
    {
        if (dto is null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        Label = dto.Label;
        PendingCount = dto.PendingCount;
        PotentialValue = dto.PotentialValue;
        HeatBrush = ResolveBrush(PendingCount, PotentialValue);
    }

    public string Label { get; }
    public int PendingCount { get; }
    public decimal PotentialValue { get; }
    public Brush HeatBrush { get; }

    public string PendingDisplay => PendingCount switch
    {
        0 => "No approvals",
        1 => "1 approval",
        _ => FormattableString.Invariant($"{PendingCount} approvals")
    };

    public string PotentialDisplay => PotentialValue <= 0m
        ? "No value scored"
        : PotentialValue.ToString("C0", CultureInfo.CurrentCulture);

    private static Brush ResolveBrush(int pendingCount, decimal potentialValue)
    {
        var normalizedPotential = (double)Math.Min(6m, Math.Round(potentialValue / 2500m, 2, MidpointRounding.AwayFromZero));
        var weightedScore = pendingCount + normalizedPotential;

        if (weightedScore >= 6d)
        {
            return Brushes.IndianRed;
        }

        if (weightedScore >= 3d)
        {
            return Brushes.Orange;
        }

        if (weightedScore >= 1d)
        {
            return Brushes.Goldenrod;
        }

        return Brushes.LightGreen;
    }
}

public sealed class TriageActionInsightModel
{
    public TriageActionInsightModel(ApprovalActionInsightDto dto)
    {
        if (dto is null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        ActionType = dto.ActionType;
        CountLastSevenDays = dto.CountLastSevenDays;
        CountLastThirtyDays = dto.CountLastThirtyDays;
        MostRecentActionOnUtc = dto.MostRecentActionOnUtc;
    }

    public string ActionType { get; }
    public int CountLastSevenDays { get; }
    public int CountLastThirtyDays { get; }
    public DateTime? MostRecentActionOnUtc { get; }

    public string Title => ActionType switch
    {
        ActionLogType.FastTrack => "Fast-Track Escalations",
        ActionLogType.Reminder => "Reminder Burndown",
        _ => ActionType
    };

    public string WeeklyDeltaDisplay => CountLastSevenDays switch
    {
        0 => "No activity in the past 7 days",
        1 => "1 action in the past 7 days",
        _ => FormattableString.Invariant($"{CountLastSevenDays} actions in the past 7 days")
    };

    public string MonthlyTotalDisplay => CountLastThirtyDays switch
    {
        0 => "No activity in the past 30 days",
        1 => "1 action in the past 30 days",
        _ => FormattableString.Invariant($"{CountLastThirtyDays} actions in the past 30 days")
    };

    public string MostRecentDisplay => MostRecentActionOnUtc.HasValue
        ? FormattableString.Invariant($"Most recent {MostRecentActionOnUtc.Value.ToLocalTime():g}")
        : "No activity recorded";

    public Brush TrendBrush => CountLastSevenDays switch
    {
        >= 5 => Brushes.IndianRed,
        >= 3 => Brushes.DarkOrange,
        >= 1 => Brushes.Goldenrod,
        _ => Brushes.SlateGray
    };
}

public sealed class ApprovalTriageCardModel
{
    private readonly AsyncRelayCommand _remindCommand;
    private readonly AsyncRelayCommand _fastTrackCommand;

    public ApprovalTriageCardModel(AgreementApprovalTriageDto dto, Func<Task> remindAction, Func<Task> fastTrackAction)
    {
        if (dto is null)
        {
            throw new ArgumentNullException(nameof(dto));
        }

        AgreementId = dto.AgreementId;
        ApprovalId = dto.AgreementApprovalId;
        AgreementName = dto.AgreementName;
        Approver = dto.Approver;
        PayerName = dto.PayerName;
        ImpactedIdentities = dto.ImpactedIdentities;
        ImpactedPartyNames = dto.ImpactedPartyNames ?? Array.Empty<string>();
        PotentialValue = dto.PotentialValue;
        RequestedOnUtc = dto.RequestedOnUtc;
        EscalatedOnUtc = dto.EscalatedOnUtc;

        _remindCommand = new AsyncRelayCommand(remindAction ?? throw new ArgumentNullException(nameof(remindAction)));
        _fastTrackCommand = new AsyncRelayCommand(fastTrackAction ?? throw new ArgumentNullException(nameof(fastTrackAction)));
    }

    public Guid AgreementId { get; }
    public Guid ApprovalId { get; }
    public string AgreementName { get; }
    public string Approver { get; }
    public string PayerName { get; }
    public int ImpactedIdentities { get; }
    public IReadOnlyList<string> ImpactedPartyNames { get; }
    public decimal PotentialValue { get; }
    public DateTime RequestedOnUtc { get; }
    public DateTime? EscalatedOnUtc { get; }

    public string ImpactSummary
    {
        get
        {
            if (ImpactedPartyNames is { Count: > 0 })
            {
                var headline = string.Join(", ", ImpactedPartyNames.Take(3));
                if (ImpactedPartyNames.Count > 3)
                {
                    headline += FormattableString.Invariant($" +{ImpactedPartyNames.Count - 3}");
                }

                return headline;
            }

            var normalizedCount = ImpactedIdentities <= 0 ? 1 : ImpactedIdentities;
            var noun = normalizedCount == 1 ? "client" : "clients";
            return $"Impacts {normalizedCount} {noun} via {PayerName}";
        }
    }

    public string ImpactDetails => ImpactedPartyNames is { Count: > 0 }
        ? string.Join(", ", ImpactedPartyNames)
        : string.Empty;

    public string AgingDisplay
    {
        get
        {
            var age = DateTime.UtcNow - RequestedOnUtc;
            if (age.TotalHours < 1)
            {
                return "Requested <1 hour ago";
            }

            if (age.TotalDays < 1)
            {
                return $"Waiting {Math.Round(age.TotalHours)} hours";
            }

            return $"Waiting {Math.Round(age.TotalDays)} days";
        }
    }

    public string PotentialValueDisplay => PotentialValue <= 0m
        ? "Unscored value"
        : PotentialValue.ToString("C0", CultureInfo.CurrentCulture);

    public bool IsEscalated => EscalatedOnUtc.HasValue;
    public bool CanFastTrack => !IsEscalated;

    public ICommand RemindCommand => _remindCommand;
    public ICommand FastTrackCommand => _fastTrackCommand;
}

public sealed class PricingHistoryModel
{
    public PricingHistoryModel(DateTime timestamp, string serviceCode, decimal listPrice, int quantity, decimal netAmount, decimal discountAmount, decimal copayAmount)
    {
        Timestamp = timestamp;
        ServiceCode = serviceCode;
        ListPrice = listPrice;
        Quantity = quantity;
        NetAmount = netAmount;
        DiscountAmount = discountAmount;
        CopayAmount = copayAmount;
    }

    public DateTime Timestamp { get; }
    public string TimestampDisplay => Timestamp.ToString("t", CultureInfo.CurrentCulture);
    public string ServiceCode { get; }
    public decimal ListPrice { get; }
    public string ListPriceDisplay => ListPrice.ToString("C2", CultureInfo.CurrentCulture);
    public int Quantity { get; }
    public decimal NetAmount { get; }
    public string NetDisplay => NetAmount.ToString("C2", CultureInfo.CurrentCulture);
    public decimal DiscountAmount { get; }
    public string DiscountDisplay => DiscountAmount.ToString("C2", CultureInfo.CurrentCulture);
    public decimal CopayAmount { get; }
    public string CopayDisplay => CopayAmount.ToString("C2", CultureInfo.CurrentCulture);
}

public sealed record AgreementSummaryModel(Guid AgreementId, string AgreementName, string PayerName, DateOnly EffectiveFrom, DateOnly? EffectiveTo, int RateCount, string Status, DateOnly? RenewalDate);

public sealed class AgreementApprovalModel : INotifyPropertyChanged
{
    private Guid? _agreementApprovalId;
    private string _approver = string.Empty;
    private string _decision = ApprovalDecision.Pending;
    private string? _comments;
    private DateTime _requestedOn = DateTime.Now;
    private DateTime? _decidedOn;

    public Guid? AgreementApprovalId
    {
        get => _agreementApprovalId;
        set => SetProperty(ref _agreementApprovalId, value);
    }

    public string Approver
    {
        get => _approver;
        set => SetProperty(ref _approver, value);
    }

    public string Decision
    {
        get => _decision;
        set => SetProperty(ref _decision, value);
    }

    public string? Comments
    {
        get => _comments;
        set => SetProperty(ref _comments, value);
    }

    public DateTime RequestedOn
    {
        get => _requestedOn;
        set => SetProperty(ref _requestedOn, value);
    }

    public DateTime? DecidedOn
    {
        get => _decidedOn;
        set => SetProperty(ref _decidedOn, value);
    }

    public bool IsPending => Decision == ApprovalDecision.Pending;
    public string RequestedOnDisplay => RequestedOn.ToString("g", CultureInfo.CurrentCulture);
    public string? DecidedOnDisplay => DecidedOn?.ToString("g", CultureInfo.CurrentCulture);

    public event PropertyChangedEventHandler? PropertyChanged;

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

public sealed class AgreementFormModel : INotifyPropertyChanged
{
    private Guid? _agreementId;
    private string _agreementName = string.Empty;
    private string _payerName = string.Empty;
    private string _coverageType = "Insurance";
    private DateTime _effectiveFrom = DateTime.Today;
    private DateTime? _effectiveTo;
    private string _status = AgreementStatus.Draft;
    private bool _requiresApproval;
    private bool _autoRenew;
    private DateTime? _renewalDate;
    private DateTime? _lastRenewedOn;

    public Guid? AgreementId
    {
        get => _agreementId;
        set => SetProperty(ref _agreementId, value);
    }

    public string AgreementName
    {
        get => _agreementName;
        set => SetProperty(ref _agreementName, value);
    }

    public string PayerName
    {
        get => _payerName;
        set => SetProperty(ref _payerName, value);
    }

    public string CoverageType
    {
        get => _coverageType;
        set => SetProperty(ref _coverageType, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool RequiresApproval
    {
        get => _requiresApproval;
        set => SetProperty(ref _requiresApproval, value);
    }

    public bool AutoRenew
    {
        get => _autoRenew;
        set => SetProperty(ref _autoRenew, value);
    }

    public DateTime EffectiveFrom
    {
        get => _effectiveFrom;
        set => SetProperty(ref _effectiveFrom, value);
    }

    public DateTime? EffectiveTo
    {
        get => _effectiveTo;
        set => SetProperty(ref _effectiveTo, value);
    }

    public DateTime? RenewalDate
    {
        get => _renewalDate;
        set => SetProperty(ref _renewalDate, value);
    }

    public DateTime? LastRenewedOn
    {
        get => _lastRenewedOn;
        set => SetProperty(ref _lastRenewedOn, value);
    }

    public bool IsValid =>
        !string.IsNullOrWhiteSpace(AgreementName) &&
        !string.IsNullOrWhiteSpace(PayerName) &&
        !string.IsNullOrWhiteSpace(CoverageType) &&
        !string.IsNullOrWhiteSpace(Status);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Reset()
    {
        AgreementId = null;
        AgreementName = string.Empty;
        PayerName = string.Empty;
        CoverageType = "Insurance";
        Status = AgreementStatus.Draft;
        RequiresApproval = false;
        AutoRenew = false;
        EffectiveFrom = DateTime.Today;
        EffectiveTo = null;
        RenewalDate = null;
        LastRenewedOn = null;
    }

    public void LoadFrom(AgreementDetailDto detail)
    {
        AgreementId = detail.AgreementId;
        AgreementName = detail.AgreementName;
        PayerName = detail.PayerName;
        CoverageType = detail.CoverageType;
        Status = detail.Status;
        RequiresApproval = detail.RequiresApproval;
        AutoRenew = detail.AutoRenew;
        EffectiveFrom = detail.EffectiveFrom.ToDateTime(TimeOnly.MinValue);
        EffectiveTo = detail.EffectiveTo?.ToDateTime(TimeOnly.MinValue);
        RenewalDate = detail.RenewalDate?.ToDateTime(TimeOnly.MinValue);
        LastRenewedOn = detail.LastRenewedOn?.ToDateTime(TimeOnly.MinValue);
    }

    public AgreementDetailDto ToDetail(IEnumerable<AgreementRateModel> rates, IEnumerable<AgreementApprovalModel> approvals, IEnumerable<AgreementImpactedPartyModel> impactedParties)
    {
        var ratesList = rates
            .Select(r => new AgreementRateDto(r.AgreementRateId, r.ServiceCode, r.BaseAmount, r.DiscountPercent, r.CopayPercent))
            .ToList();

        var approvalsList = approvals
            .Select(a => new AgreementApprovalDto(
                a.AgreementApprovalId,
                a.Approver,
                a.Decision,
                a.Comments,
                a.RequestedOn.ToUniversalTime(),
                a.DecidedOn?.ToUniversalTime()))
            .ToList();

        var impactedList = impactedParties
            .Where(p => p.IsValid)
            .Select(p => new AgreementImpactedPartyDto(
                p.AgreementImpactedPartyId,
                p.PartyId!.Value,
                p.PartyName,
                p.PartyType,
                p.Relationship))
            .ToList();

        return new AgreementDetailDto(
            AgreementId,
            AgreementName,
            PayerName,
            CoverageType,
            DateOnly.FromDateTime(EffectiveFrom.Date),
            EffectiveTo.HasValue ? DateOnly.FromDateTime(EffectiveTo.Value.Date) : null,
            ratesList,
            Status,
            RequiresApproval,
            AutoRenew,
            RenewalDate.HasValue ? DateOnly.FromDateTime(RenewalDate.Value.Date) : null,
            LastRenewedOn.HasValue ? DateOnly.FromDateTime(LastRenewedOn.Value.Date) : null,
            approvalsList,
            impactedList);
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

public sealed class AgreementRateModel : INotifyPropertyChanged
{
    private Guid? _agreementRateId;
    private string _serviceCode = string.Empty;
    private decimal _baseAmount;
    private decimal _discountPercent;
    private decimal? _copayPercent;

    public Guid? AgreementRateId
    {
        get => _agreementRateId;
        set => SetProperty(ref _agreementRateId, value);
    }

    public string ServiceCode
    {
        get => _serviceCode;
        set => SetProperty(ref _serviceCode, value);
    }

    public decimal BaseAmount
    {
        get => _baseAmount;
        set => SetProperty(ref _baseAmount, value);
    }

    public decimal DiscountPercent
    {
        get => _discountPercent;
        set => SetProperty(ref _discountPercent, value);
    }

    public decimal? CopayPercent
    {
        get => _copayPercent;
        set => SetProperty(ref _copayPercent, value);
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(ServiceCode);

    public event PropertyChangedEventHandler? PropertyChanged;

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

public sealed class AgreementImpactedPartyModel : INotifyPropertyChanged
{
    private Guid? _agreementImpactedPartyId;
    private Guid? _partyId;
    private string _partyName = string.Empty;
    private string _partyType = string.Empty;
    private string _relationship = string.Empty;
    private PartyLookupResultModel? _selectedLookup;

    public Guid? AgreementImpactedPartyId
    {
        get => _agreementImpactedPartyId;
        set => SetProperty(ref _agreementImpactedPartyId, value);
    }

    public Guid? PartyId
    {
        get => _partyId;
        set
        {
            if (SetProperty(ref _partyId, value))
            {
                OnPropertyChanged(nameof(IsValid));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public string PartyName
    {
        get => _partyName;
        set
        {
            if (SetProperty(ref _partyName, value?.Trim() ?? string.Empty))
            {
                OnPropertyChanged(nameof(IsValid));
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public string PartyType
    {
        get => _partyType;
        set => SetProperty(ref _partyType, value?.Trim() ?? string.Empty);
    }

    public string Relationship
    {
        get => _relationship;
        set
        {
            var normalized = value?.Trim() ?? string.Empty;
            if (SetProperty(ref _relationship, normalized))
            {
                OnPropertyChanged(nameof(IsEmpty));
            }
        }
    }

    public PartyLookupResultModel? SelectedLookup
    {
        get => _selectedLookup;
        set
        {
            if (SetProperty(ref _selectedLookup, value) && value is not null)
            {
                PartyId = value.PartyId;
                PartyName = value.DisplayName;
                PartyType = value.PartyType;
            }
        }
    }

    public bool IsValid => PartyId.HasValue && !string.IsNullOrWhiteSpace(PartyName);

    public bool IsEmpty => !PartyId.HasValue && string.IsNullOrWhiteSpace(PartyName) && string.IsNullOrWhiteSpace(Relationship);

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        if (!string.IsNullOrWhiteSpace(propertyName))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        return true;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class PartyLookupResultModel
{
    public PartyLookupResultModel(Guid partyId, string displayName, string partyType, string? identifier, string relationshipContext)
    {
        PartyId = partyId;
        DisplayName = displayName;
        PartyType = string.IsNullOrWhiteSpace(partyType) ? "Identity" : partyType;
        Identifier = identifier;
        RelationshipContext = string.IsNullOrWhiteSpace(relationshipContext) ? PartyType : relationshipContext;
    }

    public Guid PartyId { get; }
    public string DisplayName { get; }
    public string PartyType { get; }
    public string? Identifier { get; }
    public string RelationshipContext { get; }

    public string ContextSummary => string.IsNullOrWhiteSpace(Identifier)
        ? RelationshipContext
        : FormattableString.Invariant($"{RelationshipContext} · {Identifier}");

    public string DisplayLabel => string.IsNullOrWhiteSpace(Identifier)
        ? FormattableString.Invariant($"{DisplayName} · {RelationshipContext}")
        : FormattableString.Invariant($"{DisplayName} · {RelationshipContext} [{Identifier}]");
}

public sealed class PricingRequestModel : INotifyPropertyChanged
{
    private string _serviceCode = string.Empty;
    private decimal _listPrice;
    private int _quantity = 1;
    private PricingResultModel _result = new(0m, 0m, 0m);

    public string ServiceCode
    {
        get => _serviceCode;
        set => SetProperty(ref _serviceCode, value);
    }

    public decimal ListPrice
    {
        get => _listPrice;
        set => SetProperty(ref _listPrice, value);
    }

    public int Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    public PricingResultModel Result
    {
        get => _result;
        set => SetProperty(ref _result, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Reset()
    {
        ServiceCode = string.Empty;
        ListPrice = 0m;
        Quantity = 1;
        Result = new PricingResultModel(0m, 0m, 0m);
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

public sealed class PricingResultModel : INotifyPropertyChanged
{
    private decimal _netAmount;
    private decimal _discountAmount;
    private decimal _copayAmount;

    public PricingResultModel(decimal netAmount, decimal discountAmount, decimal copayAmount)
    {
        _netAmount = netAmount;
        _discountAmount = discountAmount;
        _copayAmount = copayAmount;
    }

    public decimal NetAmount
    {
        get => _netAmount;
        set => SetProperty(ref _netAmount, value);
    }

    public decimal DiscountAmount
    {
        get => _discountAmount;
        set => SetProperty(ref _discountAmount, value);
    }

    public decimal CopayAmount
    {
        get => _copayAmount;
        set => SetProperty(ref _copayAmount, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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
