using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExcellCore.Module.IS.Retail.ViewModels;

namespace ExcellCore.Module.IS.Retail.Views;

public partial class RetailWorkspaceView : UserControl
{
    private readonly RetailWorkspaceViewModel _viewModel;
    private bool _initialized;

    public RetailWorkspaceView(RetailWorkspaceViewModel viewModel)
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
