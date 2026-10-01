namespace Vault.Core.Models;

/// <summary>
/// Maps a column index from a source file to an import field.
/// </summary>
public class ImportColumnMapping
{
    public int? LabelColumn { get; set; }
    public int? UsernameColumn { get; set; }
    public int? PasswordColumn { get; set; }
    public int? UrlColumn { get; set; }
    public int? NotesColumn { get; set; }
    public int? TagsColumn { get; set; }

    /// <summary>Whether the source file's first row contains column headers.</summary>
    public bool HasHeaderRow { get; set; } = true;
}

/// <summary>
/// One parsed row from a source file, before encryption and persistence.
/// Plaintext passwords live here only transiently during the import preview/commit flow.
/// </summary>
public class ImportRow
{
    public int RowNumber { get; set; }

    public string Label { get; set; } = string.Empty;

    public string UsernameOrEmail { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? Url { get; set; }

    public string? Notes { get; set; }

    public List<string> Tags { get; set; } = new();

    /// <summary>Validation errors for this row. Rows with errors cannot be imported.</summary>
    public List<string> Errors { get; set; } = new();

    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Result of parsing a source file: the raw column headers plus the mapped, validated rows.
/// </summary>
public class ImportPreview
{
    public string SourceFileName { get; set; } = string.Empty;

    public IReadOnlyList<string> SourceColumns { get; set; } = Array.Empty<string>();

    public List<ImportRow> Rows { get; set; } = new();

    public int ValidRowCount => Rows.Count(r => r.IsValid);

    public int InvalidRowCount => Rows.Count - ValidRowCount;
}
