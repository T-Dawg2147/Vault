using Vault.Core.Enums;

namespace Vault.Core.Models;

/// <summary>
/// An application user mapped to a Windows identity.
/// The Windows SID is the stable identity anchor; usernames and domains can change.
/// </summary>
public class User
{
    public int Id { get; set; }

    /// <summary>Windows security identifier, e.g. "S-1-5-21-...". Unique.</summary>
    public string WindowsSid { get; set; } = string.Empty;

    public string Domain { get; set; } = string.Empty;

    public string Username { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; } = UserRole.Viewer;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
