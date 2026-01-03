using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExcellCore.Module.Core.Agreements.ViewModels;

namespace ExcellCore.Module.Core.Agreements.Views;

public partial class AgreementWorkspaceView : UserControl
{
    private readonly AgreementWorkspaceViewModel _viewModel;
    private bool _initialized;

    public AgreementWorkspaceView(AgreementWorkspaceViewModel viewModel)
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
