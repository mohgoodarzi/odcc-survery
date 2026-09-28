using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.ActionManagement.Dtos;
using ODCC.Domain.Modules.ActionManagement.Entities;

namespace ODCC.Application.Modules.ActionManagement.Abstractions;

/// <summary>
/// مخزن اختصاصی برنامه‌های اقدام.
/// </summary>
public interface IActionPlanRepository : IRepository<ActionPlan>
{
    /// <summary>
    /// جستجوی صفحه‌بندی‌شده‌ی برنامه‌ها. دامنه‌ی سازمانی کاربر (محاسبه‌شده در
    /// سرویس، نه از کلاینت) به‌عنوان مرز امنیتی اعمال می‌شود.
    /// </summary>
    Task<IReadOnlyList<ActionPlan>> SearchAsync(ActionPlanSearchRequest request, OrgScope scope, CancellationToken ct = default);

    /// <summary>تعداد برنامه‌های مطابق با فیلتر و دامنه.</summary>
    Task<int> CountAsync(ActionPlanSearchRequest request, OrgScope scope, CancellationToken ct = default);

    /// <summary>یافتن یک برنامه با کلید منبع خودکار (برای idempotency ایجاد خودکار).</summary>
    Task<ActionPlan?> FindBySourceKeyAsync(string sourceKey, CancellationToken ct = default);

    /// <summary>برنامه‌های باز (پیش‌نویس/فعال) متصل به یک نظرسنجی مشخص.</summary>
    Task<IReadOnlyList<ActionPlan>> ListOpenBySurveyAsync(Guid surveyId, CancellationToken ct = default);
}

/// <summary>
/// مخزن اختصاصی آیتم‌های اقدام.
/// </summary>
public interface IActionItemRepository : IRepository<ActionItem>
{
    /// <summary>
    /// جستجوی صفحه‌بندی‌شده‌ی آیتم‌ها. مرز سازمانی اعمال می‌شود، با استثناء‌ی
    /// مستند: کاربر آیتم‌های منتسب به خودش را می‌بیند (شناسه‌ی کاربر جاری).
    /// </summary>
    Task<IReadOnlyList<ActionItem>> SearchAsync(ActionItemSearchRequest request, OrgScope scope, Guid currentUserId, CancellationToken ct = default);

    /// <summary>تعداد آیتم‌های مطابق با فیلتر و دامنه.</summary>
    Task<int> CountAsync(ActionItemSearchRequest request, OrgScope scope, Guid currentUserId, CancellationToken ct = default);

    /// <summary>آیتم‌های یک برنامه (مرتب بر اساس ترتیب نمایش).</summary>
    Task<IReadOnlyList<ActionItem>> ListByPlanAsync(Guid planId, CancellationToken ct = default);

    /// <summary>
    /// آیتم‌های بازی که زمان یادآوری‌شان رسیده است. زمان‌بند پیگیری این
    /// متد را صدا می‌زند.
    /// </summary>
    Task<IReadOnlyList<ActionItem>> ListDueRemindersAsync(DateTime asOf, int maxResults, CancellationToken ct = default);

    /// <summary>
    /// آیتم‌های سررسیده‌شده‌ی باز که نیاز به شدیدسازی دارند. زمان‌بند پیگیری
    /// این متد را صدا می‌زند.
    /// </summary>
    Task<IReadOnlyList<ActionItem>> ListOverdueAsync(DateTime asOf, int maxResults, CancellationToken ct = default);

    /// <summary>دیدگاه‌های یک آیتم (مرتب از قدیم به جدید).</summary>
    Task<IReadOnlyList<ActionComment>> ListCommentsAsync(Guid itemId, CancellationToken ct = default);

    /// <summary>افزودن یک دیدگاه به آیتم.</summary>
    Task AddCommentAsync(ActionComment comment, CancellationToken ct = default);

    /// <summary>پیوست‌های یک آیتم.</summary>
    Task<IReadOnlyList<ActionEvidence>> ListEvidenceAsync(Guid itemId, CancellationToken ct = default);

    /// <summary>افزودن یک پیوست به آیتم.</summary>
    Task AddEvidenceAsync(ActionEvidence evidence, CancellationToken ct = default);

    /// <summary>یافتن یک پیوست با شناسه.</summary>
    Task<ActionEvidence?> GetEvidenceByIdAsync(Guid evidenceId, CancellationToken ct = default);

    /// <summary>حذف نرم یک پیوست.</summary>
    void RemoveEvidence(ActionEvidence evidence);
}

/// <summary>
/// انبار پیوست‌های اقدامات: ذخیره‌سازی محتوای فایل بیرون از پایگاه داده.
/// پیاده‌سازی پیش‌فرض یک شاخه‌ی ریشه‌ی پیکربندی‌شده است و از path traversal
/// جلوگیری می‌کند (الگوی مشابه <c>IReportArtifactStore</c>).
/// </summary>
public interface IActionEvidenceStore
{
    /// <summary>ذخیره‌ی محتوای فایل و بازگرداندن مسیر نسبی امن.</summary>
    Task<StoredEvidence> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>باز کردن فایل برای خواندن.</summary>
    Task<Stream> OpenReadAsync(string relativePath, CancellationToken ct = default);

    /// <summary>حذف فایل از دیسک.</summary>
    Task DeleteAsync(string relativePath, CancellationToken ct = default);

    /// <summary>آیا فایل وجود دارد؟</summary>
    bool Exists(string relativePath);
}

/// <summary>پیوست ذخیره‌شده به‌همراه مسیر نسبی امن.</summary>
public sealed record StoredEvidence(string RelativePath, long LengthBytes);
