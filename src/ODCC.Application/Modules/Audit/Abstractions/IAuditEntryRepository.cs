using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Domain.Modules.Audit.Entities;

namespace ODCC.Application.Modules.Audit.Abstractions;

/// <summary>
/// مخزن اختصاصی ماژول ممیزی.
/// هر ماژول مخزن اختصاصی خود را تعریف می‌کند تا قرارداد خواندن/نوشتن آن از سایر ماژول‌ها مستقل بماند.
/// </summary>
public interface IAuditEntryRepository : IRepository<AuditEntry>
{
    /// <summary>ذخیره‌ی تغییرات DbContext اختصاصی ماژول ممیزی.</summary>
    Task<int> SaveChangesAsync(CancellationToken ct = default);

    /// <summary>جستجوی صفحه‌بندی‌شده بدون بارگذاری کامل ردیف‌ها در حافظه.</summary>
    Task<IReadOnlyList<AuditEntry>> SearchAsync(AuditSearchRequest request, CancellationToken ct = default);
}
