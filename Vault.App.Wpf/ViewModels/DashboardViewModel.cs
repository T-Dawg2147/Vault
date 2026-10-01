using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Wpf.Services;
using Vault.Core.Interfaces;
using Vault.Core.Models;

namespace Vault.App.Wpf.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IVaultService _vaultService;
    private readonly ICredentialService _credentialService;
    private readonly IAppSession _session;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private int _accessibleVaultCount;

    [ObservableProperty]
    private int _accessibleCredentialCount;

    [ObservableProperty]
    private int _rotationDueCount;

    [ObservableProperty]
    private string _welcomeText = string.Empty;

    public DashboardViewModel(IVaultService vaultService, ICredentialService credentialService,
        IAppSession session, INavigationService navigation)
    {
        _vaultService = vaultService;
        _credentialService = credentialService;
        _session = session;
        _navigation = navigation;
    }

    public override void OnNavigatedTo()
    {
        var user = _session.CurrentUser;
        WelcomeText = $"Signed in as {user.DisplayName} ({user.Domain}\\{user.Username}) — role: {user.Role}";

        var vaults = _vaultService.GetAccessibleVaults(user);
        AccessibleVaultCount = vaults.Count;

        var credentials = _credentialService.Search(user, string.Empty);
        AccessibleCredentialCount = credentials.Count;
        RotationDueCount = credentials.Count(c =>
            c.RotationDueAt.HasValue && c.RotationDueAt.Value <= DateTime.UtcNow.AddDays(14));
    }

    [RelayCommand]
    private void OpenCredentials() => _navigation.NavigateTo<CredentialListViewModel>();

    [RelayCommand]
    private void OpenVaults() => _navigation.NavigateTo<VaultListViewModel>();
}
