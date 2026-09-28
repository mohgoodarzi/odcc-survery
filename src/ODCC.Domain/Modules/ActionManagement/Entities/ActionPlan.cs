using ODCC.Domain.Common;
using ODCC.Domain.Modules.ActionManagement.Enums;
using ODCC.Domain.Modules.ActionManagement.Events;

namespace ODCC.Domain.Modules.ActionManagement.Entities;

/// <summary>
/// برنامه‌ی اقدام: یک aggregate ریشه که چندین <see cref="ActionItem"/> را گروه می‌کند.
///
/// یک برنامه معمولاً از یافته‌های یک نظرسنجی یا هشدار تحلیلات ساخته می‌شود
/// (مثلاً «NPS واحد مالی از هدف پایین است») و مسئولیت پیگیری تا تکمیل را
/// به یک مالک واگذار می‌کند. هر برنامه به یک واحد سازمانی تعلق دارد تا
/// مرز دسترسی سازمانی (cross-organization) حفظ شود.
///
/// <b>حریم خصوصی:</b> این موجودیت هرگز شناسه‌ی پاسخ‌گوی یک نظرسنجی را ذخیره
/// نمی‌کند — حتی برای نظرسنجی‌های غیرناشناس. فقط شاخص‌های تجمعی (NPS/CSAT/CES)
/// و شناسه‌های نظرسنجی/کمپین را به‌عنوان زمینه نگه می‌دارد.
/// </summary>
public sealed class ActionPlan : BaseEntity
{
    /// <summary>عنوان برنامه.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>توضیح زمینه و دلیل ایجاد.</summary>
    public string? Description { get; set; }

    /// <summary>منشأ ایجاد (دستی / هشدار تحلیلات / یافته‌ی نظرسنجی).</summary>
    public ActionSource Source { get; set; } = ActionSource.Manual;

    /// <summary>
    /// کلید یکتای منبع خودکار (مثلاً «analytics:{surveyId}:survey:Nps»).
    /// فقط برای برنامه‌های خودکار مقدار دارد و تضمین می‌کند محاسبه‌ی مجدد
    /// تحلیلات، برنامه‌ی مضاعف نسازد. برای برنامه‌های دستی <c>null</c> است.
    /// </summary>
    public string? SourceKey { get; set; }

    /// <summary>شناسه‌ی نظرسنجیِ مرتبط (در صورت وجود).</summary>
    public Guid? SurveyId { get; set; }

    /// <summary>کد نظرسنجی در زمان ایجاد (snapshot برای تاریخچه).</summary>
    public string? SurveyCode { get; set; }

    /// <summary>عنوان نظرسنجی در زمان ایجاد (snapshot).</summary>
    public string? SurveyTitle { get; set; }

    /// <summary>شناسه‌ی کمپینِ مرتبط (در صورت وجود).</summary>
    public Guid? CampaignId { get; set; }

    /// <summary>
    /// نوع شاخصی که این برنامه به آن واکنش نشان می‌دهد (فقط برای منشأ تحلیلی).
    /// </summary>
    public ActionMetricType? TriggerMetricType { get; set; }

    /// <summary>
    /// مقدار شاخص در زمان ایجاد برنامه (baseline). برای سنجش اثربخشی پس از
    /// اجرا با <see cref="OutcomeMetricValue"/> مقایسه می‌شود.
    /// </summary>
    public decimal? TriggerMetricValue { get; set; }

    /// <summary>
    /// مقدار شاخص پس از اجرای برنامه (توسط شنونده‌ی تحلیلات یا به‌صورت دستی
    /// پر می‌شود). این هسته‌ی «سنجش اثربخشی» است.
    /// </summary>
    public decimal? OutcomeMetricValue { get; set; }

    /// <summary>زمان ثبت مقدار خروجی (UTC).</summary>
    public DateTime? OutcomeMeasuredAt { get; set; }

    // --- دامنه‌ی سازمانی -------------------------------------------------------

    /// <summary>شناسه‌ی واحد سازمانی که این برنامه به آن تعلق دارد (<c>null</c> = سراسری).</summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>
    /// مسیر مادی واحد سازمانی (snapshot). برای فیلتر کردن دامنه‌ی کاربر با
    /// یک عملگر LIKE کارآمد استفاده می‌شود.
    /// </summary>
    public string? OrgUnitPath { get; set; }

    // --- مالکیت و زمان‌بندی -----------------------------------------------------

    /// <summary>کاربر مالک برنامه (پاسخگوی نهایی پیگیری).</summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>نام نمایشی مالک در زمان ایجاد (snapshot).</summary>
    public string? OwnerUserName { get; set; }

    /// <summary>اولویت برنامه.</summary>
    public ActionPriority Priority { get; set; } = ActionPriority.Medium;

    /// <summary>وضعیت چرخه‌ی عمر.</summary>
    public ActionPlanStatus Status { get; set; } = ActionPlanStatus.Draft;

    /// <summary>مهلت نهایی برنامه (UTC).</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>کاربری که برنامه را ایجاد کرده.</summary>
    public Guid? CreatedByUserId { get; set; }

    /// <summary>نام نمایشی ایجادکننده (snapshot).</summary>
    public string? CreatedByUserName { get; set; }

    /// <summary>زمان تکمیل برنامه (UTC).</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>آیتم‌های این برنامه.</summary>
    public List<ActionItem> Items { get; set; } = [];

    // --- محاسبات ---------------------------------------------------------------

    /// <summary>تعداد آیتم‌های انجام‌شده (حذفی و لغوشده حساب نمی‌شوند).</summary>
    public int CompletedItemCount =>
        Items.Count(i => i.Status == ActionItemStatus.Done);

    /// <summary>درصد پیشرفت (۰ تا ۱۰۰). اگر آیتمی نباشد، ۰ است.</summary>
    public decimal ProgressPercentage =>
        Items.Count == 0 ? 0 : Math.Round((decimal)CompletedItemCount / Items.Count * 100, 1);

    /// <summary>آیا برنامه هنوز باز است (فعال و در حال پیگیری)؟</summary>
    public bool IsOpen => Status is ActionPlanStatus.Draft or ActionPlanStatus.Active;

    // --- چرخه‌ی عمر -------------------------------------------------------------

    /// <summary>فعال‌سازی برنامه: شروع پیگیری واقعی آیتم‌ها.</summary>
    public void Activate()
    {
        Status = ActionPlanStatus.Active;
    }

    /// <summary>تکمیل برنامه (فقط در حالت فعال).</summary>
    public void Complete()
    {
        Status = ActionPlanStatus.Completed;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>لغو برنامه (فقط در حالت پیش‌نویس یا فعال).</summary>
    public void Cancel()
    {
        Status = ActionPlanStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
    }

    /// <summary>بایگانی نرم برنامه.</summary>
    public void Archive()
    {
        Status = ActionPlanStatus.Archived;
    }

    /// <summary>ثبت مقدار شاخص پس از اجرا (برای سنجش اثربخشی).</summary>
    public void RecordOutcome(decimal? value)
    {
        OutcomeMetricValue = value;
        OutcomeMeasuredAt = DateTime.UtcNow;
    }

    // --- رویدادهای دامنه -------------------------------------------------------

    /// <summary>انتشار رویداد ایجاد برنامه (شنونده‌ی ممیزی ثبت می‌کند).</summary>
    public void RaiseCreatedEvent()
    {
        RaiseDomainEvent(new ActionPlanCreatedEvent(
            Id, Title, Source, SurveyId, OrgUnitId, OrgUnitPath,
            OwnerUserId, OwnerUserName, Priority));
    }

    /// <summary>انتشار رویداد ویرایش برنامه.</summary>
    public void RaiseUpdatedEvent()
    {
        RaiseDomainEvent(new ActionPlanUpdatedEvent(Id, Title, OrgUnitId, OrgUnitPath, Priority));
    }

    /// <summary>انتشار رویداد فعال‌سازی.</summary>
    public void RaiseActivatedEvent()
    {
        RaiseDomainEvent(new ActionPlanActivatedEvent(Id, Title, OwnerUserId));
    }

    /// <summary>انتشار رویداد تکمیل (شامل درصد پیشرفت برای سنجش اثربخشی).</summary>
    public void RaiseCompletedEvent()
    {
        RaiseDomainEvent(new ActionPlanCompletedEvent(Id, Title, CompletedItemCount, Items.Count, OwnerUserId));
    }

    /// <summary>انتشار رویداد لغو.</summary>
    public void RaiseCancelledEvent()
    {
        RaiseDomainEvent(new ActionPlanCancelledEvent(Id, Title, OwnerUserId));
    }

    /// <summary>انتشار رویداد بایگانی.</summary>
    public void RaiseArchivedEvent()
    {
        RaiseDomainEvent(new ActionPlanArchivedEvent(Id, Title));
    }
}
