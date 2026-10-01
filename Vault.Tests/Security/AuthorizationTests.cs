using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Security.Authorization;
using Xunit;

namespace Vault.Tests.Security;

/// <summary>
/// Authorization is deny-by-default and centralized. These tests pin the permission matrix.
/// </summary>
public class AuthorizationTests : IDisposable
{
    private readonly global::Vault.Infrastructure.Persistence.SqliteVaultRepository _repository;
    private readonly AuthorizationService _authorization;
    private readonly User _admin;
    private readonly User _editor;
    private readonly User _viewer;
    private readonly User _outsider;
    private readonly VaultGroup _vault;

    public AuthorizationTests()
    {
        _repository = TestHelpers.CreateRepository();
        _authorization = new AuthorizationService(_repository);

        _admin = _repository.InsertUser(TestHelpers.MakeUser(0, UserRole.Admin));
        _editor = _repository.InsertUser(TestHelpers.MakeUser(0, UserRole.Editor));
        _viewer = _repository.InsertUser(TestHelpers.MakeUser(0, UserRole.Viewer));
        _outsider = _repository.InsertUser(TestHelpers.MakeUser(0, UserRole.Viewer));

        _vault = _repository.InsertVault(new VaultGroup
        {
            Name = "IT",
            Description = "IT vault",
            CreatedByUserId = _admin.Id
        });

        _repository.InsertAccess(new VaultAccess
        {
            VaultId = _vault.Id, UserId = _editor.Id,
            Permission = PermissionLevel.Editor, GrantedByUserId = _admin.Id
        });
        _repository.InsertAccess(new VaultAccess
        {
            VaultId = _vault.Id, UserId = _viewer.Id,
            Permission = PermissionLevel.Viewer, GrantedByUserId = _admin.Id
        });
        // _outsider has no vault access grant.
    }

    private Credential CredentialInVault() => new()
    {
        VaultId = _vault.Id, Label = "Test", UsernameOrEmail = "svc",
        PasswordEncrypted = new byte[] { 1, 2, 3 }, CreatedByUserId = _admin.Id
    };

    [Fact]
    public void Admin_CanDoEverything()
    {
        Assert.True(_authorization.CanManageUsers(_admin));
        Assert.True(_authorization.CanAssignRoles(_admin));
        Assert.True(_authorization.CanViewAuditLog(_admin));
        Assert.True(_authorization.CanImport(_admin));
        Assert.True(_authorization.CanManageVaults(_admin));
        Assert.True(_authorization.CanViewVault(_admin, _vault.Id));
        Assert.True(_authorization.CanEditVault(_admin, _vault.Id));
        Assert.True(_authorization.CanViewCredential(_admin, CredentialInVault()));
        Assert.True(_authorization.CanEditCredential(_admin, CredentialInVault()));
    }

    [Fact]
    public void Viewer_CanViewButNotEdit_AssignedVault()
    {
        Assert.True(_authorization.CanViewVault(_viewer, _vault.Id));
        Assert.True(_authorization.CanViewCredential(_viewer, CredentialInVault()));
        Assert.False(_authorization.CanEditVault(_viewer, _vault.Id));
        Assert.False(_authorization.CanEditCredential(_viewer, CredentialInVault()));
        Assert.False(_authorization.CanDeleteCredential(_viewer, CredentialInVault()));
    }

    [Fact]
    public void Editor_CanViewAndEdit_AssignedVault()
    {
        Assert.True(_authorization.CanViewVault(_editor, _vault.Id));
        Assert.True(_authorization.CanEditVault(_editor, _vault.Id));
        Assert.True(_authorization.CanEditCredential(_editor, CredentialInVault()));
        Assert.True(_authorization.CanDeleteCredential(_editor, CredentialInVault()));
    }

    [Fact]
    public void Outsider_IsDeniedByDefault()
    {
        Assert.False(_authorization.CanViewVault(_outsider, _vault.Id));
        Assert.False(_authorization.CanEditVault(_outsider, _vault.Id));
        Assert.False(_authorization.CanViewCredential(_outsider, CredentialInVault()));
        Assert.False(_authorization.CanEditCredential(_outsider, CredentialInVault()));
    }

    [Fact]
    public void NonAdmin_CannotManageUsersOrRolesOrImport()
    {
        foreach (var user in new[] { _editor, _viewer, _outsider })
        {
            Assert.False(_authorization.CanManageUsers(user));
            Assert.False(_authorization.CanAssignRoles(user));
            Assert.False(_authorization.CanManageVaults(user));
            Assert.False(_authorization.CanViewAuditLog(user));
            Assert.False(_authorization.CanImport(user));
        }
    }

    [Fact]
    public void InactiveUser_IsDeniedEverything()
    {
        var inactiveAdmin = TestHelpers.MakeUser(_admin.Id, UserRole.Admin, isActive: false);

        Assert.False(_authorization.CanManageUsers(inactiveAdmin));
        Assert.False(_authorization.CanViewVault(inactiveAdmin, _vault.Id));
        Assert.False(_authorization.CanViewCredential(inactiveAdmin, CredentialInVault()));
    }

    [Fact]
    public void DeletedCredential_CannotBeViewedOrEdited()
    {
        var deleted = CredentialInVault();
        deleted.IsDeleted = true;

        Assert.False(_authorization.CanViewCredential(_admin, deleted));
        Assert.False(_authorization.CanEditCredential(_admin, deleted));
    }

    [Fact]
    public void Require_ThrowsVaultAccessDenied_WhenNotAllowed()
    {
        Assert.Throws<VaultAccessDeniedException>(
            () => _authorization.Require(_authorization.CanViewVault(_outsider, _vault.Id), "ViewVault"));
    }

    [Fact]
    public void Require_DoesNotThrow_WhenAllowed()
    {
        _authorization.Require(_authorization.CanViewVault(_admin, _vault.Id), "ViewVault");
    }

    public void Dispose() { }
}
