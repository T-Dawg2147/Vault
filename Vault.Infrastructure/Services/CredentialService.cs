using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Core.Validation;

namespace Vault.Infrastructure.Services;

/// <summary>
/// Credential CRUD with service-layer authorization, per-record AES-GCM encryption,
/// and audit logging. Plaintext secrets only exist transiently in method scope and
/// are never logged or included in exception messages.
/// </summary>
public sealed class CredentialService : ICredentialService
{
    private readonly IRepository _repository;
    private readonly IAuthorizationService _authorization;
    private readonly IEncryptionService _encryption;
    private readonly IAuditService _audit;

    public CredentialService(
        IRepository repository,
        IAuthorizationService authorization,
        IEncryptionService encryption,
        IAuditService audit)
    {
        _repository = repository;
        _authorization = authorization;
        _encryption = encryption;
        _audit = audit;
    }

    public IReadOnlyList<Credential> ListByVault(User user, int vaultId)
    {
        _authorization.Require(_authorization.CanViewVault(user, vaultId), "ListCredentials");
        return _repository.GetCredentialsByVault(vaultId);
    }

    public IReadOnlyList<Credential> Search(User user, string query)
    {
        var accessibleVaultIds = _repository.GetAllVaults()
            .Where(v => _authorization.CanViewVault(user, v.Id))
            .Select(v => v.Id)
            .ToList();

        var candidates = _repository.GetCredentialsByVaults(accessibleVaultIds);

        if (string.IsNullOrWhiteSpace(query))
            return candidates;

        var term = query.Trim();
        return candidates.Where(c =>
                c.Label.Contains(term, StringComparison.OrdinalIgnoreCase)
                || c.UsernameOrEmail.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (c.Url?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
                || c.Tags.Any(t => t.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public Credential Create(User user, int vaultId, string label, string usernameOrEmail,
        string password, string? url = null, string? notes = null,
        IEnumerable<string>? tags = null, DateTime? rotationDueAt = null)
    {
        _authorization.Require(_authorization.CanEditVault(user, vaultId), "CreateCredential");

        var credential = new Credential
        {
            VaultId = vaultId,
            Label = InputValidator.RequiredText(label, "Label", InputValidator.MaxLabelLength),
            UsernameOrEmail = InputValidator.OptionalText(usernameOrEmail, "Username", InputValidator.MaxUsernameLength) ?? string.Empty,
            PasswordEncrypted = _encryption.Encrypt(InputValidator.RequiredPassword(password)),
            NotesEncrypted = EncryptOptional(notes),
            Url = InputValidator.OptionalUrl(url),
            Tags = InputValidator.NormalizeTags(tags).ToList(),
            RotationDueAt = rotationDueAt,
            CreatedByUserId = user.Id
        };

        var created = _repository.InsertCredential(credential);
        _audit.Log(AuditAction.CredentialCreated, user.Id, "Credential", created.Id.ToString(),
            $"Credential '{created.Label}' created in vault #{vaultId}.");
        return created;
    }

    public void Update(User user, int credentialId, string label, string usernameOrEmail,
        string? newPassword, string? url, string? notes,
        IEnumerable<string>? tags, DateTime? rotationDueAt)
    {
        var credential = _repository.FindCredentialById(credentialId)
            ?? throw new ValidationException("Credential not found.");

        _authorization.Require(_authorization.CanEditCredential(user, credential), "UpdateCredential");

        credential.Label = InputValidator.RequiredText(label, "Label", InputValidator.MaxLabelLength);
        credential.UsernameOrEmail = InputValidator.OptionalText(usernameOrEmail, "Username", InputValidator.MaxUsernameLength) ?? string.Empty;
        credential.Url = InputValidator.OptionalUrl(url);
        credential.Tags = InputValidator.NormalizeTags(tags).ToList();
        credential.RotationDueAt = rotationDueAt;
        credential.NotesEncrypted = EncryptOptional(notes);
        credential.UpdatedAt = DateTime.UtcNow;

        // A null password means "keep the existing password"; a non-null one re-encrypts.
        if (newPassword is not null)
            credential.PasswordEncrypted = _encryption.Encrypt(InputValidator.RequiredPassword(newPassword));

        _repository.UpdateCredential(credential);
        _audit.Log(AuditAction.CredentialUpdated, user.Id, "Credential", credential.Id.ToString(),
            $"Credential '{credential.Label}' updated.");
    }

    public void Delete(User user, int credentialId)
    {
        var credential = _repository.FindCredentialById(credentialId)
            ?? throw new ValidationException("Credential not found.");

        _authorization.Require(_authorization.CanDeleteCredential(user, credential), "DeleteCredential");

        credential.IsDeleted = true;
        credential.UpdatedAt = DateTime.UtcNow;
        _repository.UpdateCredential(credential);

        _audit.Log(AuditAction.CredentialDeleted, user.Id, "Credential", credential.Id.ToString(),
            $"Credential '{credential.Label}' deleted.");
    }

    public string RevealPassword(User user, int credentialId)
    {
        var credential = _repository.FindCredentialById(credentialId)
            ?? throw new ValidationException("Credential not found.");

        _authorization.Require(_authorization.CanViewCredential(user, credential), "RevealPassword");

        _repository.TouchLastAccessed(credential.Id, DateTime.UtcNow);
        _audit.Log(AuditAction.CredentialPasswordRevealed, user.Id, "Credential", credential.Id.ToString(),
            $"Password of '{credential.Label}' revealed.");

        return _encryption.Decrypt(credential.PasswordEncrypted);
    }

    public string? RevealNotes(User user, int credentialId)
    {
        var credential = _repository.FindCredentialById(credentialId)
            ?? throw new ValidationException("Credential not found.");

        _authorization.Require(_authorization.CanViewCredential(user, credential), "RevealNotes");

        _audit.Log(AuditAction.CredentialViewed, user.Id, "Credential", credential.Id.ToString(),
            $"Credential '{credential.Label}' viewed.");

        return credential.NotesEncrypted is null ? null : _encryption.Decrypt(credential.NotesEncrypted);
    }

    private byte[]? EncryptOptional(string? value)
        => string.IsNullOrEmpty(value) ? null : _encryption.Encrypt(value);
}
