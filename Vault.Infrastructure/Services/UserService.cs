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
        var user = _repository.FindUserBySid(identity.Sid);

        if (user is null || !user.IsActive)
            return null;

        // Keep directory-sourced fields fresh; never persist secrets here.
        if (!string.Equals(user.Username, identity.Username, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(user.Domain, identity.Domain, StringComparison.OrdinalIgnoreCase))
        {
            user.Username = identity.Username;
            user.Domain = identity.Domain;
            _repository.UpdateUser(user);
        }

        return user;
    }

    public User? GetById(int id) => _repository.FindUserById(id);

    public IReadOnlyList<User> GetAll(User actor)
    {
        _authorization.Require(_authorization.CanManageUsers(actor), "GetAllUsers");
        return _repository.GetAllUsers();
    }

    public User Create(User actor, string windowsSid, string domain, string username, string displayName, UserRole role)
    {
        _authorization.Require(_authorization.CanManageUsers(actor), "CreateUser");

        var user = new User
        {
            WindowsSid = InputValidator.RequiredSid(windowsSid),
            Domain = InputValidator.OptionalText(domain, "Domain", InputValidator.MaxNameLength) ?? string.Empty,
            Username = InputValidator.RequiredAccountName(username, "Username"),
            DisplayName = InputValidator.OptionalText(displayName, "Display name", InputValidator.MaxNameLength) ?? string.Empty,
            Role = role,
            IsActive = true
        };

        var created = _repository.InsertUser(user);
        _audit.Log(AuditAction.UserCreated, actor.Id, "User", created.Id.ToString(),
            $"User '{created.Username}' created with role {created.Role}.");
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
}
