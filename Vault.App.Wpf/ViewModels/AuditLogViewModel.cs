using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Vault.App.Wpf.Services;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Core.Validation;

namespace Vault.App.Wpf.ViewModels;

/// <summary>
/// Admin-only audit log viewer. Entries never contain plaintext secrets by design.
/// </summary>
public partial class AuditLogViewModel : ViewModelBase
{
    private readonly IAuditService _auditService;
    private readonly IAppSession _session;

    [ObservableProperty] private ObservableCollection<AuditLogEntry> _entries = new();
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string? _errorMessage;

    public AuditLogViewModel(IAuditService auditService, IAppSession session)
    {
        _auditService = auditService;
        _session = session;
    }

    public override void OnNavigatedTo() => Refresh();

    [RelayCommand]
    private void Refresh()
    {
        try
        {
            Entries = new ObservableCollection<AuditLogEntry>(
                string.IsNullOrWhiteSpace(FilterText)
                    ? _auditService.GetRecent(_session.CurrentUser)
                    : _auditService.Search(_session.CurrentUser, FilterText, null, null));
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is VaultAccessDeniedException or ValidationException)
        {
            ErrorMessage = ex.Message;
        }
    }
}
