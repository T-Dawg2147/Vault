using Vault.Core.Enums;

namespace Vault.Core.Models;

/// <summary>
/// Grants a user access to a vault at a given permission level.
/// </summary>
public class VaultAccess
{
    public int Id { get; set; }

    public int VaultId { get; set; }

    public int UserId { get; set; }

    public PermissionLevel Permission { get; set; } = PermissionLevel.Viewer;

    public int GrantedByUserId { get; set; }

    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
}
