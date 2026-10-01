namespace Vault.Core.Enums;

/// <summary>
/// Application-wide role for a user. Roles are ordered from most to least privileged.
/// Authorization is deny-by-default: a role only grants the permissions listed for it.
/// </summary>
public enum UserRole
{
    /// <summary>Read-only access to credentials in assigned vaults.</summary>
    Viewer = 0,

    /// <summary>Can create and edit credentials in assigned vaults.</summary>
    Editor = 1,

    /// <summary>Full access: manage users, roles, vaults, vault access, import/export, audit log.</summary>
    Admin = 2
}
