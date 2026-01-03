using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExcellCore.Module.IS.Clinical.ViewModels;

namespace ExcellCore.Module.IS.Clinical.Views;

public partial class ClinicalWorkspaceView : UserControl
{
    private readonly ClinicalWorkspaceViewModel _viewModel;
    private bool _initialized;

    public ClinicalWorkspaceView(ClinicalWorkspaceViewModel viewModel)
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
