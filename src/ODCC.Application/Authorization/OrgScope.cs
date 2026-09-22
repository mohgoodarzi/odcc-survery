using ODCC.Domain.Common;

namespace ODCC.Application.Authorization;

/// <summary>
/// دامنه‌ی سازمانیِ قابل‌مشاهده توسط کاربر جاری.
///
/// به‌جای بازگرداندن لیستی از شناسه‌ها (که در سازمان‌های بزرگ بسیار بزرگ می‌شود)،
/// این کلاس یک «توصیف فیلتر» برمی‌گرداند که مخازن می‌توانند آن را روی پرس‌وجوها اعمال کنند.
/// چون مسیرها به‌صورت مادی (<c>OrgUnit.Path</c>) ذخیره می‌شوند، محدودسازی زیردرخت
/// با یک عملگر LIKE کارآمد انجام می‌شود.
///
/// <b>قاعده‌ی امنیتی:</b> این شیء همواره fail-closed است. اگر نتوان دامنه را به‌طور قطعی
/// محاسبه کرد (کاربر ناشناس، دامنه‌ی Own، یا نبودن لنگر سازمانی معتبر)، هیچ داده‌ی سازمانی
/// نباید قابل‌مشاهده باشد. هیچ مسیری در این کلاس به «دسترسی نامحدود» ختم نمی‌شود،
/// مگر اینکه دامنه صراحتاً <see cref="DataScope.Company"/> باشد.
/// </summary>
public sealed record OrgScope
{
    /// <summary>شناسه‌ی واحد سازمانی لنگر (واحد کاربر).</summary>
    public Guid? AnchorOrgUnitId { get; init; }

    /// <summary>مسیر مادی واحد لنگر، مثلاً «/hq/fin».</summary>
    public string? AnchorPath { get; init; }

    /// <summary>دامنه‌ی دسترسی.</summary>
    public DataScope Scope { get; init; }

    /// <summary>
    /// آیا کاربر هیچ محدودیت سازمانی ندارد؟
    /// فقط <see cref="DataScope.Company"/> واقعاً نامحدود است. هیچ‌چیز دیگری (حتی
    /// نبودن لنگر) به‌عنوان نامحدود تفسیر نمی‌شود.
    /// </summary>
    public bool IsUnrestricted => Scope == DataScope.Company;

    /// <summary>
    /// آیا این دامنه اساساً زیردرخت سازمانی قابل‌مشاهده‌ای دارد؟
    /// دامنه‌ی <see cref="DataScope.Own"/> و دامنه‌های بدون لنگر معتبر، هیچ
    /// داده‌ی سازمانی نمی‌بینند. (داده‌ی شخصی کاربر از مسیر دیگری مثل
    /// <c>GetByUserIdAsync</c> در دسترس است.)
    /// </summary>
    public bool HasVisibleOrgScope =>
        !IsUnrestricted
        && Scope != DataScope.Own
        && AnchorOrgUnitId is not null
        && !string.IsNullOrWhiteSpace(AnchorPath);

    /// <summary>
    /// پیشوند مسیر برای فیلتر کردن زیردرخت قابل‌مشاهده.
    /// برای محدود کردن یک پرس‌وجو: <c>Where(u => u.Path.StartsWith(prefix))</c>.
    /// <c>null</c> یعنی «نامشخص» — <b>نه</b> «نامحدود». برای تشخیص دسترسی نامحدود
    /// از <see cref="IsUnrestricted"/> استفاده کنید.
    /// </summary>
    public string? VisiblePathPrefix => HasVisibleOrgScope ? AnchorPath : null;

    /// <summary>دامنه‌ی بدون دسترسی به داده‌های سازمانی.</summary>
    public static OrgScope Empty => new() { Scope = DataScope.Own };

    /// <summary>
    /// آیا کاربر اجازه‌ی دیدن داده‌های متعلق به یک مسیر سازمانی را دارد؟
    /// <b>fail-closed:</b> در صورت نبودن اطلاعات کافی، <c>false</c> برمی‌گرداند.
    /// </summary>
    public bool CanAccess(string? resourceOrgPath)
    {
        if (IsUnrestricted)
        {
            return true;
        }

        if (!HasVisibleOrgScope)
        {
            // دامنه‌ی Own یا لنگر نامعتبر: هیچ داده‌ی سازمانی قابل‌مشاهده نیست.
            return false;
        }

        if (string.IsNullOrWhiteSpace(resourceOrgPath))
        {
            // منبعی که مسیر سازمانی ندارد (مثلاً هنوز به واحدی متصل نشده).
            return false;
        }

        return IsSelfOrDescendant(resourceOrgPath);
    }

    /// <summary>
    /// آیا کاربر اجازه‌ی دیدن داده‌های یک واحد سازمانی مشخص را دارد؟
    /// </summary>
    /// <param name="resourceOrgUnitId">شناسه‌ی واحد منبع.</param>
    /// <param name="resourceOrgPath">مسیر مادی واحد منبع (در صورت موجود بودن، برای بررسی دقیق‌تر).</param>
    public bool CanAccess(Guid? resourceOrgUnitId, string? resourceOrgPath)
    {
        if (IsUnrestricted)
        {
            return true;
        }

        if (!HasVisibleOrgScope)
        {
            return false;
        }

        if (resourceOrgUnitId is null)
        {
            return false;
        }

        // اگر مسیر مادی موجود باشد، دقیق‌ترین بررسی ممکن را انجام می‌دهیم.
        if (!string.IsNullOrWhiteSpace(resourceOrgPath))
        {
            return IsSelfOrDescendant(resourceOrgPath);
        }

        // بدون مسیر: فقط خود واحد لنگر را می‌پذیریم.
        return resourceOrgUnitId == AnchorOrgUnitId;
    }

    /// <summary>
    /// آیا مسیر داده‌شده خود لنگر است یا زیردرخت آن؟
    /// مرزها با «/» بررسی می‌شوند تا «/hq/fin» با «/hq/finance» اشتباه گرفته نشود.
    /// </summary>
    private bool IsSelfOrDescendant(string resourceOrgPath)
    {
        var anchor = AnchorPath!;

        return string.Equals(resourceOrgPath, anchor, StringComparison.OrdinalIgnoreCase)
               || resourceOrgPath.StartsWith(anchor + "/", StringComparison.OrdinalIgnoreCase);
    }
}
