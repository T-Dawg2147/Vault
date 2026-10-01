using Vault.Core.Enums;
using Vault.Core.Models;
using Xunit;

namespace Vault.Tests.Infrastructure;

public class RepositoryTests
{
    [Fact]
    public void User_Crud_RoundTrips()
    {
        var repo = TestHelpers.CreateRepository();
        var user = repo.InsertUser(TestHelpers.MakeUser(0, UserRole.Editor));

        Assert.True(user.Id > 0);

        var byId = repo.FindUserById(user.Id);
        var bySid = repo.FindUserBySid(user.WindowsSid);
        Assert.Equal("CORP", byId!.Domain);
        Assert.Equal(user.Id, bySid!.Id);
        Assert.Equal(UserRole.Editor, bySid.Role);

        user.Role = UserRole.Admin;
        repo.UpdateUser(user);
        Assert.Equal(UserRole.Admin, repo.FindUserById(user.Id)!.Role);

        Assert.Equal(1, repo.CountUsers());
        Assert.Single(repo.GetAllUsers());
    }

    [Fact]
    public void Vault_And_Access_RoundTrip()
    {
        var repo = TestHelpers.CreateRepository();
        var admin = repo.InsertUser(TestHelpers.MakeUser(0, UserRole.Admin));
        var viewer = repo.InsertUser(TestHelpers.MakeUser(0, UserRole.Viewer));

        var vault = repo.InsertVault(new VaultGroup
        {
            Name = "Finance", Description = "Finance systems", CreatedByUserId = admin.Id
        });

        Assert.False(repo.FindVaultById(vault.Id)!.IsArchived);
        Assert.Single(repo.GetAllVaults());

        var access = repo.InsertAccess(new VaultAccess
        {
            VaultId = vault.Id, UserId = viewer.Id,
            Permission = PermissionLevel.Viewer, GrantedByUserId = admin.Id
        });

        Assert.Equal(PermissionLevel.Viewer, repo.FindAccess(vault.Id, viewer.Id)!.Permission);
        Assert.Single(repo.GetAccessForVault(vault.Id));
        Assert.Single(repo.GetAccessForUser(viewer.Id));

        access.Permission = PermissionLevel.Editor;
        repo.UpdateAccess(access);
        Assert.Equal(PermissionLevel.Editor, repo.FindAccess(vault.Id, viewer.Id)!.Permission);

        repo.DeleteAccess(vault.Id, viewer.Id);
        Assert.Null(repo.FindAccess(vault.Id, viewer.Id));

        vault.IsArchived = true;
        repo.UpdateVault(vault);
        Assert.Empty(repo.GetAllVaults());
        Assert.Single(repo.GetAllVaults(includeArchived: true));
    }

    [Fact]
    public void Credential_Crud_SoftDelete_AndLastAccessed()
    {
        var repo = TestHelpers.CreateRepository();
        var admin = repo.InsertUser(TestHelpers.MakeUser(0, UserRole.Admin));
        var vault = repo.InsertVault(new VaultGroup { Name = "IT", CreatedByUserId = admin.Id });

        var credential = repo.InsertCredential(new Credential
        {
            VaultId = vault.Id,
            Label = "Prod SQL",
            UsernameOrEmail = "sa",
            PasswordEncrypted = new byte[] { 9, 9, 9 },
            NotesEncrypted = new byte[] { 1 },
            Url = "https://db.internal",
            Tags = new List<string> { "prod", "sql" },
            CreatedByUserId = admin.Id
        });

        var loaded = repo.FindCredentialById(credential.Id)!;
        Assert.Equal("Prod SQL", loaded.Label);
        Assert.Equal(new byte[] { 9, 9, 9 }, loaded.PasswordEncrypted);
        Assert.Equal(2, loaded.Tags.Count);
        Assert.Single(repo.GetCredentialsByVault(vault.Id));
        Assert.Single(repo.GetCredentialsByVaults(new[] { vault.Id }));

        var accessedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        repo.TouchLastAccessed(credential.Id, accessedAt);
        Assert.Equal(accessedAt, repo.FindCredentialById(credential.Id)!.LastAccessedAt);

        loaded.IsDeleted = true;
        repo.UpdateCredential(loaded);
        Assert.Null(repo.FindCredentialById(credential.Id));
        Assert.Empty(repo.GetCredentialsByVault(vault.Id));
    }

    [Fact]
    public void Audit_Insert_And_Search()
    {
        var repo = TestHelpers.CreateRepository();
        var admin = repo.InsertUser(TestHelpers.MakeUser(0, UserRole.Admin));

        repo.InsertAuditEntry(new AuditLogEntry
        {
            UserId = admin.Id, Action = AuditAction.LoginSucceeded,
            TargetType = "User", TargetId = admin.Id.ToString(), Details = "signed in"
        });
        repo.InsertAuditEntry(new AuditLogEntry
        {
            UserId = admin.Id, Action = AuditAction.CredentialCreated,
            TargetType = "Credential", TargetId = "42", Details = "Credential 'Prod SQL' created"
        });

        var all = repo.GetAuditEntries(100);
        Assert.Equal(2, all.Count);

        var filtered = repo.SearchAuditEntries("Prod SQL", null, null, 100);
        Assert.Single(filtered);
        Assert.Equal(AuditAction.CredentialCreated, filtered[0].Action);

        var future = repo.SearchAuditEntries(null, DateTime.UtcNow.AddDays(1), null, 100);
        Assert.Empty(future);
    }
}
