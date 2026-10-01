using Vault.Core.Enums;
using Vault.Core.Interfaces;
using Vault.Core.Models;

namespace Vault.Infrastructure.Services;

/// <summary>
/// Append-only audit logging. Callers are responsible for never passing secret
/// values in <c>details</c>; this service additionally truncates long details
/// as a defense-in-depth measure.
/// </summary>
public sealed class AuditService : IAuditService
{
    private const int MaxDetailsLength = 2000;

    private readonly IRepository _repository;
    private readonly IAuthorizationService _authorization;

    public AuditService(IRepository repository, IAuthorizationService authorization)
    {
        _repository = repository;
        _authorization = authorization;
    }

    public void Log(AuditAction action, int? userId = null, string? targetType = null,
        string? targetId = null, string? details = null)
    {
        var entry = new AuditLogEntry
        {
            UserId = userId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            Timestamp = DateTime.UtcNow,
            Details = details is { Length: > MaxDetailsLength }
                ? details[..MaxDetailsLength]
                : details
        };

        _repository.InsertAuditEntry(entry);
    }

    public IReadOnlyList<AuditLogEntry> GetRecent(User actor, int count = 500)
    {
        _authorization.Require(_authorization.CanViewAuditLog(actor), "ViewAuditLog");
        return _repository.GetAuditEntries(Math.Clamp(count, 1, 10_000));
    }

    public IReadOnlyList<AuditLogEntry> Search(User actor, string? textFilter, DateTime? from, DateTime? to, int count = 500)
    {
        _authorization.Require(_authorization.CanViewAuditLog(actor), "SearchAuditLog");
        return _repository.SearchAuditEntries(textFilter, from, to, Math.Clamp(count, 1, 10_000));
    }
}
