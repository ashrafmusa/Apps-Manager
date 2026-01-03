using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExcellCore.Module.Core.Identity.ViewModels;

namespace ExcellCore.Module.Core.Identity.Views;

public partial class IdentityDashboardView : UserControl
{
    private readonly IdentityDashboardViewModel _viewModel;

    public IdentityDashboardView(IdentityDashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _viewModel.InitializeAsync();
    }
}
