using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Infrastructure.Services;
using Vault.Security.Authorization;
using Xunit;

namespace Vault.Tests.Infrastructure;

/// <summary>
/// End-to-end service-layer tests: encryption at rest, authorization enforcement,
/// and audit emission — using the real SQLite repository and AES-GCM service.
/// </summary>
public class CredentialServiceTests
{
    private sealed class Fixture : IDisposable
    {
        public global::Vault.Infrastructure.Persistence.SqliteVaultRepository Repository { get; }
        public CredentialService Credentials { get; }
        public AuditService Audit { get; }
        public User Admin { get; }
        public User Editor { get; }
        public User Outsider { get; }
        public VaultGroup Vault { get; }

        public Fixture()
        {
            Repository = TestHelpers.CreateRepository();
            var encryption = TestHelpers.CreateEncryption();
            var authorization = new AuthorizationService(Repository);
            Audit = new AuditService(Repository, authorization);
            Credentials = new CredentialService(Repository, authorization, encryption, Audit);

            Admin = Repository.InsertUser(TestHelpers.MakeUser(0, UserRole.Admin));
            Editor = Repository.InsertUser(TestHelpers.MakeUser(0, UserRole.Editor));
            Outsider = Repository.InsertUser(TestHelpers.MakeUser(0, UserRole.Viewer));
            Vault = Repository.InsertVault(new VaultGroup { Name = "IT", CreatedByUserId = Admin.Id });

            Repository.InsertAccess(new VaultAccess
            {
                VaultId = Vault.Id, UserId = Editor.Id,
                Permission = PermissionLevel.Editor, GrantedByUserId = Admin.Id
            });
        }

        public void Dispose() { }
    }

    [Fact]
    public void Create_StoresOnlyEncryptedSecrets()
    {
        var f = new Fixture();
        var created = f.Credentials.Create(f.Admin, f.Vault.Id, "Prod DB", "sa", "TopSecret123!",
            notes: "rotate monthly");

        var raw = f.Repository.FindCredentialById(created.Id)!;
        var rawBytes = System.Text.Encoding.UTF8.GetString(raw.PasswordEncrypted);

        Assert.DoesNotContain("TopSecret123!", rawBytes);
        Assert.NotEmpty(raw.PasswordEncrypted);
        Assert.NotNull(raw.NotesEncrypted);

        // And the round trip works for an authorized user.
        Assert.Equal("TopSecret123!", f.Credentials.RevealPassword(f.Admin, created.Id));
    }

    [Fact]
    public void Editor_CanCreate_InAssignedVault()
    {
        var f = new Fixture();
        var created = f.Credentials.Create(f.Editor, f.Vault.Id, "Svc account", "svc", "pass");

        Assert.True(created.Id > 0);
        Assert.Equal("pass", f.Credentials.RevealPassword(f.Editor, created.Id));
    }

    [Fact]
    public void Outsider_CannotCreateOrReveal()
    {
        var f = new Fixture();
        var created = f.Credentials.Create(f.Admin, f.Vault.Id, "Prod DB", "sa", "secret");

        Assert.Throws<VaultAccessDeniedException>(
            () => f.Credentials.Create(f.Outsider, f.Vault.Id, "X", "y", "z"));
        Assert.Throws<VaultAccessDeniedException>(
            () => f.Credentials.RevealPassword(f.Outsider, created.Id));
        Assert.Throws<VaultAccessDeniedException>(
            () => f.Credentials.ListByVault(f.Outsider, f.Vault.Id));
    }

    [Fact]
    public void Update_WithNullPassword_KeepsExistingPassword()
    {
        var f = new Fixture();
        var created = f.Credentials.Create(f.Admin, f.Vault.Id, "Prod DB", "sa", "original");

        f.Credentials.Update(f.Admin, created.Id, "Prod DB (renamed)", "sa",
            newPassword: null, url: "https://db.internal", notes: null, tags: new[] { "prod" }, rotationDueAt: null);

        Assert.Equal("original", f.Credentials.RevealPassword(f.Admin, created.Id));
        var reloaded = f.Credentials.Search(f.Admin, "renamed");
        Assert.Single(reloaded);
    }

    [Fact]
    public void RevealPassword_WritesAuditEntry_WithoutSecret()
    {
        var f = new Fixture();
        var created = f.Credentials.Create(f.Admin, f.Vault.Id, "Prod DB", "sa", "audit-me-secret");

        f.Credentials.RevealPassword(f.Admin, created.Id);

        var entries = f.Repository.GetAuditEntries(100);
        var reveal = entries.First(e => e.Action == AuditAction.CredentialPasswordRevealed);
        Assert.Equal(created.Id.ToString(), reveal.TargetId);
        Assert.DoesNotContain("audit-me-secret", reveal.Details ?? string.Empty);
    }

    [Fact]
    public void Search_FiltersByLabelUsernameUrlTag()
    {
        var f = new Fixture();
        f.Credentials.Create(f.Admin, f.Vault.Id, "Mail server", "postmaster", "p1",
            url: "https://mail.internal", tags: new[] { "mail" });
        f.Credentials.Create(f.Admin, f.Vault.Id, "File share", "fs-admin", "p2",
            url: "https://files.internal");

        Assert.Single(f.Credentials.Search(f.Admin, "mail server"));
        Assert.Single(f.Credentials.Search(f.Admin, "fs-admin"));
        Assert.Single(f.Credentials.Search(f.Admin, "files.internal"));
        Assert.Single(f.Credentials.Search(f.Admin, "mail"));
        Assert.Equal(2, f.Credentials.Search(f.Admin, string.Empty).Count);
    }

    [Fact]
    public void Search_NeverLeaksCredentials_FromInaccessibleVaults()
    {
        var f = new Fixture();
        f.Credentials.Create(f.Admin, f.Vault.Id, "Hidden secret store", "x", "p");

        Assert.Empty(f.Credentials.Search(f.Outsider, "Hidden"));
        Assert.Empty(f.Credentials.Search(f.Outsider, string.Empty));
    }
}
