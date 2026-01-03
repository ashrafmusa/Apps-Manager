using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ExcellCore.Module.Core.Financials.Commands;

namespace ExcellCore.Module.Core.Financials.ViewModels;

public sealed class FinancialsWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly RelayCommand _toggleCashflowCommand;
    private readonly List<InvoiceModel> _allInvoices = new();
    private string _searchText = string.Empty;
    private string _selectedStatus = "All";
    private InvoiceModel? _selectedInvoice;
    private FinancialSummaryModel _summary = FinancialSummaryModel.Empty;
    private ObservableCollection<CashflowSnapshot> _cashflow;
    private bool _showCashflow = true;
    private string _statusMessage = "Loading financial data...";
    private bool _isBusy;

    public FinancialsWorkspaceViewModel()
    {
        StatusFilters = new ObservableCollection<string>(new[] { "All", "Open", "Overdue", "Paid" });
        Invoices = new ObservableCollection<InvoiceModel>();
        _cashflow = new ObservableCollection<CashflowSnapshot>();

        _refreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
        _toggleCashflowCommand = new RelayCommand(() => ShowCashflow = !ShowCashflow);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> StatusFilters { get; }

    public ObservableCollection<InvoiceModel> Invoices { get; }

    public ObservableCollection<CashflowSnapshot> Cashflow
    {
        get => _cashflow;
        private set => SetProperty(ref _cashflow, value);
    }

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand ToggleCashflowCommand => _toggleCashflowCommand;

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

    public InvoiceModel? SelectedInvoice
    {
        get => _selectedInvoice;
        set => SetProperty(ref _selectedInvoice, value);
    }

    public FinancialSummaryModel Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public bool ShowCashflow
    {
        get => _showCashflow;
        private set => SetProperty(ref _showCashflow, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                _refreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public async Task InitializeAsync()
    {
        await LoadAsync();
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
            await Task.Delay(150);
            BuildSampleData();
            ApplyFilters();
            LoadCashflow();
            StatusMessage = $"Financials refreshed {DateTime.Now:t}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildSampleData()
    {
        _allInvoices.Clear();
        _allInvoices.AddRange(new[]
        {
            new InvoiceModel("INV-2501", "Northwind Labs", 12500m, DateTime.Today.AddDays(-18), DateTime.Today.AddDays(-3), "Overdue", 0m),
            new InvoiceModel("INV-2502", "Summit Health", 8600m, DateTime.Today.AddDays(-10), DateTime.Today.AddDays(20), "Open", 2500m),
            new InvoiceModel("INV-2503", "City General", 14750m, DateTime.Today.AddDays(-5), DateTime.Today.AddDays(25), "Open", 0m),
            new InvoiceModel("INV-2504", "Brightside Retail", 3220m, DateTime.Today.AddDays(-2), DateTime.Today.AddDays(18), "Open", 0m),
            new InvoiceModel("INV-2505", "Evergreen Holdings", 9200m, DateTime.Today.AddDays(-32), DateTime.Today.AddDays(-2), "Paid", 9200m),
            new InvoiceModel("INV-2506", "Radiant Imaging", 17850m, DateTime.Today.AddDays(-45), DateTime.Today.AddDays(-5), "Paid", 17850m),
            new InvoiceModel("INV-2507", "Blue Horizon", 5600m, DateTime.Today.AddDays(-8), DateTime.Today.AddDays(22), "Open", 0m),
            new InvoiceModel("INV-2508", "Equinox Labs", 4350m, DateTime.Today.AddDays(-15), DateTime.Today.AddDays(-1), "Overdue", 0m)
        });
    }

    private void LoadCashflow()
    {
        var snapshots = new[]
        {
            new CashflowSnapshot(DateTime.Today.AddDays(-6), 18500m, 12200m),
            new CashflowSnapshot(DateTime.Today.AddDays(-5), 14200m, 10800m),
            new CashflowSnapshot(DateTime.Today.AddDays(-4), 16350m, 9400m),
            new CashflowSnapshot(DateTime.Today.AddDays(-3), 22400m, 11800m),
            new CashflowSnapshot(DateTime.Today.AddDays(-2), 19800m, 12500m),
            new CashflowSnapshot(DateTime.Today.AddDays(-1), 17200m, 9800m),
            new CashflowSnapshot(DateTime.Today, 25500m, 13200m)
        };

        Cashflow = new ObservableCollection<CashflowSnapshot>(snapshots);
    }

    private void ApplyFilters()
    {
        if (_allInvoices.Count == 0)
        {
            Invoices.Clear();
            Summary = FinancialSummaryModel.Empty;
            return;
        }

        var query = _allInvoices.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedStatus) && SelectedStatus != "All")
        {
            query = query.Where(i => i.Status.Equals(SelectedStatus, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(i => i.InvoiceNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                     i.Customer.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.OrderByDescending(i => i.InvoiceDate).ToList();

        Invoices.Clear();
        foreach (var invoice in filtered)
        {
            Invoices.Add(invoice);
        }

        var outstanding = filtered.Where(i => i.Status is "Open" or "Overdue").Sum(i => i.BalanceDue);
        var overdueCount = filtered.Count(i => i.Status == "Overdue");
        var collected = filtered.Where(i => i.Status == "Paid" && i.InvoiceDate >= DateTime.Today.AddDays(-7)).Sum(i => i.AmountPaid);

        Summary = new FinancialSummaryModel(outstanding, overdueCount, collected);

        StatusMessage = filtered.Count == 0 ? "No invoices match the current filters." : $"Showing {filtered.Count} invoice(s).";
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

    public readonly record struct FinancialSummaryModel(decimal OutstandingBalance, int OverdueInvoices, decimal Collections7Day)
    {
        public static FinancialSummaryModel Empty { get; } = new(0m, 0, 0m);
    }

    public sealed record InvoiceModel(
        string InvoiceNumber,
        string Customer,
        decimal Amount,
        DateTime InvoiceDate,
        DateTime DueDate,
        string Status,
        decimal AmountPaid)
    {
        public decimal BalanceDue => Math.Max(Amount - AmountPaid, 0m);
        public int DaysOutstanding => (DateTime.Today - InvoiceDate).Days;
    }

    public sealed record CashflowSnapshot(DateTime Date, decimal Receipts, decimal Disbursements)
    {
        public decimal Net => Receipts - Disbursements;
    }
}
