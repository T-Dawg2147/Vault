namespace Vault.Core.Models;

/// <summary>
/// A named container (folder/group) for credentials, e.g. IT, Finance, HR, Shared, Executive.
/// Access to credentials is granted at the vault level via <see cref="VaultAccess"/>.
/// </summary>
public class VaultGroup
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsArchived { get; set; }
}
