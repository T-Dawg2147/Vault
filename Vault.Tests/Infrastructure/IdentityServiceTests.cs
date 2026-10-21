using Vault.Core.Validation;
using Vault.Infrastructure.Identity;

namespace Vault.Tests.Infrastructure;

public class IdentityServiceTests
{
    [Fact]
    public void Factory_ReturnsIdentityWithValidStableIdentifier()
    {
        var identity = CurrentUserIdentityServiceFactory.Create().GetCurrentIdentity();

        Assert.False(string.IsNullOrWhiteSpace(identity.Username));
        Assert.Equal(identity.Sid, InputValidator.RequiredSid(identity.Sid));
    }

    [Fact]
    public void PosixIdentity_UsesUserAndMachine()
    {
        var identity = new PosixIdentityService().GetCurrentIdentity();

        Assert.Equal($"posix:{Environment.UserName}@{Environment.MachineName}", identity.Sid);
        Assert.Equal(Environment.UserName, identity.Username);
    }
}
