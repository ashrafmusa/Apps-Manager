using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using ExcellCore.Module.Extensions.Sync.Models;
using ExcellCore.Module.Extensions.Sync.Services;

namespace ExcellCore.Module.Extensions.Sync.ViewModels;

public sealed class SyncWorkspaceViewModel
{
    private readonly IDeltaSyncProvider _deltaSyncProvider;

    public SyncWorkspaceViewModel(IDeltaSyncProvider deltaSyncProvider)
    {
        _deltaSyncProvider = deltaSyncProvider ?? throw new ArgumentNullException(nameof(deltaSyncProvider));
        TriageItems = new ObservableCollection<SyncTriageItem>();
    }

    public string Title => "Synchronization Center";
    public string Subtitle => "Manage adapters, schedules, and conflict policies.";

    public ObservableCollection<SyncTriageItem> TriageItems { get; }

    public async Task InitializeAsync()
    {
        await LoadTriageAsync().ConfigureAwait(false);
    }

    public Task<IReadOnlyList<SyncDelta>> PreviewDeltasAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        return _deltaSyncProvider.CaptureLocalChangesAsync(sinceUtc, cancellationToken);
    }

    public async Task LoadTriageAsync(CancellationToken cancellationToken = default)
    {
        var triage = await _deltaSyncProvider.GetTriageAsync(50, cancellationToken).ConfigureAwait(false);

        TriageItems.Clear();
        foreach (var item in triage)
        {
            TriageItems.Add(item);
        }
    }
}
