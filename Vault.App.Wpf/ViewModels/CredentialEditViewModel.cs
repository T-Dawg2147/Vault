using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Wpf.Services;
using Vault.Core.Interfaces;
using Vault.Core.Validation;

namespace Vault.App.Wpf.ViewModels;

/// <summary>
/// Add/edit a single credential. The plaintext password is held only while editing
/// and is never displayed unless the user explicitly toggles reveal.
/// </summary>
public partial class CredentialEditViewModel : ViewModelBase
{
    private readonly ICredentialService _credentialService;
    private readonly IAppSession _session;
    private readonly INavigationService _navigation;

    private int _vaultId;
    private int? _credentialId;

    [ObservableProperty] private string _title = "Add credential";
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private string _usernameOrEmail = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _url = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _tags = string.Empty;
    [ObservableProperty] private DateTime? _rotationDueAt;
    [ObservableProperty] private bool _isPasswordVisible;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isEditMode;

    public CredentialEditViewModel(ICredentialService credentialService, IAppSession session,
        INavigationService navigation)
    {
        _credentialService = credentialService;
        _session = session;
        _navigation = navigation;
    }

    public void ForNew(int vaultId)
    {
        _vaultId = vaultId;
        _credentialId = null;
        Title = "Add credential";
        IsEditMode = false;
    }

    public void ForEdit(int credentialId)
    {
        var user = _session.CurrentUser;
        var credential = _credentialService.Search(user, string.Empty)
            .FirstOrDefault(c => c.Id == credentialId);

        if (credential is null)
        {
            ErrorMessage = "Credential not found or not accessible.";
            return;
        }

        _credentialId = credentialId;
        _vaultId = credential.VaultId;
        Title = "Edit credential";
        IsEditMode = true;
        Label = credential.Label;
        UsernameOrEmail = credential.UsernameOrEmail;
        Url = credential.Url ?? string.Empty;
        Tags = string.Join(", ", credential.Tags);
        RotationDueAt = credential.RotationDueAt;

        try
        {
            Notes = _credentialService.RevealNotes(user, credentialId) ?? string.Empty;
        }
        catch (Exception ex) when (ex is VaultAccessDeniedException or ValidationException)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void TogglePasswordVisibility() => IsPasswordVisible = !IsPasswordVisible;

    [RelayCommand]
    private void Save()
    {
        var user = _session.CurrentUser;
        var tags = Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        try
        {
            if (_credentialId is null)
            {
                _credentialService.Create(user, _vaultId, Label, UsernameOrEmail, Password,
                    NullIfEmpty(Url), NullIfEmpty(Notes), tags, RotationDueAt);
            }
            else
            {
                // Empty password on edit keeps the existing password.
                _credentialService.Update(user, _credentialId.Value, Label, UsernameOrEmail,
                    string.IsNullOrEmpty(Password) ? null : Password,
                    NullIfEmpty(Url), NullIfEmpty(Notes), tags, RotationDueAt);
            }

            Password = string.Empty;
            _navigation.NavigateTo<CredentialListViewModel>(vm => vm.SelectVault(_vaultId));
        }
        catch (Exception ex) when (ex is VaultAccessDeniedException or ValidationException)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        Password = string.Empty;
        _navigation.NavigateTo<CredentialListViewModel>();
    }

    [RelayCommand]
    private void Delete()
    {
        if (_credentialId is null)
            return;

        try
        {
            _credentialService.Delete(_session.CurrentUser, _credentialId.Value);
            _navigation.NavigateTo<CredentialListViewModel>(vm => vm.SelectVault(_vaultId));
        }
        catch (Exception ex) when (ex is VaultAccessDeniedException or ValidationException)
        {
            ErrorMessage = ex.Message;
        }
    }

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
