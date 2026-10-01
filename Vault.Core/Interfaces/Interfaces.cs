using Vault.Core.Enums;
using Vault.Core.Models;

namespace Vault.Core.Interfaces;

/// <summary>
/// Provides information about the currently logged-on Windows user.
/// </summary>
public interface IWindowsIdentityService
{
    WindowsIdentityInfo GetCurrentIdentity();
}

/// <summary>
/// Authenticated-encryption service for sensitive fields (passwords, notes).
/// Implementations must use authenticated encryption (AES-GCM) and never log plaintext.
/// </summary>
public interface IEncryptionService
{
    byte[] Encrypt(string plaintext);

    /// <summary>Throws <see cref="System.Security.Cryptography.CryptographicException"/> on tampered/invalid input.</summary>
    string Decrypt(byte[] ciphertext);
}

/// <summary>
/// Centralized authorization. Deny-by-default: every check returns false unless an
/// explicit rule grants access. All permission decisions in the app must go through here.
/// </summary>
public interface IAuthorizationService
{
    bool CanViewVault(User user, int vaultId);
    bool CanEditVault(User user, int vaultId);
    bool CanManageVaults(User user);
    bool CanViewCredential(User user, Credential credential);
    bool CanEditCredential(User user, Credential credential);
    bool CanDeleteCredential(User user, Credential credential);
    bool CanManageUsers(User user);
    bool CanAssignRoles(User user);
    bool CanViewAuditLog(User user);
    bool CanImport(User user);

    /// <summary>Throws <see cref="VaultAccessDeniedException"/> when the check fails.</summary>
    void Require(bool allowed, string operation);
}

/// <summary>Thrown when an authorization check fails in the service layer.</summary>
public class VaultAccessDeniedException : Exception
{
    public VaultAccessDeniedException(string operation)
        : base($"Access denied for operation: {operation}")
    {
        Operation = operation;
    }

    public string Operation { get; }
}

/// <summary>
/// Resolves the current Windows user to a registered application user and manages users.
/// </summary>
public interface IUserService
{
    /// <summary>Returns the active application user for the current Windows identity, or null when unregistered/inactive.</summary>
    User? GetCurrentUser();

    User? GetById(int id);

    /// <summary>Admin-only listing of all registered users.</summary>
    IReadOnlyList<User> GetAll(User actor);

    /// <summary>Admin-only. Creates a user record for a Windows identity.</summary>
    User Create(User actor, string windowsSid, string domain, string username, string displayName, UserRole role);

    /// <summary>Admin-only.</summary>
    void ChangeRole(User actor, int targetUserId, UserRole newRole);

    /// <summary>Admin-only.</summary>
    void SetActive(User actor, int targetUserId, bool isActive);
}

public interface IVaultService
{
    IReadOnlyList<Models.VaultGroup> GetAccessibleVaults(User user);
    IReadOnlyList<Models.VaultGroup> GetAllVaults(User actor);

    VaultGroup Create(User actor, string name, string? description);
    void Update(User actor, int vaultId, string name, string? description);
    void Archive(User actor, int vaultId);

    IReadOnlyList<VaultAccess> GetAccessList(User actor, int vaultId);
    void GrantAccess(User actor, int vaultId, int userId, PermissionLevel level);
    void RevokeAccess(User actor, int vaultId, int userId);
}

/// <summary>
/// Credential operations. Every method enforces authorization in the service layer;
/// callers receive decrypted secrets only when explicitly requested and permitted.
/// </summary>
public interface ICredentialService
{
    IReadOnlyList<Credential> ListByVault(User user, int vaultId);

    /// <summary>Search authorized vaults by label, username, URL or tag.</summary>
    IReadOnlyList<Credential> Search(User user, string query);

    Credential Create(User user, int vaultId, string label, string usernameOrEmail,
        string password, string? url = null, string? notes = null,
        IEnumerable<string>? tags = null, DateTime? rotationDueAt = null);

    void Update(User user, int credentialId, string label, string usernameOrEmail,
        string? newPassword, string? url, string? notes,
        IEnumerable<string>? tags, DateTime? rotationDueAt);

    void Delete(User user, int credentialId);

    /// <summary>Decrypts and returns the password. Audited. Only call on explicit user request.</summary>
    string RevealPassword(User user, int credentialId);

    /// <summary>Decrypts and returns notes, or null. Audited via credential view.</summary>
    string? RevealNotes(User user, int credentialId);
}

public interface IAuditService
{
    /// <summary>Writes an audit entry. Details must never contain plaintext secrets.</summary>
    void Log(AuditAction action, int? userId = null, string? targetType = null,
        string? targetId = null, string? details = null);

    IReadOnlyList<AuditLogEntry> GetRecent(User actor, int count = 500);
    IReadOnlyList<AuditLogEntry> Search(User actor, string? textFilter, DateTime? from, DateTime? to, int count = 500);
}

/// <summary>
/// Admin-only migration path for legacy CSV/TXT/XLS/XLSX password files.
/// </summary>
public interface IImportService
{
    /// <summary>Reads the source file and returns raw column headers for mapping.</summary>
    IReadOnlyList<string> ReadColumns(string filePath, bool hasHeaderRow);

    /// <summary>Parses and validates rows using the supplied column mapping. Plaintext lives only in the returned preview.</summary>
    ImportPreview Preview(string filePath, ImportColumnMapping mapping);

    /// <summary>Encrypts and persists valid rows into the target vault. Audited.</summary>
    int Commit(User actor, int vaultId, ImportPreview preview);
}
