using ODCC.Domain.Common;
using ODCC.Domain.Modules.Reporting.Enums;
using ODCC.Domain.Modules.Reporting.Events;

namespace ODCC.Domain.Modules.Reporting.Entities;

/// <summary>
/// تعریف یک گزارش قابل‌اجرا و (اختیاری) زمان‌بندی‌شده.
///
/// یک تعریف گزارش سه بخش دارد:
/// <list type="bullet">
///   <item><b>محتوا:</b> <see cref="Type"/> تعیین می‌کند کدام داده‌ها جمع‌آوری شوند.</item>
///   <item><b>دامنه:</b> بازه‌ی زمانی (<see cref="From"/>/<see cref="To"/>)، نظرسنجی
///   (<see cref="SurveyId"/>) و واحد سازمانی (<see cref="OrgUnitId"/>) که داده‌ها به
///   آن محدود می‌شوند. این مقادیر در زمان تعریف <b>تثبیت</b> می‌شوند تا اجرای
///   زمان‌بندی‌شده (بدون کاربر) دقیقاً همان دامنه‌ای را ببیند که سازنده دیده است.</item>
///   <item><b>زمان‌بندی:</b> <see cref="Schedule"/> و <see cref="NextRunAt"/>.</item>
/// </list>
///
/// <b>حریم خصوصی:</b> گزارش‌ها فقط از <b>تجمع‌های</b> تحلیلات تغذیه می‌شوند.
/// هیچ شناسه‌ی پاسخ‌گو (کاربر، کارمند یا نام) هرگز در خروجی گزارش قرار نمی‌گیرد —
/// حتی برای نظرسنجی‌های غیرناشناس. این یک تصمیم طراحی عمدی است.
/// </summary>
public class ReportDefinition : BaseEntity
{
    /// <summary>نام نمایشی گزارش، مثلاً «گزارش ماهانه NPS واحد فروش».</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>توضیحات اختیاری هدف گزارش.</summary>
    public string? Description { get; set; }

    /// <summary>نوع محتوای گزارش.</summary>
    public ReportType Type { get; set; } = ReportType.SurveyAnalytics;

    /// <summary>قالب خروجی گزارش.</summary>
    public ReportFormat Format { get; set; } = ReportFormat.Pdf;

    /// <summary>زمان‌بندی اجرا.</summary>
    public ReportSchedule Schedule { get; set; } = ReportSchedule.OneTime;

    /// <summary>وضعیت چرخه‌ی عمر تعریف.</summary>
    public ReportStatus Status { get; set; } = ReportStatus.Draft;

    // --- دامنه‌ی داده (تثبیت‌شده در زمان تعریف) --------------------------------

    /// <summary>شناسه‌ی نظرسنجی (برای <see cref="ReportType.SurveyAnalytics"/> و <see cref="ReportType.BenchmarkComparison"/>).</summary>
    public Guid? SurveyId { get; set; }

    /// <summary>کد نظرسنجی در زمان تعریف (عکس‌العمل، برای نمایش بدون پرس‌وجوی متقاطع).</summary>
    public string? SurveyCode { get; set; }

    /// <summary>عنوان نظرسنجی در زمان تعریف (عکس‌العمل).</summary>
    public string? SurveyTitle { get; set; }

    /// <summary>شروع بازه‌ی زمانی گزارش (شامل). <c>null</c> یعنی بدون محدودیت.</summary>
    public DateTime? From { get; set; }

    /// <summary>پایان بازه‌ی زمانی گزارش (شامل). <c>null</c> یعنی بدون محدودیت.</summary>
    public DateTime? To { get; set; }

    /// <summary>شناسه‌ی واحد سازمانی محدودکننده (اختیاری).</summary>
    public Guid? OrgUnitId { get; set; }

    /// <summary>مسیر مادی واحد سازمانی (برای نمایش و بررسی دسترسی).</summary>
    public string? OrgUnitPath { get; set; }

    /// <summary>آیا زیرمجموعه‌های واحد سازمانی هم شامل شوند؟</summary>
    public bool IncludeDescendants { get; set; } = true;

    // --- مالکیت و زمان‌بندی ----------------------------------------------------

    /// <summary>کاربری که تعریف را ساخته (در اجرای زمان‌بندی‌شده <c>null</c>).</summary>
    public Guid? OwnerUserId { get; set; }

    /// <summary>نام نمایشی سازنده (عکس‌العمل).</summary>
    public string? OwnerUserName { get; set; }

    /// <summary>زمان آخرین اجرای موفق.</summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>زمان اجرای بعدی (فقط برای زمان‌بندی‌های غیرِ یک‌باره).</summary>
    public DateTime? NextRunAt { get; set; }

    /// <summary>تعداد اجراهایی که خروجی آن‌ها نگه داشته می‌شود (بقیه نرم حذف می‌شوند).</summary>
    public int RetentionCount { get; set; } = 10;

    /// <summary>
    /// اعمال مقادیر یک تعریف گزارش. فقط در زمان ایجاد/ویرایش فراخوانی می‌شود.
    /// </summary>
    public void Update(
        string name,
        string? description,
        ReportType type,
        ReportFormat format,
        ReportSchedule schedule,
        Guid? surveyId,
        string? surveyCode,
        string? surveyTitle,
        DateTime? from,
        DateTime? to,
        Guid? orgUnitId,
        string? orgUnitPath,
        bool includeDescendants,
        int retentionCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Description = description;
        Type = type;
        Format = format;
        Schedule = schedule;
        SurveyId = surveyId;
        SurveyCode = surveyCode;
        SurveyTitle = surveyTitle;
        From = from;
        To = to;
        OrgUnitId = orgUnitId;
        OrgUnitPath = orgUnitPath;
        IncludeDescendants = includeDescendants;
        RetentionCount = Math.Clamp(retentionCount, 1, 100);

        // تغییر زمان‌بندی، اجرای بعدی را دوباره محاسبه می‌کند.
        NextRunAt = ComputeNextRun(Schedule, DateTime.UtcNow, Status == ReportStatus.Active);
    }

    /// <summary>فعال‌سازی تعریف: از این پس توسط زمان‌بند اجرا می‌شود.</summary>
    public void Activate()
    {
        if (Status == ReportStatus.Archived)
        {
            return;
        }

        Status = ReportStatus.Active;
        NextRunAt = ComputeNextRun(Schedule, DateTime.UtcNow, isActive: true);
    }

    /// <summary>بایگانی تعریف (حذف نرم): دیگر اجرا نمی‌شود ولی تاریخچه باقی می‌ماند.</summary>
    public void Archive()
    {
        Status = ReportStatus.Archived;
        NextRunAt = null;
    }

    /// <summary>
    /// ثبت یک اجرای جدید روی این تعریف و محاسبه‌ی زمان اجرای بعدی.
    /// </summary>
    /// <param name="executedAt">زمان شروع اجرا (UTC).</param>
    /// <param name="surveyCode">کد نظرسنجیِ به‌روز (برای تازه‌سازی عکس‌العمل).</param>
    /// <param name="surveyTitle">عنوان نظرسنجیِ به‌روز.</param>
    public void RecordExecution(DateTime executedAt, string? surveyCode = null, string? surveyTitle = null)
    {
        LastExecutedAt = executedAt;

        // عکس‌العمل عنوان/کد نظرسنجی در هر اجرا تازه می‌شود تا نام‌های تغییرکرده
        // در گزارش‌ها منعکس شوند (همان الگوی مدل خواندنی تحلیلات).
        if (surveyCode is not null)
            SurveyCode = surveyCode;

        if (surveyTitle is not null)
            SurveyTitle = surveyTitle;

        NextRunAt = ComputeNextRun(Schedule, executedAt, Status == ReportStatus.Active);
    }

    /// <summary>
    /// محاسبه‌ی زمان اجرای بعدی بر اساس نوع زمان‌بندی.
    /// زمان‌بندی یک‌باره هیچ اجرای بعدی ندارد (<c>null</c>).
    /// </summary>
    internal static DateTime? ComputeNextRun(ReportSchedule schedule, DateTime from, bool isActive)
    {
        if (!isActive || schedule == ReportSchedule.OneTime)
        {
            return null;
        }

        return schedule switch
        {
            ReportSchedule.Daily => from.AddDays(1),
            ReportSchedule.Weekly => from.AddDays(7),
            ReportSchedule.Monthly => from.AddMonths(1),
            _ => null
        };
    }

    /// <summary>رویداد «ایجاد تعریف گزارش».</summary>
    public void RaiseCreatedEvent() =>
        RaiseDomainEvent(new ReportCreatedEvent(Id, Name, Type, Format, Schedule, OwnerUserId));

    /// <summary>رویداد «ویرایش تعریف گزارش».</summary>
    public void RaiseUpdatedEvent() =>
        RaiseDomainEvent(new ReportUpdatedEvent(Id, Name, Type, Format, Schedule, OwnerUserId));

    /// <summary>رویداد «فعال‌سازی تعریف گزارش».</summary>
    public void RaiseActivatedEvent() =>
        RaiseDomainEvent(new ReportActivatedEvent(Id, Name, Schedule, NextRunAt, OwnerUserId));

    /// <summary>رویداد «بایگانی تعریف گزارش».</summary>
    public void RaiseArchivedEvent() =>
        RaiseDomainEvent(new ReportArchivedEvent(Id, Name, OwnerUserId));
}
