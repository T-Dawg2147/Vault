using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;

namespace Vault.Security.Authorization;

/// <summary>
/// Centralized, deny-by-default authorization.
///
/// Rules:
/// - Inactive users can do nothing.
/// - Admins can do everything.
/// - Editors/Viewers act only within vaults they are explicitly granted access to,
///   at the granted permission level (Editor &gt;= Viewer).
///
/// All service-layer methods must call this service; UI hiding alone is not security.
/// </summary>
public sealed class AuthorizationService : IAuthorizationService
{
    private readonly IRepository _repository;

    public AuthorizationService(IRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public bool CanViewVault(User user, int vaultId)
        => IsActive(user) && (IsAdmin(user) || GetPermission(user, vaultId) is not null);

    public bool CanEditVault(User user, int vaultId)
        => IsActive(user) && (IsAdmin(user) || GetPermission(user, vaultId) == PermissionLevel.Editor);

    public bool CanManageVaults(User user)
        => IsActive(user) && IsAdmin(user);

    public bool CanViewCredential(User user, Credential credential)
        => IsActive(user) && credential is not null && !credential.IsDeleted
           && (IsAdmin(user) || GetPermission(user, credential.VaultId) is not null);

    public bool CanEditCredential(User user, Credential credential)
        => IsActive(user) && credential is not null && !credential.IsDeleted
           && (IsAdmin(user) || GetPermission(user, credential.VaultId) == PermissionLevel.Editor);

    public bool CanDeleteCredential(User user, Credential credential)
        => CanEditCredential(user, credential);

    public bool CanManageUsers(User user)
        => IsActive(user) && IsAdmin(user);

    public bool CanAssignRoles(User user)
        => IsActive(user) && IsAdmin(user);

    public bool CanViewAuditLog(User user)
        => IsActive(user) && IsAdmin(user);

    public bool CanImport(User user)
        => IsActive(user) && IsAdmin(user);

    public void Require(bool allowed, string operation)
    {
        if (!allowed)
            throw new VaultAccessDeniedException(operation);
    }

    private static bool IsActive(User? user) => user is not null && user.IsActive;

    private static bool IsAdmin(User user) => user.Role == UserRole.Admin;

    private PermissionLevel? GetPermission(User user, int vaultId)
        => _repository.FindAccess(vaultId, user.Id)?.Permission;
}
