namespace Vault.Core.Enums;

/// <summary>
/// Permission level granted to a user on a specific vault.
/// </summary>
public enum PermissionLevel
{
    /// <summary>Can view credentials in the vault.</summary>
    Viewer = 0,

    /// <summary>Can view, create and edit credentials in the vault.</summary>
    Editor = 1
}
