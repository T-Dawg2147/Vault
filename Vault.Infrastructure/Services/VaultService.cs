using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Core.Validation;

namespace Vault.Infrastructure.Services;

/// <summary>
/// Vault management and vault access assignments. All mutations require admin
/// rights and are audited.
/// </summary>
public sealed class VaultService : IVaultService
{
    private readonly IRepository _repository;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditService _audit;

    public VaultService(IRepository repository, IAuthorizationService authorization, IAuditService audit)
    {
        _repository = repository;
        _authorization = authorization;
        _audit = audit;
    }

    public IReadOnlyList<VaultGroup> GetAccessibleVaults(User user)
    {
        var all = _repository.GetAllVaults();
        return all.Where(v => _authorization.CanViewVault(user, v.Id)).ToList();
    }

    public IReadOnlyList<VaultGroup> GetAllVaults(User actor)
    {
        _authorization.Require(_authorization.CanManageVaults(actor), "GetAllVaults");
        return _repository.GetAllVaults(includeArchived: true);
    }

    public VaultGroup Create(User actor, string name, string? description)
    {
        _authorization.Require(_authorization.CanManageVaults(actor), "CreateVault");

        var vault = new VaultGroup
        {
            Name = InputValidator.RequiredText(name, "Vault name", InputValidator.MaxNameLength),
            Description = InputValidator.OptionalText(description, "Description", 1000) ?? string.Empty,
            CreatedByUserId = actor.Id
        };

        var created = _repository.InsertVault(vault);
        _audit.Log(AuditAction.VaultCreated, actor.Id, "Vault", created.Id.ToString(),
            $"Vault '{created.Name}' created.");
        return created;
    }

    public void Update(User actor, int vaultId, string name, string? description)
    {
        _authorization.Require(_authorization.CanManageVaults(actor), "UpdateVault");

        var vault = _repository.FindVaultById(vaultId)
            ?? throw new ValidationException("Vault not found.");

        vault.Name = InputValidator.RequiredText(name, "Vault name", InputValidator.MaxNameLength);
        vault.Description = InputValidator.OptionalText(description, "Description", 1000) ?? string.Empty;
        _repository.UpdateVault(vault);

        _audit.Log(AuditAction.VaultUpdated, actor.Id, "Vault", vault.Id.ToString(),
            $"Vault '{vault.Name}' updated.");
    }

    public void Archive(User actor, int vaultId)
    {
        _authorization.Require(_authorization.CanManageVaults(actor), "ArchiveVault");

        var vault = _repository.FindVaultById(vaultId)
            ?? throw new ValidationException("Vault not found.");

        vault.IsArchived = true;
        _repository.UpdateVault(vault);

        _audit.Log(AuditAction.VaultArchived, actor.Id, "Vault", vault.Id.ToString(),
            $"Vault '{vault.Name}' archived.");
    }

    public IReadOnlyList<VaultAccess> GetAccessList(User actor, int vaultId)
    {
        _authorization.Require(_authorization.CanManageVaults(actor), "GetVaultAccessList");
        return _repository.GetAccessForVault(vaultId);
    }

    public void GrantAccess(User actor, int vaultId, int userId, PermissionLevel level)
    {
        _authorization.Require(_authorization.CanManageVaults(actor), "GrantVaultAccess");

        if (_repository.FindVaultById(vaultId) is null)
            throw new ValidationException("Vault not found.");
        if (_repository.FindUserById(userId) is null)
            throw new ValidationException("User not found.");

        var existing = _repository.FindAccess(vaultId, userId);
        if (existing is not null)
        {
            existing.Permission = level;
            _repository.UpdateAccess(existing);
            _audit.Log(AuditAction.VaultAccessChanged, actor.Id, "Vault", vaultId.ToString(),
                $"Access for user #{userId} changed to {level}.");
            return;
        }

        _repository.InsertAccess(new VaultAccess
        {
            VaultId = vaultId,
            UserId = userId,
            Permission = level,
            GrantedByUserId = actor.Id
        });

        _audit.Log(AuditAction.VaultAccessGranted, actor.Id, "Vault", vaultId.ToString(),
            $"Access for user #{userId} granted at level {level}.");
    }

    public void RevokeAccess(User actor, int vaultId, int userId)
    {
        _authorization.Require(_authorization.CanManageVaults(actor), "RevokeVaultAccess");
        _repository.DeleteAccess(vaultId, userId);

        _audit.Log(AuditAction.VaultAccessRevoked, actor.Id, "Vault", vaultId.ToString(),
            $"Access for user #{userId} revoked.");
    }
}
