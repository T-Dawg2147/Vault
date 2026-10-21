namespace Vault.Core.Models;

/// <summary>
/// Snapshot of the current OS user identity used to map to an application user.
/// </summary>
public class PlatformIdentityInfo
{
    /// <summary>Stable identity anchor: a Windows SID ("S-1-5-21-...") on Windows, or "posix:user@machine" on macOS/Linux.</summary>
    public string Sid { get; set; } = string.Empty;

    /// <summary>Domain or machine name portion of the identity.</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Sam-account-name style username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>DOMAIN\user format.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Friendly display name when available from the directory; otherwise empty.</summary>
    public string DisplayName { get; set; } = string.Empty;
}
