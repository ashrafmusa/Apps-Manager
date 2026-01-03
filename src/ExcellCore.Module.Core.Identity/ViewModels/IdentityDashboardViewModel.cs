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
using ExcellCore.Domain.Services;
using ExcellCore.Module.Core.Identity.Commands;

namespace ExcellCore.Module.Core.Identity.ViewModels;

public sealed class IdentityDashboardViewModel : INotifyPropertyChanged
{
    private readonly IPartyService _partyService;
    private readonly AsyncRelayCommand _searchCommand;
    private readonly AsyncRelayCommand _saveCommand;
    private readonly AsyncRelayCommand _refreshIdentifiersCommand;
    private readonly RelayCommand _newCommand;
    private readonly RelayCommand _addIdentifierCommand;
    private readonly RelayCommand _removeIdentifierCommand;
    private bool _isBusy;
    private string _statusMessage = "Ready";
    private string? _notification;
    private string _searchText = string.Empty;
    private PartySummaryModel? _selectedParty;
    private IdentityIdentifierModel? _selectedIdentifier;

    public IdentityDashboardViewModel(IPartyService partyService)
    {
        _partyService = partyService;

        Parties = new ObservableCollection<PartySummaryModel>();
        PartyTypes = new ObservableCollection<string>(new[] { "Patient", "Client", "Guest", "Corporate" });
        Identifiers = new ObservableCollection<IdentityIdentifierModel>();
        Form = new IdentityFormModel();

        Form.PropertyChanged += (_, _) => UpdateCommandStates();
        Identifiers.CollectionChanged += OnIdentifiersChanged;

        _searchCommand = new AsyncRelayCommand(() => RefreshAsync(), () => !IsBusy);
        _saveCommand = new AsyncRelayCommand(SaveAsync, () => !IsBusy && Form.IsValid);
        _refreshIdentifiersCommand = new AsyncRelayCommand(() => LoadPartyDetailAsync(SelectedParty?.PartyId), () => !IsBusy && SelectedParty is not null);
        _newCommand = new RelayCommand(NewIdentity);
        _addIdentifierCommand = new RelayCommand(AddIdentifier, () => !IsBusy);
        _removeIdentifierCommand = new RelayCommand(RemoveSelectedIdentifier, () => !IsBusy && SelectedIdentifier is not null);

        AddIdentifier();
    }

    public ObservableCollection<PartySummaryModel> Parties { get; }
    public ObservableCollection<string> PartyTypes { get; }
    public ObservableCollection<IdentityIdentifierModel> Identifiers { get; }
    public IdentityFormModel Form { get; }

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

    public PartySummaryModel? SelectedParty
    {
        get => _selectedParty;
        set
        {
            var changed = SetProperty(ref _selectedParty, value);

            if (!changed && value is null)
            {
                ResetDetail();
                return;
            }

            if (!changed && value is not null)
            {
                _ = LoadPartyDetailAsync(value.PartyId);
                return;
            }

            if (value is null)
            {
                ResetDetail();
            }
            else
            {
                _ = LoadPartyDetailAsync(value.PartyId);
            }
        }
    }

    public IdentityIdentifierModel? SelectedIdentifier
    {
        get => _selectedIdentifier;
        set
        {
            if (SetProperty(ref _selectedIdentifier, value))
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
    public ICommand AddIdentifierCommand => _addIdentifierCommand;
    public ICommand RemoveIdentifierCommand => _removeIdentifierCommand;
    public ICommand RefreshDetailCommand => _refreshIdentifiersCommand;

    public event PropertyChangedEventHandler? PropertyChanged;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await RefreshAsync(cancellationToken: cancellationToken);
    }

    private async Task RefreshAsync(Guid? highlightId = null, CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        PartySummaryModel? selection = null;

        try
        {
            var parties = await _partyService.SearchAsync(SearchText, cancellationToken);
            Parties.Clear();
            foreach (var party in parties)
            {
                Parties.Add(new PartySummaryModel(
                    party.PartyId,
                    party.DisplayName,
                    party.PartyType,
                    party.PrimaryIdentifier,
                    party.DateOfBirth));
            }

            StatusMessage = Parties.Count == 0
                ? "No identities found."
                : $"Showing {Parties.Count} identity records.";

            if (highlightId.HasValue)
            {
                selection = Parties.FirstOrDefault(p => p.PartyId == highlightId.Value);
            }
            else if (SelectedParty is not null)
            {
                selection = Parties.FirstOrDefault(p => p.PartyId == SelectedParty.PartyId);
            }
        }
        catch (Exception ex)
        {
            Notification = $"Identity search failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }

        SelectedParty = selection ?? Parties.FirstOrDefault();
    }

    private async Task LoadPartyDetailAsync(Guid? partyId)
    {
        if (partyId is null)
        {
            ResetDetail();
            return;
        }

        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var detail = await _partyService.GetAsync(partyId.Value);
            if (detail is null)
            {
                Notification = "Identity could not be loaded.";
                ResetDetail();
                return;
            }

            Form.LoadFrom(detail);
            LoadIdentifiers(detail.Identifiers);
            Notification = null;
        }
        catch (Exception ex)
        {
            Notification = $"Failed to load identity: {ex.Message}";
            ResetDetail();
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

        if (!Form.IsValid)
        {
            Notification = "Display name and type are required.";
            return;
        }

        IsBusy = true;

        try
        {
            var dto = Form.ToDetail(Identifiers);
            var saved = await _partyService.SaveAsync(dto);
            Form.LoadFrom(saved);
            LoadIdentifiers(saved.Identifiers);
            Notification = dto.PartyId.HasValue ? "Identity updated." : "Identity registered.";
            await RefreshAsync(saved.PartyId);
        }
        catch (Exception ex)
        {
            Notification = $"Failed to save identity: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void NewIdentity()
    {
        SelectedParty = null;
        Form.Reset();
        Identifiers.Clear();
        AddIdentifier();
        Notification = "Ready for new identity details.";
    }

    private void AddIdentifier()
    {
        if (IsBusy)
        {
            return;
        }

        var model = new IdentityIdentifierModel
        {
            Scheme = "MRN",
            Value = string.Empty
        };
        Identifiers.Add(model);
        SelectedIdentifier = model;
    }

    private void RemoveSelectedIdentifier()
    {
        if (SelectedIdentifier is null)
        {
            return;
        }

        Identifiers.Remove(SelectedIdentifier);
        SelectedIdentifier = Identifiers.FirstOrDefault();
        if (Identifiers.Count == 0)
        {
            AddIdentifier();
        }
    }

    private void LoadIdentifiers(IEnumerable<PartyIdentifierDto> identifiers)
    {
        Identifiers.Clear();
        foreach (var identifier in identifiers)
        {
            var model = new IdentityIdentifierModel
            {
                PartyIdentifierId = identifier.PartyIdentifierId,
                Scheme = identifier.Scheme,
                Value = identifier.Value
            };
            Identifiers.Add(model);
        }

        if (Identifiers.Count == 0)
        {
            AddIdentifier();
        }
        else
        {
            SelectedIdentifier = Identifiers.FirstOrDefault();
        }
    }

    private void ResetDetail()
    {
        Form.Reset();
        Identifiers.Clear();
        AddIdentifier();
    }

    private void UpdateCommandStates()
    {
        _searchCommand.RaiseCanExecuteChanged();
        _saveCommand.RaiseCanExecuteChanged();
        _refreshIdentifiersCommand.RaiseCanExecuteChanged();
        _addIdentifierCommand.RaiseCanExecuteChanged();
        _removeIdentifierCommand.RaiseCanExecuteChanged();
    }

    private void OnIdentifiersChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (IdentityIdentifierModel model in e.NewItems)
            {
                model.PropertyChanged += OnIdentifierPropertyChanged;
            }
        }

        if (e.OldItems is not null)
        {
            foreach (IdentityIdentifierModel model in e.OldItems)
            {
                model.PropertyChanged -= OnIdentifierPropertyChanged;
            }
        }

        UpdateCommandStates();
    }

    private void OnIdentifierPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateCommandStates();
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

public sealed class IdentityFormModel : INotifyPropertyChanged
{
    private Guid? _partyId;
    private string _displayName = string.Empty;
    private string _partyType = "Patient";
    private string? _nationalId;
    private DateTime? _dateOfBirth;

    public Guid? PartyId
    {
        get => _partyId;
        set => SetProperty(ref _partyId, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public string PartyType
    {
        get => _partyType;
        set => SetProperty(ref _partyType, value);
    }

    public string? NationalId
    {
        get => _nationalId;
        set => SetProperty(ref _nationalId, value);
    }

    public DateTime? DateOfBirth
    {
        get => _dateOfBirth;
        set => SetProperty(ref _dateOfBirth, value);
    }

    public bool IsValid => !string.IsNullOrWhiteSpace(DisplayName) && !string.IsNullOrWhiteSpace(PartyType);

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Reset()
    {
        PartyId = null;
        DisplayName = string.Empty;
        PartyType = "Patient";
        NationalId = string.Empty;
        DateOfBirth = null;
    }

    public void LoadFrom(PartyDetailDto detail)
    {
        PartyId = detail.PartyId;
        DisplayName = detail.DisplayName;
        PartyType = detail.PartyType;
        NationalId = detail.NationalId;
        DateOfBirth = detail.DateOfBirth?.ToDateTime(TimeOnly.MinValue);
    }

    public PartyDetailDto ToDetail(IEnumerable<IdentityIdentifierModel> identifiers)
    {
        var identifierDtos = identifiers
            .Where(i => !string.IsNullOrWhiteSpace(i.Value))
            .Select(i => new PartyIdentifierDto(i.PartyIdentifierId, i.Scheme, i.Value))
            .ToList();

        var dob = DateOfBirth.HasValue ? DateOnly.FromDateTime(DateOfBirth.Value) : (DateOnly?)null;

        return new PartyDetailDto(
            PartyId,
            DisplayName,
            PartyType,
            dob,
            string.IsNullOrWhiteSpace(NationalId) ? null : NationalId?.Trim(),
            identifierDtos);
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

public sealed class IdentityIdentifierModel : INotifyPropertyChanged
{
    private Guid? _partyIdentifierId;
    private string _scheme = string.Empty;
    private string _value = string.Empty;

    public Guid? PartyIdentifierId
    {
        get => _partyIdentifierId;
        set => SetProperty(ref _partyIdentifierId, value);
    }

    public string Scheme
    {
        get => _scheme;
        set => SetProperty(ref _scheme, value);
    }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
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

public sealed record PartySummaryModel(Guid PartyId, string DisplayName, string PartyType, string? PrimaryIdentifier, DateOnly? DateOfBirth);
