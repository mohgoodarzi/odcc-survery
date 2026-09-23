using Microsoft.EntityFrameworkCore;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Modules.Audit.Entities;
using ODCC.Infrastructure.Persistence.Audit;

namespace ODCC.Infrastructure.Repositories.Audit;

/// <summary>
/// مخزن ماژول ممیزی روی <see cref="AuditDbContext"/>.
/// </summary>
public class AuditEntryRepository(AuditDbContext context) : Repository<AuditEntry>(context), IAuditEntryRepository
{
    private readonly AuditDbContext _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);

    public async Task<IReadOnlyList<AuditEntry>> SearchAsync(AuditSearchRequest request, CancellationToken ct = default)
    {
        // فیلتر حذف نرم به‌وسیله‌ی QueryFilter سراسری اعمال می‌شود.
        var query = _context.AuditEntries.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.EntityType))
            query = query.Where(e => e.EntityType == request.EntityType);

        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(e => e.Action == request.Action);

        if (request.UserId is { } userId)
            query = query.Where(e => e.UserId == userId);

        if (request.FromUtc is { } from)
            query = query.Where(e => e.CreatedAt >= from);

        if (request.ToUtc is { } to)
            query = query.Where(e => e.CreatedAt <= to);

        var entries = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((Math.Max(request.Page, 1) - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(ct);

        return entries;
    }
}
