using System.Windows;
using Vault.App.Wpf.ViewModels;

namespace Vault.App.Wpf;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
