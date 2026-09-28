using ODCC.Application.Abstractions;
using ODCC.Application.Modules.ActionManagement.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.ActionManagement.Abstractions;

/// <summary>
/// سرویس مدیریت اقدامات و پیگیری: ساخت برنامه‌ها و آیتم‌ها از یافته‌های
/// نظرسنجی/تحلیلات، انتصاب مسئول، اولویت‌بندی، مهلت‌ها، چرخه‌ی عمر، دیدگاه‌ها،
/// پیوست‌ها، یادآور/تشدید خودکار و سنجش اثربخشی.
///
/// <b>مرز سازمانی:</b> کاربر فقط برنامه‌ها و آیتم‌های داخل دامنه‌ی سازمانی
/// خودش را می‌بیند (fail-closed). استثناء مستند: کاربر همیشه آیتم‌های
/// منتسب به خودش را می‌بیند تا بتواند کارهای واگذاری‌شده‌اش را انجام دهد.
/// این مرز در این سرویس اعمال می‌شود (نه در کنترلر) تا هیچ مسیری دور نزند.
///
/// <b>حریم خصوصی:</b> این سرویس هرگز شناسه‌ی پاسخ‌گوی یک نظرسنجی را ذخیره
/// یا فاش نمی‌کند. برنامه‌های خودکار فقط شاخص‌های تجمعی (NPS/CSAT/CES) را
/// به‌عنوان زمینه نگه می‌دارند.
/// </summary>
public interface IActionManagementService
{
    // --- برنامه‌ها -------------------------------------------------------------

    Task<PagedResult<ActionPlanDto>> SearchPlansAsync(ActionPlanSearchRequest request, CancellationToken ct = default);

    Task<Result<ActionPlanDto>> GetPlanByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<ActionPlanDto>> CreatePlanAsync(SaveActionPlanRequest request, CancellationToken ct = default);

    Task<Result<ActionPlanDto>> UpdatePlanAsync(Guid id, SaveActionPlanRequest request, CancellationToken ct = default);

    Task<Result<ActionPlanDto>> ActivatePlanAsync(Guid id, CancellationToken ct = default);

    Task<Result<ActionPlanDto>> CompletePlanAsync(Guid id, CancellationToken ct = default);

    Task<Result<ActionPlanDto>> CancelPlanAsync(Guid id, CancellationToken ct = default);

    Task<Result> ArchivePlanAsync(Guid id, CancellationToken ct = default);

    /// <summary>ثبت دستی مقدار شاخصِ خروجی برای سنجش اثربخشی یک برنامه.</summary>
    Task<Result<ActionPlanDto>> RecordPlanOutcomeAsync(Guid id, RecordOutcomeRequest request, CancellationToken ct = default);

    // --- آیتم‌ها ---------------------------------------------------------------

    Task<PagedResult<ActionItemDto>> SearchItemsAsync(ActionItemSearchRequest request, CancellationToken ct = default);

    Task<Result<ActionItemDto>> GetItemByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<ActionItemDto>> CreateItemAsync(Guid planId, SaveActionItemRequest request, CancellationToken ct = default);

    Task<Result<ActionItemDto>> UpdateItemAsync(Guid id, SaveActionItemRequest request, CancellationToken ct = default);

    /// <summary>تغییر وضعیت یک آیتم (شروع/تکمیل/ازسرگیری/لغو).</summary>
    Task<Result<ActionItemDto>> TransitionItemAsync(Guid id, TransitionActionItemRequest request, CancellationToken ct = default);

    /// <summary>ثبت ارزیابی اثربخشی یک آیتم (فقط پس از تکمیل/لغو).</summary>
    Task<Result<ActionItemDto>> AssessItemEffectivenessAsync(
        Guid id, AssessEffectivenessRequest request, CancellationToken ct = default);

    // --- دیدگاه‌ها --------------------------------------------------------------

    Task<Result<IReadOnlyList<ActionCommentDto>>> ListCommentsAsync(Guid itemId, CancellationToken ct = default);

    Task<Result<ActionCommentDto>> AddCommentAsync(Guid itemId, AddActionCommentRequest request, CancellationToken ct = default);

    // --- پیوست‌ها ---------------------------------------------------------------

    Task<Result<IReadOnlyList<ActionEvidenceDto>>> ListEvidenceAsync(Guid itemId, CancellationToken ct = default);

    /// <summary>ذخیره‌ی یک فایل پیوست. نوع/اندازه‌ی فایل در اینجا اعتبارسنجی می‌شود.</summary>
    Task<Result<ActionEvidenceDto>> UploadEvidenceAsync(
        Guid itemId, UploadActionEvidenceRequest request, CancellationToken ct = default);

    /// <summary>باز کردن محتوای یک پیوست برای دانلود (فقط کاربران مجاز).</summary>
    Task<Result<ActionEvidenceContent>> DownloadEvidenceAsync(Guid evidenceId, CancellationToken ct = default);

    Task<Result> DeleteEvidenceAsync(Guid evidenceId, CancellationToken ct = default);

    // --- پیگیری خودکار (زمان‌بند) -----------------------------------------------

    /// <summary>
    /// پردازش یادآورهای رسیده‌ی آیتم‌های باز. یک اثر جانبی است و توسط
    /// زمان‌بند پس‌زمینه (با تأیید صریح) صدا زده می‌شود.
    /// </summary>
    /// <returns>تعداد یادآورهای پردازش‌شده.</returns>
    Task<int> ProcessDueRemindersAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// تشدید آیتم‌های سررسیده‌شده‌ی باز یک درجه. یک اثر جانبی است و توسط
    /// زمان‌بند پس‌زمینه (با تأیید صریح) صدا زده می‌شود.
    /// </summary>
    /// <returns>تعداد آیتم‌های تشدیدشده.</returns>
    Task<int> ProcessOverdueEscalationsAsync(DateTime asOf, CancellationToken ct = default);

    /// <summary>
    /// ساخت خودکار یک برنامه از هشدار تحلیلات (مثلاً افت NPS زیر بنچمارک).
    /// این متد <b>خودتوان</b> است: اگر برنامه‌ای با همان کلید منبع وجود داشته
    /// باشد، چیزی ایجاد نمی‌کند. فقط شاخص‌های تجمعی دریافت می‌کند — هرگز
    /// شناسه‌ی پاسخ‌گو.
    /// </summary>
    Task<Result<ActionPlanDto?>> EnsurePlanFromAnalyticsAlertAsync(
        AnalyticsAlertRequest request, CancellationToken ct = default);

    /// <summary>خلاصه‌ی آماری اقدامات برای داشبورد (با احترام به دامنه‌ی سازمانی).</summary>
    Task<Result<ActionStatsDto>> GetStatsAsync(CancellationToken ct = default);
}
