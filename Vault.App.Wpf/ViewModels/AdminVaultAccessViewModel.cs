using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Wpf.Services;
using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Core.Validation;

namespace Vault.App.Wpf.ViewModels;

/// <summary>
/// Admin-only vault access management: grant/revoke per-user vault permissions.
/// </summary>
public partial class AdminVaultAccessViewModel : ViewModelBase
{
    private readonly IVaultService _vaultService;
    private readonly IUserService _userService;
    private readonly IAppSession _session;

    [ObservableProperty] private ObservableCollection<VaultGroup> _vaults = new();
    [ObservableProperty] private VaultGroup? _selectedVault;
    [ObservableProperty] private ObservableCollection<VaultAccess> _accessList = new();
    [ObservableProperty] private ObservableCollection<User> _users = new();
    [ObservableProperty] private User? _selectedUser;
    [ObservableProperty] private PermissionLevel _selectedPermission = PermissionLevel.Viewer;
    [ObservableProperty] private string? _errorMessage;

    public IReadOnlyList<PermissionLevel> PermissionLevels { get; } = Enum.GetValues<PermissionLevel>();

    public AdminVaultAccessViewModel(IVaultService vaultService, IUserService userService, IAppSession session)
    {
        _vaultService = vaultService;
        _userService = userService;
        _session = session;
    }

    public override void OnNavigatedTo()
    {
        ErrorMessage = null;
        Vaults = new ObservableCollection<VaultGroup>(_vaultService.GetAllVaults(_session.CurrentUser));
        Users = new ObservableCollection<User>(_userService.GetAll(_session.CurrentUser));
    }

    partial void OnSelectedVaultChanged(VaultGroup? value) => RefreshAccessList();

    private void RefreshAccessList()
    {
        if (SelectedVault is null)
        {
            AccessList = new ObservableCollection<VaultAccess>();
            return;
        }

        AccessList = new ObservableCollection<VaultAccess>(
            _vaultService.GetAccessList(_session.CurrentUser, SelectedVault.Id));
    }

    [RelayCommand]
    private void Grant()
    {
        if (SelectedVault is null || SelectedUser is null)
        {
            ErrorMessage = "Select a vault and a user first.";
            return;
        }

        try
        {
            _vaultService.GrantAccess(_session.CurrentUser, SelectedVault.Id, SelectedUser.Id, SelectedPermission);
            RefreshAccessList();
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is VaultAccessDeniedException or ValidationException)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Revoke(VaultAccess? access)
    {
        if (access is null)
            return;

        try
        {
            _vaultService.RevokeAccess(_session.CurrentUser, access.VaultId, access.UserId);
            RefreshAccessList();
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is VaultAccessDeniedException or ValidationException)
        {
            ErrorMessage = ex.Message;
        }
    }
}
