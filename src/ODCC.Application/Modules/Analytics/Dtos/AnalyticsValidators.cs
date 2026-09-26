using FluentValidation;
using ODCC.Application.Modules.Analytics.Dtos;

namespace ODCC.Application.Modules.Analytics.Dtos;

/// <summary>
/// اعتبارسنجی درخواست محاسبه‌ی تحلیلات.
/// </summary>
public sealed class ComputeAnalyticsRequestValidator : AbstractValidator<ComputeAnalyticsRequest>
{
    public ComputeAnalyticsRequestValidator()
    {
        RuleFor(x => x.SurveyId)
            .NotEmpty()
            .WithMessage("شناسه‌ی نظرسنجی الزامی است.");

        RuleFor(x => x.Filter.From)
            .LessThanOrEqualTo(x => x.Filter.To)
            .When(x => x.Filter.From.HasValue && x.Filter.To.HasValue)
            .WithMessage("شروع بازه‌ی زمانی نمی‌تواند پس از پایان آن باشد.");

        RuleFor(x => x.SegmentType)
            .IsInEnum()
            .WithMessage("نوع بخش‌بندی نامعتبر است.");

        RuleFor(x => x.OrgUnitId)
            .NotEmpty()
            .When(x => x.SegmentType == ODCC.Domain.Modules.Analytics.Enums.AnalyticsSegment.OrgUnit)
            .WithMessage("برای بخش‌بندی سازمانی، شناسه‌ی واحد الزامی است.");

        RuleFor(x => x.CampaignId)
            .NotEmpty()
            .When(x => x.SegmentType == ODCC.Domain.Modules.Analytics.Enums.AnalyticsSegment.Campaign)
            .WithMessage("برای بخش‌بندی کمپین، شناسه‌ی کمپین الزامی است.");
    }
}

/// <summary>
/// اعتبارسنجی درخواست جستجوی تحلیلات.
/// </summary>
public sealed class AnalyticsSearchRequestValidator : AbstractValidator<AnalyticsSearchRequest>
{
    public AnalyticsSearchRequestValidator()
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
/// اعتبارسنجی درخواست ایجاد/ویرایش بنچمارک.
/// </summary>
public sealed class SaveBenchmarkRequestValidator : AbstractValidator<SaveBenchmarkRequest>
{
    public SaveBenchmarkRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200)
            .WithMessage("نام بنچمارک الزامی است و حداکثر ۲۰۰ کاراکتر می‌تواند باشد.");

        RuleFor(x => x.Metric)
            .IsInEnum()
            .WithMessage("نوع شاخص نامعتبر است.");

        RuleFor(x => x.TargetValue)
            .InclusiveBetween(-100m, 100m)
            .WithMessage("مقدار هدف باید بین ۱۰۰- تا ۱۰۰ باشد.");

        RuleFor(x => x.OrgUnitId)
            .NotEmpty()
            .When(x => !x.IsCompanyWide)
            .WithMessage("برای بنچمارک سازمانی، شناسه‌ی واحد الزامی است.");
    }
}

/// <summary>
/// اعتبارسنجی درخواست روند زمانی.
/// </summary>
public sealed class TrendRequestValidator : AbstractValidator<TrendRequest>
{
    public TrendRequestValidator()
    {
        RuleFor(x => x.Period)
            .IsInEnum()
            .WithMessage("دوره‌ی زمانی نامعتبر است.");

        RuleFor(x => x.From)
            .LessThanOrEqualTo(x => x.To)
            .When(x => x.From.HasValue && x.To.HasValue)
            .WithMessage("شروع بازه‌ی زمانی نمی‌تواند پس از پایان آن باشد.");
    }
}
