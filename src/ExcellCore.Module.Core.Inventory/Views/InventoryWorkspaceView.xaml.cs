using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExcellCore.Module.Core.Inventory.ViewModels;

namespace ExcellCore.Module.Core.Inventory.Views;

public partial class InventoryWorkspaceView : UserControl
{
    private readonly InventoryWorkspaceViewModel _viewModel;
    private bool _initialized;

    public InventoryWorkspaceView(InventoryWorkspaceViewModel viewModel)
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
