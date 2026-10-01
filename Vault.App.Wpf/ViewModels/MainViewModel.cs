using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Wpf.Services;

namespace Vault.App.Wpf.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private ViewModelBase? _currentViewModel;

    public IAppSession Session { get; }

    public MainViewModel(INavigationService navigation, IAppSession session)
    {
        _navigation = navigation;
        Session = session;
        _navigation.CurrentViewModelChanged += (_, _) =>
            CurrentViewModel = _navigation.CurrentViewModel;
    }

    [RelayCommand]
    private void Navigate(string destination)
    {
        switch (destination)
        {
            case "Dashboard": _navigation.NavigateTo<DashboardViewModel>(); break;
            case "Vaults": _navigation.NavigateTo<VaultListViewModel>(); break;
            case "Credentials": _navigation.NavigateTo<CredentialListViewModel>(); break;
            case "AdminUsers":
                if (Session.IsAdmin) _navigation.NavigateTo<AdminUsersViewModel>();
                break;
            case "AdminVaultAccess":
                if (Session.IsAdmin) _navigation.NavigateTo<AdminVaultAccessViewModel>();
                break;
            case "AuditLog":
                if (Session.IsAdmin) _navigation.NavigateTo<AuditLogViewModel>();
                break;
            case "Import":
                if (Session.IsAdmin) _navigation.NavigateTo<ImportViewModel>();
                break;
        }
    }
}
