using System.Windows;
using ExcellCore.Shell.ViewModels;

namespace ExcellCore.Shell.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
