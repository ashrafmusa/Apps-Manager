using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ExcellCore.Domain.Services;
using ExcellCore.Module.Core.Inventory.Commands;

namespace ExcellCore.Module.Core.Inventory.ViewModels;

public sealed class InventoryWorkspaceViewModel : INotifyPropertyChanged
{
    private readonly AsyncRelayCommand _refreshCommand;
    private readonly IInventoryAnalyticsService _inventoryAnalyticsService;
    private readonly List<StockItemModel> _allItems = new();
    private string? _selectedLocation;
    private string _searchText = string.Empty;
    private StockItemModel? _selectedItem;
    private InventorySummaryModel _summary = InventorySummaryModel.Empty;
    private string _statusMessage = "Loading inventory...";
    private string _lastAnalyzed = string.Empty;
    private bool _isBusy;

    public InventoryWorkspaceViewModel(IInventoryAnalyticsService inventoryAnalyticsService)
    {
        _inventoryAnalyticsService = inventoryAnalyticsService ?? throw new ArgumentNullException(nameof(inventoryAnalyticsService));
        Locations = new ObservableCollection<string>();
        StockLedger = new ObservableCollection<StockItemModel>();
        AnomalyAlerts = new ObservableCollection<InventoryAnomalySignalModel>();

        _refreshCommand = new AsyncRelayCommand(LoadAsync, () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> Locations { get; }

    public ObservableCollection<StockItemModel> StockLedger { get; }

    public ObservableCollection<InventoryAnomalySignalModel> AnomalyAlerts { get; }

    public ICommand RefreshCommand => _refreshCommand;

    public string? SelectedLocation
    {
        get => _selectedLocation;
        set
        {
            if (SetProperty(ref _selectedLocation, value))
            {
                ApplyFilters();
            }
        }
    }

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

    public StockItemModel? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }

    public InventorySummaryModel Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string LastAnalyzed
    {
        get => _lastAnalyzed;
        private set => SetProperty(ref _lastAnalyzed, value);
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
            var snapshot = await _inventoryAnalyticsService.GetStockSnapshotAsync().ConfigureAwait(false);
            var analysis = await _inventoryAnalyticsService.AnalyzeAsync(cancellationToken: default).ConfigureAwait(false);

            BuildFromSnapshot(snapshot);
            PopulateLocations();
            ApplyFilters();
            UpdateAnomalies(analysis.Signals);

            LastAnalyzed = FormattableString.Invariant($"Last analyzed {DateTime.Now:t}");
            StatusMessage = "Inventory insights updated";
        }
        catch (Exception ex)
        {
            StatusMessage = FormattableString.Invariant($"Failed to load inventory: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void BuildFromSnapshot(InventoryStockSnapshotDto snapshot)
    {
        _allItems.Clear();

        foreach (var item in snapshot.Items)
        {
            _allItems.Add(new StockItemModel(
                item.Sku,
                item.ItemName,
                item.Location,
                item.QuantityOnHand,
                item.ReorderPoint,
                item.OnOrder,
                item.LastMovementUtc));
        }
    }

    private void PopulateLocations()
    {
        var locations = _allItems
            .Select(i => i.Location)
            .Distinct()
            .OrderBy(l => l)
            .ToList();

        Locations.Clear();
        Locations.Add("All Locations");
        foreach (var location in locations)
        {
            Locations.Add(location);
        }

        if (SelectedLocation is null || !Locations.Contains(SelectedLocation))
        {
            SelectedLocation = Locations.FirstOrDefault();
        }
    }

    private void ApplyFilters()
    {
        if (_allItems.Count == 0)
        {
            StockLedger.Clear();
            Summary = InventorySummaryModel.Empty;
            return;
        }

        var query = _allItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(SelectedLocation) && SelectedLocation != "All Locations")
        {
            query = query.Where(i => i.Location.Equals(SelectedLocation, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(i => i.Sku.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                                     i.Description.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var filtered = query.OrderBy(i => i.Location).ThenBy(i => i.Sku).ToList();

        StockLedger.Clear();
        foreach (var item in filtered)
        {
            StockLedger.Add(item);
        }

        var totalQuantity = filtered.Sum(i => i.QuantityOnHand);
        var lowStock = filtered.Count(i => i.QuantityOnHand <= i.ReorderPoint);

        Summary = new InventorySummaryModel(filtered.Count, totalQuantity, lowStock);

        if (filtered.Count == 0)
        {
            StatusMessage = "No matching inventory records.";
        }
    }

    private void UpdateAnomalies(IReadOnlyList<InventoryAnomalySignalDto> alerts)
    {
        AnomalyAlerts.Clear();

        foreach (var alert in alerts.OrderByDescending(a => a.Severity).ThenByDescending(a => a.DetectedOnUtc))
        {
            AnomalyAlerts.Add(new InventoryAnomalySignalModel(alert));
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

    public readonly record struct InventorySummaryModel(int TotalSkus, decimal TotalQuantity, int LowStockCount)
    {
        public static InventorySummaryModel Empty { get; } = new(0, 0, 0);
    }

    public sealed record StockItemModel(
        string Sku,
        string Description,
        string Location,
        decimal QuantityOnHand,
        decimal ReorderPoint,
        decimal OnOrder,
        DateTime LastMovement)
    {
        public string ReorderStatus => QuantityOnHand <= ReorderPoint ? "Reorder" : "Healthy";
    }

    public sealed record InventoryAnomalySignalModel(
        string Sku,
        string ItemName,
        string Location,
        string SignalType,
        string Severity,
        double VelocityPerDay,
        decimal QuantityOnHand,
        decimal ReorderPoint,
        string Message,
        DateTime DetectedOnUtc)
    {
        public InventoryAnomalySignalModel(InventoryAnomalySignalDto dto)
            : this(dto.Sku, dto.ItemName, dto.Location, dto.SignalType, dto.Severity, dto.VelocityPerDay, dto.QuantityOnHand, dto.ReorderPoint, dto.Message, dto.DetectedOnUtc)
        {
        }

        public string SeverityTag => Severity;
        public string Detail => FormattableString.Invariant($"{SignalType}: {Message}");
        public string QuantityDisplay => FormattableString.Invariant($"On hand {QuantityOnHand} (RP {ReorderPoint})");
        public string VelocityDisplay => FormattableString.Invariant($"{VelocityPerDay:F1} units/day");
        public string DetectedDisplay => DateTime.SpecifyKind(DetectedOnUtc, DateTimeKind.Utc).ToLocalTime().ToString("g");
        public string ActionHint => ResolveActionHint(SignalType, SeverityTag);

        private static string ResolveActionHint(string signalType, string severity)
        {
            if (signalType.Equals("StockOutRisk", StringComparison.OrdinalIgnoreCase))
            {
                return severity.Equals("Critical", StringComparison.OrdinalIgnoreCase)
                    ? "Create emergency reorder / substitute"
                    : "Review reorder point and place order";
            }

            if (signalType.Equals("Shrinkage", StringComparison.OrdinalIgnoreCase))
            {
                return "Audit location, freeze transfers, check adjustments";
            }

            return "Monitor signal";
        }
    }
}
