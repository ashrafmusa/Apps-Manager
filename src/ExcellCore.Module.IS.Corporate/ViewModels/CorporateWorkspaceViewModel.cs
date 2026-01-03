using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;

namespace ExcellCore.Module.IS.Corporate.ViewModels;

public sealed class CorporateWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly ICorporatePortfolioService _corporateService;
    private bool _initialized;
    private CorporateSummaryModel _summary = CorporateSummaryModel.Empty;
    private string _statusMessage = "Loading corporate metrics...";

    public CorporateWorkspaceViewModel(ICorporatePortfolioService corporateService)
    {
        _corporateService = corporateService;
        ContractBacklog = new ObservableCollection<CorporateContractModel>();
        AllocationAlerts = new ObservableCollection<CorporateAllocationModel>();
    }

    public string Title => "Corporate Console";
    public string Subtitle => "Contract billing, allocations, and program reporting.";

    public ObservableCollection<CorporateContractModel> ContractBacklog { get; }

    public ObservableCollection<CorporateAllocationModel> AllocationAlerts { get; }

    public CorporateSummaryModel Summary
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
        StatusMessage = "Loading corporate metrics...";

        var dashboard = await _corporateService.GetDashboardAsync();

        ContractBacklog.Clear();
        foreach (var contract in dashboard.Contracts)
        {
            var renewalDate = DateTime.SpecifyKind(contract.RenewalDate, DateTimeKind.Utc).ToLocalTime();
            ContractBacklog.Add(new CorporateContractModel(contract.ContractCode, contract.CustomerName, contract.ContractValue, renewalDate, contract.Category));
        }

        AllocationAlerts.Clear();
        foreach (var allocation in dashboard.Allocations)
        {
            AllocationAlerts.Add(new CorporateAllocationModel(allocation.Program, allocation.AllocationRatio, allocation.Status));
        }

        Summary = new CorporateSummaryModel(dashboard.Summary.AnnualizedRevenue, dashboard.Summary.RenewalsDue, dashboard.Summary.AllocationRisks);
        StatusMessage = $"Portfolio refreshed {DateTime.Now:t}";
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

public sealed record CorporateSummaryModel(decimal AnnualizedRevenue, int RenewalsDue, int AllocationRisks)
{
    public static CorporateSummaryModel Empty { get; } = new(0m, 0, 0);
}

public sealed record CorporateContractModel(string ContractCode, string Customer, decimal ContractValue, DateTime RenewalDate, string Category)
{
    public string ValueDisplay => ContractValue.ToString("C0");
    public string RenewalDisplay => RenewalDate.ToString("d");
}

public sealed record CorporateAllocationModel(string Program, decimal Allocation, string Status)
{
    public string AllocationDisplay => $"{Allocation:P0}";
}
