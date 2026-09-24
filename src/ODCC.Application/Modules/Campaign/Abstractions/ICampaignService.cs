using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Campaign.Dtos;
using ODCC.Domain.Common;

namespace ODCC.Application.Modules.Campaign.Abstractions;

/// <summary>
/// سرویس مدیریت کمپین‌ها: زمان‌بندی، هدف‌گیری مخاطب، توزیع، یادآوری‌ها و پیگیری.
/// </summary>
public interface ICampaignService
{
    Task<PagedResult<CampaignSummaryDto>> SearchAsync(CampaignSearchRequest request, CancellationToken ct = default);

    Task<Result<CampaignDto>> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<Result<CampaignDto>> CreateAsync(SaveCampaignRequest request, CancellationToken ct = default);

    /// <summary>به‌روزرسانی تنظیمات کمپین (فقط در حالت پیش‌نویس).</summary>
    Task<Result<CampaignDto>> UpdateAsync(Guid id, SaveCampaignRequest request, CancellationToken ct = default);

    /// <summary>زمان‌بندی کمپین برای اجرای خودکار در زمان مقرر (Draft → Scheduled).</summary>
    Task<Result<CampaignDto>> ScheduleAsync(Guid id, DateTime scheduledAt, CancellationToken ct = default);

    /// <summary>
    /// اجرای کمپین: حل جمعیت هدف، ساخت ردیف‌های توزیع و گذار به «در حال اجرا».
    /// </summary>
    Task<Result<CampaignLaunchResultDto>> LaunchAsync(Guid id, CancellationToken ct = default);

    /// <summary>تکمیل کمپین (Running → Completed).</summary>
    Task<Result<CampaignDto>> CompleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>بایگانی کمپین.</summary>
    Task<Result<CampaignDto>> ArchiveAsync(Guid id, CancellationToken ct = default);

    /// <summary>حذف نرم کمپین (فقط در حالت پیش‌نویس).</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken ct = default);

    // --- توزیع و یادآور -------------------------------------------------------

    /// <summary>فهرست صفحه‌بندی‌شده‌ی ردیف‌های توزیع یک کمپین.</summary>
    Task<PagedResult<DistributionDto>> GetDistributionsAsync(Guid campaignId, DistributionSearchRequest request, CancellationToken ct = default);

    /// <summary>
    /// ثبت نتیجه‌ی ارسال یک ردیف توزیع (موفق یا ناموفق). این متد توسط ماژول
    /// اعلان‌ها (Notification) پس از تلاش ارسال فراخوانی می‌شود.
    /// </summary>
    Task<Result<DistributionDto>> RecordDistributionResultAsync(Guid distributionId, bool success, string? failureReason, CancellationToken ct = default);

    /// <summary>
    /// پردازش یادآورهای سررسیده: همه‌ی یادآورهای رسیده‌ی کمپین‌های «در حال اجرا»
    /// به‌عنوان ارسال‌شده علامت می‌خورند و شمارنده‌ی یادآورِ گیرندگانِ واجد شرایط
    /// افزایش می‌یابد. ارسال واقعی در ماژول اعلان‌ها انجام می‌شود.
    /// </summary>
    Task<Result<ReminderProcessResultDto>> ProcessDueRemindersAsync(CancellationToken ct = default);

    /// <summary>لغو یک یادآور برنامه‌ریزی‌شده.</summary>
    Task<Result> CancelReminderAsync(Guid campaignId, Guid reminderId, CancellationToken ct = default);
}
