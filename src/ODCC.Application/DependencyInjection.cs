using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Services;
using ODCC.Application.Modules.Analytics.Dtos;
using ODCC.Application.Modules.Identity.Dtos;
using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Application.Modules.Campaign.Dtos;
using ODCC.Application.Modules.Response.Dtos;

namespace ODCC.Application;

public static class DependencyInjection
{
    /// <summary>
    /// ثبت سرویس‌های لایه‌ی کاربرد.
    /// </summary>
    public static IServiceCollection AddOdccApplication(this IServiceCollection services)
    {
        // ماژول ممیزی: به‌عنوان ماژول مرجع پیاده‌سازی شده است.
        services.AddScoped<IAuditService, AuditService>();

        // اعتبارسنجی‌های FluentValidation با پیام‌های فارسی.
        // اسامی به‌صورت صریح ثبت می‌شوند تا پیکربندی خودکار نیازی به
        // اسکن اسمبلی (که در زمان شروع برنامه هزینه دارد) نداشته باشد.
        services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<SaveOrgUnitRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<SaveQuestionRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<SaveQuestionnaireRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<SaveSurveyRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<SaveCampaignRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<SaveAnswersRequestValidator>();
        services.AddValidatorsFromAssemblyContaining<ComputeAnalyticsRequestValidator>();

        return services;
    }
}
