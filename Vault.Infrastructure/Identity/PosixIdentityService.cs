using Vault.Core.Interfaces;
using Vault.Core.Models;

namespace Vault.Infrastructure.Identity;

/// <summary>
/// Identity for macOS/Linux. The stable identifier is "posix:username@machine", which
/// keeps identities from different machines distinct and satisfies the same uniqueness
/// semantics the Windows SID provides.
/// </summary>
public sealed class PosixIdentityService : ICurrentUserIdentityService
{
    public PlatformIdentityInfo GetCurrentIdentity()
    {
        var username = Environment.UserName;
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException("Could not determine the current user name.");

        var machine = Environment.MachineName;
        var domain = string.IsNullOrWhiteSpace(Environment.UserDomainName) ? machine : Environment.UserDomainName;

        return new PlatformIdentityInfo
        {
            Sid = $"posix:{username}@{machine}",
            Domain = domain,
            Username = username,
            FullName = $"{domain}\\{username}",
            DisplayName = username
        };
    }
}
