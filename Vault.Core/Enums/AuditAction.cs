namespace Vault.Core.Enums;

/// <summary>
/// Audited actions. Audit entries must never contain plaintext secrets.
/// </summary>
public enum AuditAction
{
    LoginSucceeded,
    LoginDenied,
    CredentialViewed,
    CredentialCreated,
    CredentialUpdated,
    CredentialDeleted,
    CredentialPasswordRevealed,
    CredentialCopiedToClipboard,
    VaultCreated,
    VaultUpdated,
    VaultArchived,
    VaultAccessGranted,
    VaultAccessRevoked,
    VaultAccessChanged,
    UserCreated,
    UserUpdated,
    UserDeactivated,
    UserRoleChanged,
    ImportStarted,
    ImportCompleted,
    ImportFailed,
    ExportPerformed
}
