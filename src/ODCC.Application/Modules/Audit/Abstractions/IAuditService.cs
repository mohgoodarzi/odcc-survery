using ODCC.Application.Modules.Audit.Dtos;

namespace ODCC.Application.Modules.Audit.Abstractions;

/// <summary>
/// سرویس ثبت و پرس‌وجوی رخدادهای ممیزی.
/// سایر ماژول‌ها از طریق این قرارداد (نه از طریق DbContext) رخدادهای حساس را ثبت می‌کنند.
/// </summary>
public interface IAuditService
{
    /// <summary>ثبت یک رخداد ممیزی جدید.</summary>
    Task LogAsync(AuditEntryDto entry, CancellationToken ct = default);

    /// <summary>جستجوی صفحه‌بندی‌شده‌ی رخدادها.</summary>
    Task<IReadOnlyList<AuditEntryDto>> SearchAsync(AuditSearchRequest request, CancellationToken ct = default);
}
