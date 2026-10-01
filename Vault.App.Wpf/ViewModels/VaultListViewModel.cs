using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Wpf.Services;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Core.Validation;

namespace Vault.App.Wpf.ViewModels;

public partial class VaultListViewModel : ViewModelBase
{
    private readonly IVaultService _vaultService;
    private readonly IAppSession _session;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private ObservableCollection<VaultGroup> _vaults = new();

    [ObservableProperty]
    private VaultGroup? _selectedVault;

    [ObservableProperty]
    private string _newVaultName = string.Empty;

    [ObservableProperty]
    private string _newVaultDescription = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public IAppSession Session => _session;

    public VaultListViewModel(IVaultService vaultService, IAppSession session, INavigationService navigation)
    {
        _vaultService = vaultService;
        _session = session;
        _navigation = navigation;
    }

    public override void OnNavigatedTo() => Refresh();

    private void Refresh()
    {
        ErrorMessage = null;
        Vaults = new ObservableCollection<VaultGroup>(_vaultService.GetAccessibleVaults(_session.CurrentUser));
    }

    [RelayCommand]
    private void CreateVault()
    {
        if (!Session.IsAdmin)
        {
            ErrorMessage = "Only administrators can create vaults.";
            return;
        }

        try
        {
            _vaultService.Create(_session.CurrentUser, NewVaultName, NewVaultDescription);
            NewVaultName = string.Empty;
            NewVaultDescription = string.Empty;
            Refresh();
        }
        catch (ValidationException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void OpenCredentials()
    {
        if (SelectedVault is null)
        {
            ErrorMessage = "Select a vault first.";
            return;
        }

        _navigation.NavigateTo<CredentialListViewModel>(vm => vm.SelectVault(SelectedVault.Id));
    }
}
