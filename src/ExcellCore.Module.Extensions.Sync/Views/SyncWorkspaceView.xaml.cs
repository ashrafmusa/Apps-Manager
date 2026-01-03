using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExcellCore.Module.Extensions.Sync.ViewModels;

namespace ExcellCore.Module.Extensions.Sync.Views;

public partial class SyncWorkspaceView : UserControl
{
    private readonly SyncWorkspaceViewModel _viewModel;
    private bool _initialized;

    public SyncWorkspaceView(SyncWorkspaceViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        await _viewModel.InitializeAsync();
    }
}
