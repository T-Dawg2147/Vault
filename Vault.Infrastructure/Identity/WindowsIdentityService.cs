using System.Security.Principal;
using Vault.Core.Interfaces;
using Vault.Core.Models;

namespace Vault.Infrastructure.Identity;

/// <summary>
/// Reads the current Windows identity via <see cref="WindowsIdentity.GetCurrent()"/>.
/// The SID is the stable anchor used to map to an application user record.
/// Only usable on Windows; the application targets a Windows desktop environment.
/// </summary>
public sealed class WindowsIdentityService : IWindowsIdentityService
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public WindowsIdentityInfo GetCurrentIdentity()
    {
        using var identity = WindowsIdentity.GetCurrent();

        var sid = identity.User?.Value
            ?? throw new InvalidOperationException("Could not determine the current Windows user SID.");

        var fullName = identity.Name ?? string.Empty;
        var (domain, username) = SplitName(fullName);

        var displayName = fullName;
        try
        {
            var translated = identity.User?.Translate(typeof(NTAccount))?.ToString();
            if (!string.IsNullOrWhiteSpace(translated))
                displayName = translated;
        }
        catch (IdentityNotMappedException)
        {
            // Some machine-local or service accounts cannot be translated; fall back to the logon name.
        }

        return new WindowsIdentityInfo
        {
            Sid = sid,
            Domain = domain,
            Username = username,
            FullName = fullName,
            DisplayName = displayName
        };
    }

    private static (string Domain, string Username) SplitName(string fullName)
    {
        var separatorIndex = fullName.IndexOf('\\');
        return separatorIndex > 0
            ? (fullName[..separatorIndex], fullName[(separatorIndex + 1)..])
            : (string.Empty, fullName);
    }
}
