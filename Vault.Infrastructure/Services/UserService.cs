using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;
using Vault.Core.Validation;

namespace Vault.Infrastructure.Services;

/// <summary>
/// Maps the current Windows identity to a registered application user and
/// provides admin-only user management. All mutations are authorized and audited.
/// </summary>
public sealed class UserService : IUserService
{
    private readonly IRepository _repository;
    private readonly IWindowsIdentityService _identityService;
    private readonly IAuthorizationService _authorization;
    private readonly IAuditService _audit;

    public UserService(
        IRepository repository,
        IWindowsIdentityService identityService,
        IAuthorizationService authorization,
        IAuditService audit)
    {
        _repository = repository;
        _identityService = identityService;
        _authorization = authorization;
        _audit = audit;
    }

    public User? GetCurrentUser()
    {
        var identity = _identityService.GetCurrentIdentity();
        var user = _repository.FindUserByDomainUsername(identity.Domain, identity.Username);

        if (user is null)
            user = AutoRegister(identity);

        if (user is null || !user.IsActive)
            return null;

        if (string.IsNullOrEmpty(user.WindowsSid))
        {
            // Admin pre-created the user without a SID: capture it on first successful login.
            user.WindowsSid = identity.Sid;
            _repository.UpdateUser(user);
            _audit.Log(AuditAction.SidCaptured, user.Id, "User", user.Id.ToString(),
                $"SID captured for '{user.Domain}\\{user.Username}' on first login.");
        }
        else if (!string.Equals(user.WindowsSid, identity.Sid, StringComparison.Ordinal))
        {
            // Same Domain\Username, but a different SID than last time we saw them.
            // This can happen if the account was deleted and recreated — treat it as
            // suspicious rather than silently trusting it.
            _audit.Log(AuditAction.IdentityDriftDetected, user.Id, "User", user.Id.ToString(),
                $"SID changed for '{user.Domain}\\{user.Username}'. Stored={user.WindowsSid}, Current={identity.Sid}.");

            // Fail closed: require an admin to confirm/re-link before granting access.
            return null;
        }

        if (!string.Equals(user.DisplayName, identity.DisplayName, StringComparison.Ordinal))
        {
            user.DisplayName = identity.DisplayName;
            _repository.UpdateUser(user);
        }

        return user;
    }

    /// <summary>
    /// Self-registers a first-time Windows user as an active Viewer with no vault access.
    /// Authorization is deny-by-default, so the new user cannot see any credentials until
    /// an admin grants vault access.
    /// </summary>
    private User? AutoRegister(WindowsIdentityInfo identity)
    {
        if (string.IsNullOrWhiteSpace(identity.Username) || string.IsNullOrWhiteSpace(identity.Sid))
            return null;

        try
        {
            var created = _repository.InsertUser(new User
            {
                WindowsSid = identity.Sid,
                Domain = identity.Domain ?? string.Empty,
                Username = identity.Username,
                DisplayName = identity.DisplayName ?? string.Empty,
                Role = UserRole.Viewer,
                IsActive = true
            });
            _audit.Log(AuditAction.UserAutoRegistered, created.Id, "User", created.Id.ToString(),
                $"User '{created.Domain}\\{created.Username}' automatically registered as {created.Role} with no vault access.");
            return created;
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // Lost a race with another instance registering the same user; re-read it.
            return _repository.FindUserByDomainUsername(identity.Domain ?? string.Empty, identity.Username);
        }
    }

    public User? GetById(int id) => _repository.FindUserById(id);

    public IReadOnlyList<User> GetAll(User actor)
    {
        _authorization.Require(_authorization.CanManageUsers(actor), "GetAllUsers");
        return _repository.GetAllUsers();
    }

    public User Create(User actor, string domain, string username, string displayName, UserRole role)
    {
        _authorization.Require(_authorization.CanManageUsers(actor), "CreateUser");

        var user = new User
        {
            WindowsSid = string.Empty,
            Domain = InputValidator.OptionalText(domain, "Domain", InputValidator.MaxNameLength) ?? string.Empty,
            Username = InputValidator.RequiredAccountName(username, "Username"),
            DisplayName = InputValidator.OptionalText(displayName, "Display name", InputValidator.MaxNameLength) ?? string.Empty,
            Role = role,
            IsActive = true
        };

        var created = _repository.InsertUser(user);
        _audit.Log(AuditAction.UserCreated, actor.Id, "User", created.Id.ToString(),
            $"User '{created.Domain}\\{created.Username}' created with role {created.Role}.");
        return created;
    }

    public void ChangeRole(User actor, int targetUserId, UserRole newRole)
    {
        _authorization.Require(_authorization.CanAssignRoles(actor), "ChangeRole");

        var target = _repository.FindUserById(targetUserId)
            ?? throw new ValidationException("User not found.");

        if (target.Id == actor.Id && newRole != UserRole.Admin)
            throw new ValidationException("You cannot remove your own admin role.");

        var previousRole = target.Role;
        target.Role = newRole;
        _repository.UpdateUser(target);

        _audit.Log(AuditAction.UserRoleChanged, actor.Id, "User", target.Id.ToString(),
            $"Role of '{target.Username}' changed from {previousRole} to {newRole}.");
    }

    public void SetActive(User actor, int targetUserId, bool isActive)
    {
        _authorization.Require(_authorization.CanManageUsers(actor), "SetUserActive");

        var target = _repository.FindUserById(targetUserId)
            ?? throw new ValidationException("User not found.");

        if (target.Id == actor.Id && !isActive)
            throw new ValidationException("You cannot deactivate your own account.");

        target.IsActive = isActive;
        _repository.UpdateUser(target);

        _audit.Log(isActive ? AuditAction.UserUpdated : AuditAction.UserDeactivated,
            actor.Id, "User", target.Id.ToString(),
            $"User '{target.Username}' {(isActive ? "reactivated" : "deactivated")}.");
    }

    public void RelinkSid(User actor, int targetUserId, string newWindowsSid)
    {
        _authorization.Require(_authorization.CanManageUsers(actor), "RelinkSid");

        var user = _repository.FindUserById(targetUserId)
            ?? throw new InvalidOperationException("User not found.");

        var oldSid = user.WindowsSid;
        user.WindowsSid = InputValidator.RequiredSid(newWindowsSid);
        _repository.UpdateUser(user);

        _audit.Log(AuditAction.SidRelinked, actor.Id, "User", user.Id.ToString(),
            $"SID for '{user.Domain}\\{user.Username}' changed from {oldSid} to {user.WindowsSid} by admin.");
    }
}
