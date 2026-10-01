namespace Vault.Core.Models;

/// <summary>
/// A stored credential. Sensitive fields are only ever persisted in encrypted form;
/// plaintext exists in memory only while being edited or revealed by an authorized user.
/// </summary>
public class Credential
{
    public int Id { get; set; }

    public int VaultId { get; set; }

    /// <summary>Display name, e.g. "Production SQL Server". Required.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Login username or e-mail address (single combined field).</summary>
    public string UsernameOrEmail { get; set; } = string.Empty;

    /// <summary>AES-GCM encrypted password (nonce + tag + ciphertext). Never plaintext.</summary>
    public byte[] PasswordEncrypted { get; set; } = Array.Empty<byte>();

    /// <summary>AES-GCM encrypted notes, or null when no notes are stored.</summary>
    public byte[]? NotesEncrypted { get; set; }

    public string? Url { get; set; }

    /// <summary>Optional rotation reminder date. Null when rotation is not tracked.</summary>
    public DateTime? RotationDueAt { get; set; }

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastAccessedAt { get; set; }

    /// <summary>Soft-delete flag; deleted credentials are hidden from normal queries.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Optional tags. Not persisted inside the credential row.</summary>
    public List<string> Tags { get; set; } = new();
}
