using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExcellCore.Module.Core.Financials.ViewModels;

namespace ExcellCore.Module.Core.Financials.Views;

public partial class FinancialsWorkspaceView : UserControl
{
    private readonly FinancialsWorkspaceViewModel _viewModel;
    private bool _initialized;

    public FinancialsWorkspaceView(FinancialsWorkspaceViewModel viewModel)
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
