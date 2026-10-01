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
/// Admin-only user management: register Windows users, change roles, deactivate accounts.
/// Service-layer checks enforce admin rights regardless of UI state.
/// </summary>
public partial class AdminUsersViewModel : ViewModelBase
{
    private readonly IUserService _userService;
    private readonly IAppSession _session;

    [ObservableProperty] private ObservableCollection<User> _users = new();
    [ObservableProperty] private User? _selectedUser;
    [ObservableProperty] private string _newSid = string.Empty;
    [ObservableProperty] private string _newDomain = string.Empty;
    [ObservableProperty] private string _newUsername = string.Empty;
    [ObservableProperty] private string _newDisplayName = string.Empty;
    [ObservableProperty] private UserRole _newRole = UserRole.Viewer;
    [ObservableProperty] private string? _errorMessage;

    public IReadOnlyList<UserRole> Roles { get; } = Enum.GetValues<UserRole>();

    public AdminUsersViewModel(IUserService userService, IAppSession session)
    {
        _userService = userService;
        _session = session;
    }

    public override void OnNavigatedTo() => Refresh();

    private void Refresh()
    {
        ErrorMessage = null;
        Users = new ObservableCollection<User>(_userService.GetAll(_session.CurrentUser));
    }

    [RelayCommand]
    private void AddUser()
    {
        try
        {
            _userService.Create(_session.CurrentUser, NewSid, NewDomain, NewUsername, NewDisplayName, NewRole);
            NewSid = NewDomain = NewUsername = NewDisplayName = string.Empty;
            NewRole = UserRole.Viewer;
            Refresh();
        }
        catch (Exception ex) when (ex is VaultAccessDeniedException or ValidationException)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void ChangeRole(UserRole role)
    {
        if (SelectedUser is null)
        {
            ErrorMessage = "Select a user first.";
            return;
        }

        try
        {
            _userService.ChangeRole(_session.CurrentUser, SelectedUser.Id, role);
            Refresh();
        }
        catch (Exception ex) when (ex is VaultAccessDeniedException or ValidationException)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void ToggleActive()
    {
        if (SelectedUser is null)
        {
            ErrorMessage = "Select a user first.";
            return;
        }

        try
        {
            _userService.SetActive(_session.CurrentUser, SelectedUser.Id, !SelectedUser.IsActive);
            Refresh();
        }
        catch (Exception ex) when (ex is VaultAccessDeniedException or ValidationException)
        {
            ErrorMessage = ex.Message;
        }
    }
}
