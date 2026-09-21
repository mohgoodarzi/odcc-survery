using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;

namespace ODCC.Application.Modules.Audit.Services;

/// <summary>
/// پیاده‌سازی پیش‌فرض سرویس ممیزی.
/// </summary>
public sealed class AuditService(IAuditEntryRepository repository) : IAuditService
{
    private readonly IAuditEntryRepository _repository = repository;

    public async Task LogAsync(AuditEntryDto entry, CancellationToken ct = default)
    {
        var entity = new Domain.Modules.Audit.Entities.AuditEntry
        {
            UserId = entry.UserId,
            UserName = entry.UserName,
            Action = entry.Action,
            EntityType = entry.EntityType,
            EntityId = entry.EntityId,
            Severity = entry.Severity,
            ClientIp = entry.ClientIp,
            Description = entry.Description,
            CreatedAt = entry.OccurredAt == default ? DateTime.UtcNow : entry.OccurredAt
        };

        await _repository.AddAsync(entity, ct);
    }

    public async Task<IReadOnlyList<AuditEntryDto>> SearchAsync(AuditSearchRequest request, CancellationToken ct = default)
    {
        var entries = await _repository.SearchAsync(request, ct);
        return entries.Select(Map).ToList();
    }

    private static AuditEntryDto Map(Domain.Modules.Audit.Entities.AuditEntry e) => new()
    {
        Id = e.Id,
        OccurredAt = e.CreatedAt,
        UserId = e.UserId,
        UserName = e.UserName,
        Action = e.Action,
        EntityType = e.EntityType,
        EntityId = e.EntityId,
        Severity = e.Severity,
        ClientIp = e.ClientIp,
        Description = e.Description
    };
}
