using Vault.Core.Enums;
using Vault.Core.Models;

namespace Vault.Core.Interfaces;

/// <summary>
/// Persistence abstraction. Implementations store only encrypted secret material.
/// </summary>
public interface IRepository
{
    // Users
    User? FindUserBySid(string windowsSid);
    User? FindUserById(int id);
    IReadOnlyList<User> GetAllUsers();
    User InsertUser(User user);
    void UpdateUser(User user);
    int CountUsers();

    // Vaults
    VaultGroup? FindVaultById(int id);
    IReadOnlyList<VaultGroup> GetAllVaults(bool includeArchived = false);
    VaultGroup InsertVault(VaultGroup vault);
    void UpdateVault(VaultGroup vault);

    // Vault access
    IReadOnlyList<VaultAccess> GetAccessForVault(int vaultId);
    IReadOnlyList<VaultAccess> GetAccessForUser(int userId);
    VaultAccess? FindAccess(int vaultId, int userId);
    VaultAccess InsertAccess(VaultAccess access);
    void UpdateAccess(VaultAccess access);
    void DeleteAccess(int vaultId, int userId);

    // Credentials
    Credential? FindCredentialById(int id);
    IReadOnlyList<Credential> GetCredentialsByVault(int vaultId);
    IReadOnlyList<Credential> GetCredentialsByVaults(IEnumerable<int> vaultIds);
    Credential InsertCredential(Credential credential);
    void UpdateCredential(Credential credential);
    void TouchLastAccessed(int credentialId, DateTime accessedAtUtc);

    // Audit
    void InsertAuditEntry(AuditLogEntry entry);
    IReadOnlyList<AuditLogEntry> GetAuditEntries(int maxCount);
    IReadOnlyList<AuditLogEntry> SearchAuditEntries(string? textFilter, DateTime? fromUtc, DateTime? toUtc, int maxCount);
}
