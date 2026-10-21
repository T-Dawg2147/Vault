using Vault.Core.Interfaces;

namespace Vault.Infrastructure.Identity;

/// <summary>Selects the identity implementation for the running operating system.</summary>
public static class CurrentUserIdentityServiceFactory
{
    public static ICurrentUserIdentityService Create()
        => OperatingSystem.IsWindows()
            ? new WindowsIdentityService()
            : new PosixIdentityService();
}
