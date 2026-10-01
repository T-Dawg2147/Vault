using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Vault.App.Wpf.Services;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Core.Validation;

namespace Vault.App.Wpf.ViewModels;

/// <summary>
/// Admin-only migration workflow: pick a legacy CSV/TXT/XLSX file, map columns,
/// preview validated rows, then commit (encrypt + persist + audit).
/// </summary>
public partial class ImportViewModel : ViewModelBase
{
    private readonly IImportService _importService;
    private readonly IVaultService _vaultService;
    private readonly IAppSession _session;

    private string? _filePath;

    [ObservableProperty] private string _selectedFileName = "(no file selected)";
    [ObservableProperty] private ObservableCollection<VaultGroup> _vaults = new();
    [ObservableProperty] private VaultGroup? _selectedVault;
    [ObservableProperty] private ObservableCollection<string> _sourceColumns = new();
    [ObservableProperty] private int _labelColumn;
    [ObservableProperty] private int _usernameColumn = -1;
    [ObservableProperty] private int _passwordColumn = 1;
    [ObservableProperty] private int _urlColumn = -1;
    [ObservableProperty] private int _notesColumn = -1;
    [ObservableProperty] private bool _hasHeaderRow = true;
    [ObservableProperty] private ObservableCollection<ImportRow> _previewRows = new();
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;

    public ImportViewModel(IImportService importService, IVaultService vaultService, IAppSession session)
    {
        _importService = importService;
        _vaultService = vaultService;
        _session = session;
    }

    public override void OnNavigatedTo()
    {
        ErrorMessage = null;
        StatusMessage = null;
        Vaults = new ObservableCollection<VaultGroup>(_vaultService.GetAllVaults(_session.CurrentUser));
    }

    [RelayCommand]
    private void Browse()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select legacy password file",
            Filter = "Password files (*.csv;*.txt;*.xlsx)|*.csv;*.txt;*.xlsx|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            _filePath = dialog.FileName;
            SelectedFileName = System.IO.Path.GetFileName(_filePath);
            LoadColumns();
        }
    }

    private void LoadColumns()
    {
        if (_filePath is null)
            return;

        try
        {
            SourceColumns = new ObservableCollection<string>(_importService.ReadColumns(_filePath, HasHeaderRow));
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is ValidationException or System.IO.IOException)
        {
            ErrorMessage = ex.Message;
        }
    }

    partial void OnHasHeaderRowChanged(bool value) => LoadColumns();

    [RelayCommand]
    private void Preview()
    {
        if (_filePath is null)
        {
            ErrorMessage = "Select a file first.";
            return;
        }

        try
        {
            var preview = _importService.Preview(_filePath, BuildMapping());
            PreviewRows = new ObservableCollection<ImportRow>(preview.Rows);
            StatusMessage = $"{preview.ValidRowCount} valid rows, {preview.InvalidRowCount} invalid rows.";
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is ValidationException or System.IO.IOException)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Commit()
    {
        if (_filePath is null || SelectedVault is null)
        {
            ErrorMessage = "Select a file and a target vault first.";
            return;
        }

        try
        {
            var preview = _importService.Preview(_filePath, BuildMapping());
            var imported = _importService.Commit(_session.CurrentUser, SelectedVault.Id, preview);

            StatusMessage = $"Imported {imported} credentials into '{SelectedVault.Name}'.";
            PreviewRows = new ObservableCollection<ImportRow>();
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is VaultAccessDeniedException or ValidationException or System.IO.IOException)
        {
            ErrorMessage = ex.Message;
        }
    }

    private ImportColumnMapping BuildMapping() => new()
    {
        LabelColumn = LabelColumn >= 0 ? LabelColumn : null,
        UsernameColumn = UsernameColumn >= 0 ? UsernameColumn : null,
        PasswordColumn = PasswordColumn >= 0 ? PasswordColumn : null,
        UrlColumn = UrlColumn >= 0 ? UrlColumn : null,
        NotesColumn = NotesColumn >= 0 ? NotesColumn : null,
        HasHeaderRow = HasHeaderRow
    };
}
