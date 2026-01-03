using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExcellCore.Module.Extensions.Reporting.ViewModels;

namespace ExcellCore.Module.Extensions.Reporting.Views;

public partial class ReportingWorkspaceView : UserControl
{
    private readonly ReportingWorkspaceViewModel _viewModel;
    private bool _initialized;

    public ReportingWorkspaceView(ReportingWorkspaceViewModel viewModel)
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
