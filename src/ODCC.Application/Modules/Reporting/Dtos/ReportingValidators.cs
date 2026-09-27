using FluentValidation;
using ODCC.Application.Modules.Reporting.Dtos;
using ODCC.Domain.Modules.Reporting.Enums;

namespace ODCC.Application.Modules.Reporting.Dtos;

/// <summary>
/// اعتبارسنجی درخواست ایجاد/ویرایش تعریف گزارش.
/// </summary>
public sealed class SaveReportRequestValidator : AbstractValidator<SaveReportRequest>
{
    public SaveReportRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("نام گزارش الزامی است و حداکثر ۲۰۰ کاراکتر می‌تواند باشد.");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("توضیحات حداکثر می‌تواند ۱۰۰۰ کاراکتر باشد.");

        RuleFor(x => x.Type)
            .IsInEnum()
            .WithMessage("نوع گزارش نامعتبر است.");

        RuleFor(x => x.Format)
            .IsInEnum()
            .WithMessage("قالب خروجی نامعتبر است.");

        RuleFor(x => x.Schedule)
            .IsInEnum()
            .WithMessage("زمان‌بندی نامعتبر است.");

        // گزارش‌های مبتنی بر یک نظرسنجی بدون شناسایی آن نظرسنجی معنایی ندارند.
        RuleFor(x => x.SurveyId)
            .NotEmpty()
            .When(x => x.Type == ReportType.SurveyAnalytics || x.Type == ReportType.BenchmarkComparison)
            .WithMessage("برای گزارش تحلیلات یا مقایسه‌ی بنچمارک، انتخاب نظرسنجی الزامی است.");

        // خلاصه‌ی داشبورد کل شرکت است و به نظرسنجی خاصی وابسته نیست.
        RuleFor(x => x.SurveyId)
            .Null()
            .When(x => x.Type == ReportType.DashboardSummary)
            .WithMessage("گزارش خلاصه‌ی داشبورد به نظرسنجی خاصی محدود نمی‌شود.");

        RuleFor(x => x.From)
            .LessThanOrEqualTo(x => x.To)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("شروع بازه‌ی زمانی نمی‌تواند پس از پایان آن باشد.");

        RuleFor(x => x.OrgUnitId)
            .NotEmpty()
            .When(x => x.OrgUnitId.HasValue)
            .WithMessage("شناسه‌ی واحد سازمانی نامعتبر است.");

        RuleFor(x => x.RetentionCount)
            .InclusiveBetween(1, 100)
            .WithMessage("تعداد اجراهای نگه‌داشته‌شده باید بین ۱ تا ۱۰۰ باشد.");
    }
}

/// <summary>
/// اعتبارسنجی درخواست جستجوی تعاریف گزارش.
/// </summary>
public sealed class ReportSearchRequestValidator : AbstractValidator<ReportSearchRequest>
{
    public ReportSearchRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("شماره‌ی صفحه باید حداقل ۱ باشد.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 200)
            .WithMessage("اندازه‌ی صفحه باید بین ۱ تا ۲۰۰ باشد.");
    }
}

/// <summary>
/// اعتبارسنجی درخواست جستجوی اجراهای گزارش.
/// </summary>
public sealed class ExecutionSearchRequestValidator : AbstractValidator<ExecutionSearchRequest>
{
    public ExecutionSearchRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("شماره‌ی صفحه باید حداقل ۱ باشد.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 200)
            .WithMessage("اندازه‌ی صفحه باید بین ۱ تا ۲۰۰ باشد.");
    }
}
