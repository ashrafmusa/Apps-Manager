using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExcellCore.Module.Extensions.Reporting.ViewModels;

namespace ExcellCore.Module.Extensions.Reporting.Views;

public partial class TelemetryWorkspaceView : UserControl
{
    private readonly TelemetryWorkspaceViewModel _viewModel;
    private bool _initialized;

    public TelemetryWorkspaceView(TelemetryWorkspaceViewModel viewModel)
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
