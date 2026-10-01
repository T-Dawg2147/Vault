using System.Text;
using ClosedXML.Excel;
using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Core.Validation;

namespace Vault.Infrastructure.Import;

/// <summary>
/// Admin-only importer for legacy CSV/TXT/XLS/XLSX password files.
///
/// Flow: ReadColumns → Preview (validate) → Commit (encrypt + persist + audit).
/// Plaintext secrets exist only inside the transient <see cref="ImportPreview"/>
/// and are never written to disk or logs. Invalid rows are skipped on commit.
///
/// Notes on formats:
/// - CSV/TXT are parsed with a quoting-aware parser (comma, semicolon, or tab delimiters).
/// - XLSX is read via ClosedXML from the first worksheet.
/// - Legacy binary XLS is not parseable by ClosedXML; admins must convert to XLSX first.
/// </summary>
public sealed class LegacyFileImporter : IImportService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".csv", ".txt", ".xlsx" };

    private readonly ICredentialService _credentialService;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditService _audit;

    public LegacyFileImporter(
        ICredentialService credentialService,
        IAuthorizationService authorization,
        IAuditService audit)
    {
        _credentialService = credentialService;
        _authorization = authorization;
        _audit = audit;
    }

    public IReadOnlyList<string> ReadColumns(string filePath, bool hasHeaderRow)
    {
        var rows = ReadAllRows(filePath);
        if (rows.Count == 0)
            return Array.Empty<string>();

        if (!hasHeaderRow)
            return Enumerable.Range(0, rows[0].Count).Select(i => $"Column {i + 1}").ToList();

        return rows[0];
    }

    public ImportPreview Preview(string filePath, ImportColumnMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        var allRows = ReadAllRows(filePath);
        var preview = new ImportPreview { SourceFileName = Path.GetFileName(filePath) };

        if (allRows.Count == 0)
            return preview;

        var dataRows = mapping.HasHeaderRow ? allRows.Skip(1) : allRows;
        var startRowNumber = mapping.HasHeaderRow ? 2 : 1;
        preview.SourceColumns = ReadColumns(filePath, mapping.HasHeaderRow);

        var rowNumber = startRowNumber;
        foreach (var raw in dataRows)
        {
            var row = new ImportRow
            {
                RowNumber = rowNumber++,
                Label = Get(raw, mapping.LabelColumn),
                UsernameOrEmail = Get(raw, mapping.UsernameColumn),
                Password = Get(raw, mapping.PasswordColumn),
                Url = NullIfEmpty(Get(raw, mapping.UrlColumn)),
                Notes = NullIfEmpty(Get(raw, mapping.NotesColumn)),
                Tags = Get(raw, mapping.TagsColumn)
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList()
            };

            ValidateRow(row);
            preview.Rows.Add(row);
        }

        return preview;
    }

    public int Commit(User actor, int vaultId, ImportPreview preview)
    {
        _authorization.Require(_authorization.CanImport(actor), "Import");
        ArgumentNullException.ThrowIfNull(preview);

        _audit.Log(AuditAction.ImportStarted, actor.Id, "Vault", vaultId.ToString(),
            $"Import of '{preview.SourceFileName}' started ({preview.ValidRowCount} valid rows).");

        var imported = 0;
        try
        {
            foreach (var row in preview.Rows.Where(r => r.IsValid))
            {
                _credentialService.Create(actor, vaultId, row.Label, row.UsernameOrEmail,
                    row.Password, row.Url, row.Notes, row.Tags);
                imported++;
            }
        }
        catch (Exception ex) when (ex is not VaultAccessDeniedException)
        {
            // Log the failure without echoing any row data (rows contain plaintext secrets).
            _audit.Log(AuditAction.ImportFailed, actor.Id, "Vault", vaultId.ToString(),
                $"Import of '{preview.SourceFileName}' failed after {imported} rows: {ex.GetType().Name}.");
            throw;
        }

        _audit.Log(AuditAction.ImportCompleted, actor.Id, "Vault", vaultId.ToString(),
            $"Import of '{preview.SourceFileName}' completed: {imported} credentials imported, {preview.InvalidRowCount} rows skipped.");

        return imported;
    }

    private static void ValidateRow(ImportRow row)
    {
        try
        {
            row.Label = InputValidator.RequiredText(row.Label, "Label", InputValidator.MaxLabelLength);
        }
        catch (ValidationException ex) { row.Errors.Add(ex.Message); }

        try
        {
            InputValidator.RequiredPassword(row.Password);
        }
        catch (ValidationException ex) { row.Errors.Add(ex.Message); }

        try
        {
            row.Url = InputValidator.OptionalUrl(row.Url);
        }
        catch (ValidationException ex) { row.Errors.Add(ex.Message); }

        try
        {
            row.Tags = InputValidator.NormalizeTags(row.Tags).ToList();
        }
        catch (ValidationException ex) { row.Errors.Add(ex.Message); }

        if (row.UsernameOrEmail.Length > InputValidator.MaxUsernameLength)
            row.Errors.Add($"Username must not exceed {InputValidator.MaxUsernameLength} characters.");

        if (row.Notes is { Length: > InputValidator.MaxNotesLength })
            row.Errors.Add($"Notes must not exceed {InputValidator.MaxNotesLength} characters.");
    }

    private static string Get(IReadOnlyList<string> row, int? column)
        => column is >= 0 && column.Value < row.Count ? row[column.Value] : string.Empty;

    private static string? NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<List<string>> ReadAllRows(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new FileNotFoundException("Import file not found.", filePath);

        var extension = Path.GetExtension(filePath);
        if (!SupportedExtensions.Contains(extension))
            throw new ValidationException(
                $"Unsupported file type '{extension}'. Use CSV, TXT, or XLSX (convert legacy XLS to XLSX first).");

        return extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? ReadXlsx(filePath)
            : ReadDelimited(filePath);
    }

    private static List<List<string>> ReadDelimited(string filePath)
    {
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        var delimiter = DetectDelimiter(content);
        return ParseDelimited(content, delimiter);
    }

    private static char DetectDelimiter(string content)
    {
        var firstLine = content.Split('\n', 2)[0];
        var candidates = new[] { '\t', ';', ',' };
        return candidates
            .OrderByDescending(c => firstLine.Count(ch => ch == c))
            .First();
    }

    /// <summary>RFC-4180-style delimited parsing with double-quote escaping.</summary>
    private static List<List<string>> ParseDelimited(string content, char delimiter)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < content.Length && content[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(ch);
                }
                continue;
            }

            if (ch == '"' && field.Length == 0)
            {
                inQuotes = true;
            }
            else if (ch == delimiter)
            {
                currentRow.Add(field.ToString().Trim());
                field.Clear();
            }
            else if (ch is '\r' or '\n')
            {
                if (ch == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                    i++;

                currentRow.Add(field.ToString().Trim());
                field.Clear();

                if (currentRow.Any(c => c.Length > 0))
                    rows.Add(currentRow);

                currentRow = new List<string>();
            }
            else
            {
                field.Append(ch);
            }
        }

        if (field.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(field.ToString().Trim());
            if (currentRow.Any(c => c.Length > 0))
                rows.Add(currentRow);
        }

        return rows;
    }

    private static List<List<string>> ReadXlsx(string filePath)
    {
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();

        var rows = new List<List<string>>();
        foreach (var row in worksheet.RowsUsed())
        {
            var cells = row.CellsUsed()
                .Select(cell => cell.GetString().Trim())
                .ToList();

            if (cells.Any(c => c.Length > 0))
                rows.Add(cells);
        }

        return rows;
    }
}
