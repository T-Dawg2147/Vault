using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;

namespace Vault.Infrastructure.Services;

/// <summary>
/// Controlled first-run provisioning. When the user table is empty, the very first
/// Windows user to launch the application becomes the initial admin. Afterwards the
/// application is deny-by-default and only that admin can register further users.
/// </summary>
public static class AdminProvisioner
{
    public static User EnsureInitialAdmin(
        IRepository repository,
        IWindowsIdentityService identityService,
        IAuditService audit)
    {
        if (repository.CountUsers() > 0)
            throw new InvalidOperationException("Initial admin can only be provisioned on first run.");

        var identity = identityService.GetCurrentIdentity();

        var admin = repository.InsertUser(new User
        {
            WindowsSid = identity.Sid,
            Domain = identity.Domain,
            Username = identity.Username,
            DisplayName = string.IsNullOrWhiteSpace(identity.DisplayName) ? identity.FullName : identity.DisplayName,
            Role = UserRole.Admin,
            IsActive = true
        });

        audit.Log(AuditAction.UserCreated, admin.Id, "User", admin.Id.ToString(),
            "Initial administrator provisioned on first run.");

        return admin;
    }
}
