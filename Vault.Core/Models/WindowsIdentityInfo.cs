namespace Vault.Core.Models;

/// <summary>
/// Snapshot of the current Windows identity used to map to an application user.
/// </summary>
public class WindowsIdentityInfo
{
    /// <summary>Windows SID, e.g. "S-1-5-21-...". Stable identity anchor.</summary>
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
