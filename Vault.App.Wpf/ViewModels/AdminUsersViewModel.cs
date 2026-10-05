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
    [ObservableProperty] private string _newDomain = string.Empty;
    [ObservableProperty] private string _newUsername = string.Empty;
    [ObservableProperty] private string _newDisplayName = string.Empty;
    [ObservableProperty] private UserRole _newRole = UserRole.Viewer;
    [ObservableProperty] private string _editDomain = string.Empty;
    [ObservableProperty] private string _editUsername = string.Empty;
    [ObservableProperty] private string _editDisplayName = string.Empty;
    [ObservableProperty] private UserRole _editRole = UserRole.Viewer;
    [ObservableProperty] private bool _editIsActive = true;
    [ObservableProperty] private string? _errorMessage;

    public IReadOnlyList<UserRole> Roles { get; } = Enum.GetValues<UserRole>();

    public AdminUsersViewModel(IUserService userService, IAppSession session)
    {
        _userService = userService;
        _session = session;
    }

    partial void OnSelectedUserChanged(User? value)
    {
        if (value is null) return;
        EditDomain = value.Domain;
        EditUsername = value.Username;
        EditDisplayName = value.DisplayName;
        EditRole = value.Role;
        EditIsActive = value.IsActive;
    }

    public override void OnNavigatedTo() => Refresh();

    private void Refresh()
    {
        ErrorMessage = null;
        var selectedId = SelectedUser?.Id;
        Users = new ObservableCollection<User>(_userService.GetAll(_session.CurrentUser));
        SelectedUser = selectedId is null ? null : Users.FirstOrDefault(u => u.Id == selectedId);
    }

    [RelayCommand]
    private void AddUser()
    {
        try
        {
            _userService.Create(_session.CurrentUser, NewDomain, NewUsername, NewDisplayName, NewRole);
            NewDomain = NewUsername = NewDisplayName = string.Empty;
            NewRole = UserRole.Viewer;
            Refresh();
        }
        catch (Exception ex) when (ex is VaultAccessDeniedException or ValidationException)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void SaveUser()
    {
        if (SelectedUser is null)
        {
            ErrorMessage = "Select a user first.";
            return;
        }

        try
        {
            _userService.Update(_session.CurrentUser, SelectedUser.Id,
                EditDomain, EditUsername, EditDisplayName, EditRole, EditIsActive);
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
