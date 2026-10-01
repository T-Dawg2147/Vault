using Vault.Core.Enums;

namespace Vault.Core.Models;

/// <summary>
/// An audit trail entry. <see cref="Details"/> must never contain plaintext secrets.
/// </summary>
public class AuditLogEntry
{
    public long Id { get; set; }

    /// <summary>Null only when the action could not be tied to a known user (e.g. denied login of an unregistered Windows user).</summary>
    public int? UserId { get; set; }

    public AuditAction Action { get; set; }

    /// <summary>Type of the affected entity, e.g. "Credential", "Vault", "User".</summary>
    public string? TargetType { get; set; }

    public string? TargetId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Free-form, non-secret context. Never store passwords or decrypted notes here.</summary>
    public string? Details { get; set; }
}
