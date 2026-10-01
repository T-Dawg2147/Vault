using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Wpf.Services;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Core.Validation;

namespace Vault.App.Wpf.ViewModels;

public partial class CredentialListViewModel : ViewModelBase
{
    private readonly ICredentialService _credentialService;
    private readonly IVaultService _vaultService;
    private readonly ISecureClipboard _clipboard;
    private readonly IAppSession _session;
    private readonly INavigationService _navigation;

    [ObservableProperty]
    private ObservableCollection<VaultGroup> _vaults = new();

    [ObservableProperty]
    private VaultGroup? _selectedVault;

    [ObservableProperty]
    private ObservableCollection<Credential> _credentials = new();

    [ObservableProperty]
    private Credential? _selectedCredential;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public CredentialListViewModel(ICredentialService credentialService, IVaultService vaultService,
        ISecureClipboard clipboard, IAppSession session, INavigationService navigation)
    {
        _credentialService = credentialService;
        _vaultService = vaultService;
        _clipboard = clipboard;
        _session = session;
        _navigation = navigation;
    }

    public override void OnNavigatedTo()
    {
        ErrorMessage = null;
        Vaults = new ObservableCollection<VaultGroup>(_vaultService.GetAccessibleVaults(_session.CurrentUser));
        Refresh();
    }

    public void SelectVault(int vaultId)
    {
        SelectedVault = Vaults.FirstOrDefault(v => v.Id == vaultId);
        Refresh();
    }

    partial void OnSelectedVaultChanged(VaultGroup? value) => Refresh();

    partial void OnSearchTextChanged(string value) => Refresh();

    private void Refresh()
    {
        if (Vaults.Count == 0 && !string.IsNullOrEmpty(SearchText))
            return;

        var user = _session.CurrentUser;

        IEnumerable<Credential> results;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            results = _credentialService.Search(user, SearchText);
            if (SelectedVault is not null)
                results = results.Where(c => c.VaultId == SelectedVault.Id);
        }
        else if (SelectedVault is not null)
        {
            results = _credentialService.ListByVault(user, SelectedVault.Id);
        }
        else
        {
            results = _credentialService.Search(user, string.Empty);
        }

        Credentials = new ObservableCollection<Credential>(results);
    }

    [RelayCommand]
    private void CopyUsername()
    {
        if (SelectedCredential is null || string.IsNullOrEmpty(SelectedCredential.UsernameOrEmail))
            return;

        // Usernames are not secrets; use the plain clipboard path.
        _clipboard.CopyText(SelectedCredential.UsernameOrEmail);
    }

    [RelayCommand]
    private void CopyPassword()
    {
        if (SelectedCredential is null)
            return;

        try
        {
            var password = _credentialService.RevealPassword(_session.CurrentUser, SelectedCredential.Id);
            _clipboard.CopySecret(password);
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is VaultAccessDeniedException or ValidationException)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void AddCredential()
    {
        if (SelectedVault is null)
        {
            ErrorMessage = "Select a vault first.";
            return;
        }

        _navigation.NavigateTo<CredentialEditViewModel>(vm => vm.ForNew(SelectedVault.Id));
    }

    [RelayCommand]
    private void EditCredential()
    {
        if (SelectedCredential is null)
        {
            ErrorMessage = "Select a credential first.";
            return;
        }

        _navigation.NavigateTo<CredentialEditViewModel>(vm => vm.ForEdit(SelectedCredential.Id));
    }
}
