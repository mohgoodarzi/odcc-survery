using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Domain.Common;
using ODCC.Application.Modules.Organization.Abstractions;

namespace ODCC.Infrastructure.Services;

/// <summary>
/// پیاده‌سازی دامنه‌ی سازمانی کاربر جاری.
///
/// مسیر مادی واحد لنگر کاربر را از مخزن ماژول سازمان می‌خواند و یک
/// <see cref="OrgScope"/> می‌سازد که سرویس‌ها می‌توانند برای محدود کردن پرس‌وجوها
/// استفاده کنند. این کلاس تضمین می‌کند کاربر با دانستن یک شناسه نتواند
/// داده‌های خارج از دامنه‌ی خود را ببیند.
///
/// توجه: این کلاس از قرارداد <see cref="IOrgUnitRepository"/> استفاده می‌کند،
/// نه از DbContext مستقیم، تا مرز ماژول‌ها حفظ شود.
/// </summary>
public sealed class OrgScopeProvider(
    ICurrentUserService currentUserService,
    IOrgUnitRepository orgUnitRepository) : IOrgScopeProvider
{
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IOrgUnitRepository _orgUnitRepository = orgUnitRepository;

    /// <summary>
    /// دامنه‌ی کاربر جاری را محاسبه می‌کند. این متد fail-closed است:
    /// در صورت نبودن اطلاعات کافی، دامنه‌ای برمی‌گرداند که هیچ داده‌ی سازمانی
    /// نمی‌بیند، تا هرگز دسترسی بیش از حد داده نشود.
    /// </summary>
    public async Task<OrgScope> GetCurrentScopeAsync(CancellationToken ct = default)
    {
        // کاربر احراز هویت‌نشده: هیچ دامنه‌ی سازمانی.
        if (!_currentUserService.IsAuthenticated)
        {
            return OrgScope.Empty;
        }

        var anchorId = _currentUserService.OrgUnitId;
        var scope = _currentUserService.DataScope;

        // دسترسی سراسری: نیازی به مسیر لنگر نیست، اما شناسه لنگر را برای
        // سازگاری باقی می‌گذاریم.
        if (scope == DataScope.Company)
        {
            return new OrgScope
            {
                AnchorOrgUnitId = anchorId,
                Scope = DataScope.Company
            };
        }

        // دامنه‌ی Own: فقط داده‌های شخصی کاربر (از مسیرهای دیگر قابل دسترسی).
        if (scope == DataScope.Own || anchorId is null)
        {
            return OrgScope.Empty;
        }

        // مسیر مادی لنگر را فقط در صورت نیاز می‌خوانیم (درخواست‌های Company/Own آن را دور می‌زنند).
        var anchorPath = await _orgUnitRepository.GetPathAsync(anchorId.Value, ct);

        // لنگر معتبر نیست (مثلاً واحد حذف شده): fail-closed.
        if (string.IsNullOrWhiteSpace(anchorPath))
        {
            return OrgScope.Empty;
        }

        return new OrgScope
        {
            AnchorOrgUnitId = anchorId,
            AnchorPath = anchorPath,
            Scope = scope
        };
    }
}
