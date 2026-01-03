using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExcellCore.Module.IS.Corporate.ViewModels;

namespace ExcellCore.Module.IS.Corporate.Views;

public partial class CorporateWorkspaceView : UserControl
{
    private readonly CorporateWorkspaceViewModel _viewModel;
    private bool _initialized;

    public CorporateWorkspaceView(CorporateWorkspaceViewModel viewModel)
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
