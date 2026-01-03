using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using ExcellCore.Domain.Services;
using ExcellCore.Module.IS.Retail.Commands;
using System.Windows.Input;

namespace ExcellCore.Module.IS.Retail.ViewModels;

public sealed class RetailWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly IRetailOperationsService _retailService;
    private bool _initialized;
    private bool _isBusy;
    private RetailSummaryModel _summary = RetailSummaryModel.Empty;
    private string _statusMessage = "Loading retail metrics...";
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly AsyncRelayCommand _suspendCommand;
    private readonly AsyncRelayCommand _resumeCommand;
    private readonly AsyncRelayCommand _recordReturnCommand;

    public RetailWorkspaceViewModel(IRetailOperationsService retailService)
    {
        _retailService = retailService;
        Tickets = new ObservableCollection<RetailTicketModel>();
        Promotions = new ObservableCollection<RetailPromotionModel>();
        SuspendedTickets = new ObservableCollection<RetailSuspendedTicketModel>();
        Returns = new ObservableCollection<RetailReturnModel>();

        _refreshCommand = new AsyncRelayCommand(LoadAsync, () => !_isBusy);
        _suspendCommand = new AsyncRelayCommand(SuspendDemoCartAsync, () => !_isBusy);
        _resumeCommand = new AsyncRelayCommand(ResumeOldestSuspendedAsync, () => !_isBusy && SuspendedTickets.Any());
        _recordReturnCommand = new AsyncRelayCommand(RecordReturnAsync, () => !_isBusy);
    }

    public string Title => "Retail Operations";
    public string Subtitle => "Point-of-sale, loyalty, and storefront tooling.";

    public ObservableCollection<RetailTicketModel> Tickets { get; }

    public ObservableCollection<RetailPromotionModel> Promotions { get; }

    public ObservableCollection<RetailSuspendedTicketModel> SuspendedTickets { get; }

    public ObservableCollection<RetailReturnModel> Returns { get; }

    public ICommand RefreshCommand => _refreshCommand;

    public ICommand SuspendCartCommand => _suspendCommand;

    public ICommand ResumeCartCommand => _resumeCommand;

    public ICommand RecordReturnCommand => _recordReturnCommand;

    public RetailSummaryModel Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                UpdateCommandStates();
            }
        }
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

    private async Task SuspendDemoCartAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var subtotal = 50m + SuspendedTickets.Count * 5m;
            var request = new SuspendTransactionRequest("In-Store", subtotal, null, "Suspended from dashboard", null);
            await _retailService.SuspendAsync(request);
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }

    private async Task ResumeOldestSuspendedAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var target = SuspendedTickets
            .OrderBy(s => s.SuspendedOn)
            .FirstOrDefault(s => s.ResumedOn is null || s.Status.Equals("Suspended", StringComparison.OrdinalIgnoreCase));

        if (target is null)
        {
            StatusMessage = "No suspended carts to resume.";
            return;
        }

        IsBusy = true;
        try
        {
            await _retailService.ResumeAsync(new ResumeTransactionRequest(target.RetailSuspendedTransactionId));
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }

    private async Task RecordReturnAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var ticketNumber = FormattableString.Invariant($"POS-RET-{DateTime.UtcNow:HHmmss}");
            var request = new RecordReturnRequest(ticketNumber, "In-Store", 24.50m, "Workflow return");
            await _retailService.RecordReturnAsync(request);
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }

    private void UpdateCommandStates()
    {
        _refreshCommand.RaiseCanExecuteChanged();
        _suspendCommand.RaiseCanExecuteChanged();
        _resumeCommand.RaiseCanExecuteChanged();
        _recordReturnCommand.RaiseCanExecuteChanged();
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
            StatusMessage = "Loading retail metrics...";

            var dashboard = await _retailService.GetDashboardAsync();

            Tickets.Clear();
            foreach (var ticket in dashboard.Tickets)
            {
                var raisedOn = DateTime.SpecifyKind(ticket.RaisedOnUtc, DateTimeKind.Utc).ToLocalTime();
                Tickets.Add(new RetailTicketModel(ticket.TicketNumber, ticket.Channel, ticket.TotalAmount, ticket.Status, raisedOn));
            }

            Promotions.Clear();
            foreach (var promo in dashboard.Promotions)
            {
                var endsOn = DateTime.SpecifyKind(promo.EndsOnUtc, DateTimeKind.Utc).ToLocalTime();
                Promotions.Add(new RetailPromotionModel(promo.Name, promo.Description, endsOn));
            }

            SuspendedTickets.Clear();
            foreach (var suspended in dashboard.Suspended)
            {
                var suspendedOn = DateTime.SpecifyKind(suspended.SuspendedOnUtc, DateTimeKind.Utc).ToLocalTime();
                var resumedOn = suspended.ResumedOnUtc.HasValue
                    ? DateTime.SpecifyKind(suspended.ResumedOnUtc.Value, DateTimeKind.Utc).ToLocalTime()
                    : (DateTime?)null;

                SuspendedTickets.Add(new RetailSuspendedTicketModel(
                    suspended.RetailSuspendedTransactionId,
                    suspended.TicketNumber,
                    suspended.Channel,
                    suspended.Subtotal,
                    suspended.Status,
                    suspendedOn,
                    resumedOn));
            }

            Returns.Clear();
            foreach (var item in dashboard.Returns)
            {
                var returnedOn = DateTime.SpecifyKind(item.ReturnedOnUtc, DateTimeKind.Utc).ToLocalTime();
                Returns.Add(new RetailReturnModel(item.RetailReturnId, item.TicketNumber, item.Channel, item.Amount, item.Status, item.Reason, returnedOn));
            }

            Summary = new RetailSummaryModel(dashboard.Summary.DailySales, dashboard.Summary.OpenOrders, dashboard.Summary.LoyaltyEnrollments, dashboard.Summary.SuspendedCount, dashboard.Summary.ReturnsToday);
            StatusMessage = $"Last refreshed {DateTime.Now:t}";
        }
        finally
        {
            IsBusy = false;
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

public sealed record RetailSummaryModel(decimal DailySales, int OpenOrders, int LoyaltyEnrollments, int SuspendedCount, int ReturnsToday)
{
    public static RetailSummaryModel Empty { get; } = new(0m, 0, 0, 0, 0);
}

public sealed record RetailTicketModel(string TicketNumber, string Channel, decimal TotalAmount, string Status, DateTime RaisedOn)
{
    public string RaisedDisplay => RaisedOn.ToString("t");
    public string TotalDisplay => TotalAmount.ToString("C");
}

public sealed record RetailPromotionModel(string Name, string Description, DateTime EndsOn)
{
    public string EndsDisplay => EndsOn.ToString("d");
}

public sealed record RetailSuspendedTicketModel(Guid RetailSuspendedTransactionId, string TicketNumber, string Channel, decimal Subtotal, string Status, DateTime SuspendedOn, DateTime? ResumedOn)
{
    public string SuspendedDisplay => SuspendedOn.ToString("g");
    public string ResumedDisplay => ResumedOn?.ToString("g") ?? "Pending";
    public string SubtotalDisplay => Subtotal.ToString("C");
}

public sealed record RetailReturnModel(Guid RetailReturnId, string TicketNumber, string Channel, decimal Amount, string Status, string Reason, DateTime ReturnedOn)
{
    public string AmountDisplay => Amount.ToString("C");
    public string ReturnedDisplay => ReturnedOn.ToString("g");
}
