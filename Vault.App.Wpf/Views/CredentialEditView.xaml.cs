using System.Windows.Controls;
using Vault.App.Wpf.ViewModels;

namespace Vault.App.Wpf.Views;

public partial class CredentialEditView : UserControl
{
    public CredentialEditView()
    {
        InitializeComponent();
    }

    private void PasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        // PasswordBox.Password is not bindable for security reasons; sync manually.
        if (DataContext is CredentialEditViewModel vm)
            vm.Password = PasswordBox.Password;
    }
}
