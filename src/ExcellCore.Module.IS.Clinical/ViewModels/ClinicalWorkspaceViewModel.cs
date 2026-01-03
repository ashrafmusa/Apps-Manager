using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ExcellCore.Domain.Services;
using ExcellCore.Module.IS.Clinical.Commands;

namespace ExcellCore.Module.IS.Clinical.ViewModels;

public sealed class ClinicalWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly IClinicalWorkflowService _clinicalService;
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly RelayCommand _clearFiltersCommand;
    private readonly AsyncRelayCommand _newOrderCommand;
    private readonly AsyncRelayCommand _recordDispenseCommand;
    private readonly AsyncRelayCommand _completeNextDoseCommand;
    private readonly List<MedicationOrderModel> _allOrders = new();
    private readonly List<MedicationAdministrationModel> _allAdministrations = new();
    private readonly List<JourneyEventModel> _allJourneyEvents = new();
    private readonly List<RoleWorklistItemModel> _allWorklistItems = new();
    private readonly List<JourneySignalModel> _allSignals = new();
    private string _searchText = string.Empty;
    private string _selectedStatus = "All Statuses";
    private MedicationOrderModel? _selectedOrder;
    private ClinicalSummaryModel _summary = ClinicalSummaryModel.Empty;
    private string _statusMessage = "Loading clinical workflows...";
    private string _currentPatient = "Patient";
    private bool _isBusy;

    public ClinicalWorkspaceViewModel(IClinicalWorkflowService clinicalService)
    {
        _clinicalService = clinicalService;
        Orders = new ObservableCollection<MedicationOrderModel>();
        Administrations = new ObservableCollection<MedicationAdministrationModel>();
        JourneyTimeline = new ObservableCollection<JourneyEventModel>();
        ProviderWorklist = new ObservableCollection<RoleWorklistItemModel>();
        PharmacistWorklist = new ObservableCollection<RoleWorklistItemModel>();
        NurseWorklist = new ObservableCollection<RoleWorklistItemModel>();
        InboxSignals = new ObservableCollection<JourneySignalModel>();
        StatusFilters = new ObservableCollection<string>(new[] { "All Statuses" });

        _refreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        _newOrderCommand = new AsyncRelayCommand(CreateOrderAsync, () => !IsBusy);
        _recordDispenseCommand = new AsyncRelayCommand(RecordDispenseAsync, () => !IsBusy && SelectedOrder is not null);
        _completeNextDoseCommand = new AsyncRelayCommand(CompleteNextDoseAsync, () => !IsBusy && SelectedOrder is not null);
        _clearFiltersCommand = new RelayCommand(ClearFilters);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Title => "Clinical Suite";

    public string Subtitle => "CPOE + MAR snapshot";

    public ObservableCollection<string> StatusFilters { get; }

    public ObservableCollection<MedicationOrderModel> Orders { get; }

    public ObservableCollection<MedicationAdministrationModel> Administrations { get; }

    public ObservableCollection<JourneyEventModel> JourneyTimeline { get; }

    public ObservableCollection<RoleWorklistItemModel> ProviderWorklist { get; }

    public ObservableCollection<RoleWorklistItemModel> PharmacistWorklist { get; }

    public ObservableCollection<RoleWorklistItemModel> NurseWorklist { get; }

    public ObservableCollection<JourneySignalModel> InboxSignals { get; }

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand ClearFiltersCommand => _clearFiltersCommand;

    public ICommand NewOrderCommand => _newOrderCommand;

    public ICommand RecordDispenseCommand => _recordDispenseCommand;

    public ICommand CompleteNextDoseCommand => _completeNextDoseCommand;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public string SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value))
            {
                ApplyFilters();
            }
        }
    }

    public MedicationOrderModel? SelectedOrder
    {
        get => _selectedOrder;
        set
        {
            if (SetProperty(ref _selectedOrder, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public ClinicalSummaryModel Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string CurrentPatient
    {
        get => _currentPatient;
        private set => SetProperty(ref _currentPatient, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
                UpdateCommandStates();
            }
        }
    }

    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    private void PopulateStatusFilters()
    {
        var statuses = _allOrders
            .Select(o => o.Status)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        StatusFilters.Clear();
        StatusFilters.Add("All Statuses");
        foreach (var status in statuses)
        {
            StatusFilters.Add(status);
        }

        if (!StatusFilters.Contains(SelectedStatus))
        {
            SelectedStatus = StatusFilters.First();
        }
    }

    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            StatusMessage = "Loading clinical workflows...";
            var dashboard = await _clinicalService.GetDashboardAsync();
            var journey = await _clinicalService.GetPatientJourneyAsync();

            _allOrders.Clear();
            _allOrders.AddRange(dashboard.Orders.Select(o => new MedicationOrderModel(
                o.MedicationOrderId,
                o.OrderNumber,
                o.PatientName,
                o.Medication,
                o.Dose,
                o.Route,
                o.Frequency,
                o.Status,
                o.OrderingProvider,
                o.OrderedOnUtc,
                o.StartOnUtc)));

            _allAdministrations.Clear();
            _allAdministrations.AddRange(dashboard.UpcomingAdministrations.Select(a => new MedicationAdministrationModel(
                a.MedicationAdministrationId,
                a.MedicationOrderId,
                a.OrderNumber,
                a.Medication,
                a.ScheduledForUtc,
                a.AdministeredOnUtc,
                a.Status,
                a.PerformedBy)));

            Summary = new ClinicalSummaryModel(
                dashboard.Summary.ActiveOrders,
                dashboard.Summary.PendingAdministrations,
                dashboard.Summary.DispensesToday);

            PopulateStatusFilters();
            ApplyFilters();
            UpdateJourney(journey);
            StatusMessage = $"Updated {DateTime.Now:t}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilters()
    {
        if (_allOrders.Count == 0)
        {
            Orders.Clear();
            Administrations.Clear();
            Summary = ClinicalSummaryModel.Empty;
            StatusMessage = "No clinical data available.";
            return;
        }

        var query = _allOrders.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedStatus) && SelectedStatus != "All Statuses")
        {
            query = query.Where(o => o.Status.Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(o =>
                o.Patient.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                o.OrderNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                o.Medication.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                o.OrderingProvider.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var filteredOrders = query
            .OrderByDescending(o => o.OrderedOn)
            .ToList();

        Orders.Clear();
        foreach (var order in filteredOrders)
        {
            Orders.Add(order);
        }

        var orderIds = filteredOrders
            .Select(o => o.MedicationOrderId)
            .ToHashSet();

        var filteredAdministrations = _allAdministrations
            .Where(a => orderIds.Contains(a.MedicationOrderId))
            .OrderBy(a => a.ScheduledFor)
            .Take(15)
            .ToList();

        Administrations.Clear();
        foreach (var admin in filteredAdministrations)
        {
            Administrations.Add(admin);
        }

        StatusMessage = filteredOrders.Count == 0
            ? "No orders match the current filters."
            : $"Showing {filteredOrders.Count} order(s).";
    }

    private void ClearFilters()
    {
        SearchText = string.Empty;
        SelectedStatus = StatusFilters.FirstOrDefault() ?? "All Statuses";
        ApplyFilters();
    }

    private async Task CreateOrderAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var nowUtc = DateTime.UtcNow;
            var medications = new[] { "Ceftriaxone", "Metformin", "Heparin", "Vancomycin" };
            var routes = new[] { "IV", "PO", "SQ" };
            var med = medications[nowUtc.Millisecond % medications.Length];
            var route = routes[nowUtc.Second % routes.Length];

            var request = new CreateMedicationOrderRequest(
                null,
                FormattableString.Invariant($"Workflow Patient {nowUtc:HHmm}"),
                med,
                "1 unit",
                route,
                "q8h",
                "Dr. Workflow",
                nowUtc.AddMinutes(45),
                null,
                "Active",
                "Auto-generated order",
                true);

            await _clinicalService.CreateOrderAsync(request);
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }

    private async Task RecordDispenseAsync()
    {
        if (IsBusy || SelectedOrder is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var request = new RecordDispenseRequest(
                SelectedOrder.MedicationOrderId,
                FormattableString.Invariant($"{SelectedOrder.Medication.ToUpperInvariant().Replace(" ", string.Empty)}-PACK"),
                1,
                "unit",
                DateTime.UtcNow,
                "Pharmacy",
                "Main Pharmacy");

            await _clinicalService.RecordDispenseAsync(request);
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }

    private async Task CompleteNextDoseAsync()
    {
        if (IsBusy || SelectedOrder is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _clinicalService.CompleteNextAdministrationAsync(SelectedOrder.MedicationOrderId, "RN Workflow");
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }

    private void UpdateCommandStates()
    {
        _newOrderCommand.RaiseCanExecuteChanged();
        _recordDispenseCommand.RaiseCanExecuteChanged();
        _completeNextDoseCommand.RaiseCanExecuteChanged();
    }

    private void UpdateJourney(PatientJourneyDto journey)
    {
        CurrentPatient = journey.PatientName;

        _allJourneyEvents.Clear();
        _allJourneyEvents.AddRange(journey.Events.Select(e => new JourneyEventModel(
            e.EventId,
            e.Type,
            e.Title,
            e.Status,
            e.Detail,
            e.WhenUtc,
            e.SignalSeverity,
            e.SignalMessage)));

        _allWorklistItems.Clear();
        _allWorklistItems.AddRange(journey.Worklist.Select(w => new RoleWorklistItemModel(
            w.Role,
            w.Title,
            w.Status,
            w.DueUtc)));

        _allSignals.Clear();
        _allSignals.AddRange(journey.Signals.Select(s => new JourneySignalModel(
            s.Severity,
            s.Message,
            s.WhenUtc)));

        JourneyTimeline.Clear();
        foreach (var item in _allJourneyEvents.OrderBy(e => e.WhenLocal))
        {
            JourneyTimeline.Add(item);
        }

        ProviderWorklist.Clear();
        foreach (var item in _allWorklistItems.Where(w => w.Role.Equals("Provider", StringComparison.OrdinalIgnoreCase)).OrderByDescending(w => w.DueLocal))
        {
            ProviderWorklist.Add(item);
        }

        PharmacistWorklist.Clear();
        foreach (var item in _allWorklistItems.Where(w => w.Role.Equals("Pharmacist", StringComparison.OrdinalIgnoreCase)).OrderByDescending(w => w.DueLocal))
        {
            PharmacistWorklist.Add(item);
        }

        NurseWorklist.Clear();
        foreach (var item in _allWorklistItems.Where(w => w.Role.Equals("Nurse", StringComparison.OrdinalIgnoreCase)).OrderBy(w => w.DueLocal))
        {
            NurseWorklist.Add(item);
        }

        InboxSignals.Clear();
        foreach (var signal in _allSignals.OrderByDescending(s => s.WhenLocal ?? DateTime.MinValue))
        {
            InboxSignals.Add(signal);
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

    public readonly record struct ClinicalSummaryModel(int ActiveOrders, int PendingAdministrations, int DispensesToday)
    {
        public static ClinicalSummaryModel Empty { get; } = new(0, 0, 0);

        public string PendingDisplay => PendingAdministrations.ToString();
        public string DispensesDisplay => DispensesToday.ToString();
    }

    public sealed record MedicationOrderModel(
        Guid MedicationOrderId,
        string OrderNumber,
        string Patient,
        string Medication,
        string Dose,
        string Route,
        string Frequency,
        string Status,
        string OrderingProvider,
        DateTime OrderedOn,
        DateTime? StartOn)
    {
        public string OrderedDisplay => OrderedOn.ToLocalTime().ToString("g");
        public string StartDisplay => StartOn?.ToLocalTime().ToString("g") ?? "Scheduled";
    }

    public sealed record MedicationAdministrationModel(
        Guid MedicationAdministrationId,
        Guid MedicationOrderId,
        string OrderNumber,
        string Medication,
        DateTime ScheduledFor,
        DateTime? AdministeredOn,
        string Status,
        string? PerformedBy)
    {
        public string ScheduledDisplay => ScheduledFor.ToLocalTime().ToString("g");
        public string AdministeredDisplay => AdministeredOn?.ToLocalTime().ToString("g") ?? "Pending";
    }

    public sealed record JourneyEventModel(
        Guid EventId,
        string Type,
        string Title,
        string Status,
        string Detail,
        DateTime WhenUtc,
        string? SignalSeverity,
        string? SignalMessage)
    {
        public DateTime WhenLocal => DateTime.SpecifyKind(WhenUtc, DateTimeKind.Utc).ToLocalTime();
        public string WhenDisplay => WhenLocal.ToString("g");
        public string TypeDisplay => Type;
    }

    public sealed record RoleWorklistItemModel(
        string Role,
        string Title,
        string Status,
        DateTime DueUtc)
    {
        public DateTime DueLocal => DateTime.SpecifyKind(DueUtc, DateTimeKind.Utc).ToLocalTime();
        public string DueDisplay => DueLocal.ToString("g");
    }

    public sealed record JourneySignalModel(
        string Severity,
        string Message,
        DateTime? WhenUtc)
    {
        public DateTime? WhenLocal => WhenUtc.HasValue ? DateTime.SpecifyKind(WhenUtc.Value, DateTimeKind.Utc).ToLocalTime() : null;
        public string WhenDisplay => WhenLocal?.ToString("g") ?? string.Empty;
    }
}
