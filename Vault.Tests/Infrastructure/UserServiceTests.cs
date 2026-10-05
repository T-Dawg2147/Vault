using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Infrastructure.Services;
using Vault.Security.Authorization;
using Xunit;

namespace Vault.Tests.Infrastructure;

public class UserServiceTests
{
    private sealed class FakeIdentity : IWindowsIdentityService
    {
        public WindowsIdentityInfo Current { get; set; } = new()
        {
            Sid = "S-1-5-21-1-2-3-1001", Domain = "CORP", Username = "alice", DisplayName = "Alice"
        };
        public WindowsIdentityInfo GetCurrentIdentity() => Current;
    }

    private sealed class Fixture
    {
        public global::Vault.Infrastructure.Persistence.SqliteVaultRepository Repository { get; } = TestHelpers.CreateRepository();
        public FakeIdentity Identity { get; } = new();
        public AuditService Audit { get; }
        public UserService Users { get; }
        public User Admin { get; }

        public Fixture()
        {
            var authorization = new AuthorizationService(Repository);
            Audit = new AuditService(Repository, authorization);
            Users = new UserService(Repository, Identity, authorization, Audit);
            Admin = Repository.InsertUser(TestHelpers.MakeUser(0, UserRole.Admin));
        }

        public bool Logged(AuditAction action)
            => Audit.GetRecent(Admin).Any(e => e.Action == action);
    }

    [Fact]
    public void FirstTimeUser_IsAutoRegisteredAsUngrantedViewer()
    {
        var f = new Fixture();

        var user = f.Users.GetCurrentUser();

        Assert.NotNull(user);
        Assert.Equal(UserRole.Viewer, user!.Role);
        Assert.True(user.IsActive);
        Assert.Equal("S-1-5-21-1-2-3-1001", user.WindowsSid);
        Assert.Equal("CORP", user.Domain);
        Assert.Equal("alice", user.Username);
        Assert.Empty(new VaultService(f.Repository, new AuthorizationService(f.Repository), f.Audit).GetAccessibleVaults(user));
        Assert.True(f.Logged(AuditAction.UserAutoRegistered));
    }

    [Fact]
    public void DeactivatedUser_IsDenied()
    {
        var f = new Fixture();
        var user = f.Users.GetCurrentUser()!;
        f.Users.SetActive(f.Admin, user.Id, false);

        Assert.Null(f.Users.GetCurrentUser());
    }

    [Fact]
    public void BlankSid_IsCapturedAndPersistedOnFirstLogin()
    {
        var f = new Fixture();
        var created = f.Users.Create(f.Admin, "CORP", "alice", "Alice", UserRole.Editor);

        var user = f.Users.GetCurrentUser();

        Assert.NotNull(user);
        Assert.Equal(UserRole.Editor, user!.Role);
        Assert.Equal("S-1-5-21-1-2-3-1001", f.Repository.FindUserById(created.Id)!.WindowsSid);
        Assert.True(f.Logged(AuditAction.SidCaptured));
    }

    [Fact]
    public void SidDrift_FailsClosedAndIsLogged()
    {
        var f = new Fixture();
        Assert.NotNull(f.Users.GetCurrentUser());
        f.Identity.Current.Sid = "S-1-5-21-9-9-9-2002";

        Assert.Null(f.Users.GetCurrentUser());
        Assert.True(f.Logged(AuditAction.IdentityDriftDetected));
    }

    [Fact]
    public void RelinkSid_IsPersisted()
    {
        var f = new Fixture();
        var user = f.Users.GetCurrentUser()!;
        f.Users.RelinkSid(f.Admin, user.Id, "S-1-5-21-9-9-9-2002");

        Assert.Equal("S-1-5-21-9-9-9-2002", f.Repository.FindUserById(user.Id)!.WindowsSid);
    }

    [Fact]
    public void MultipleBlankSidUsers_CanBeCreated()
    {
        var f = new Fixture();
        f.Users.Create(f.Admin, "CORP", "bob", "Bob", UserRole.Viewer);
        f.Users.Create(f.Admin, "CORP", "carol", "Carol", UserRole.Viewer);
    }
}
