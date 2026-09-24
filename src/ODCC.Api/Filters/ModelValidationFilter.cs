using System.Collections;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ODCC.Api.Filters;

/// <summary>
/// اجرای اعتبارسنج‌های ثبت‌شده‌ی FluentValidation روی پارامترهای [FromBody] اکشن.
///
/// اعتبارسنج‌ها در لایه‌ی کاربرد تعریف و در DI ثبت می‌شوند، اما MVC به‌صورت
/// پیش‌فرض آن‌ها را اجرا نمی‌کند. این فیلتر مرز اعتبارسنجی سرور را فعال می‌کند:
/// درخواست نامعتبر هرگز به سرویس کاربرد نمی‌رسد و با <c>ValidationProblemDetails</c>
/// (کلیدهای فیلد + کد مستقل از زبان) برمی‌گردد.
///
/// پیام‌های اعتبارسنج از همان ابتدا مستقل از زبان نیستند (فارسی هستند) اما
/// کلید فیلد و کد خطا قرارداد API هستند و کلاینت می‌تواند آن‌ها را نمایش دهد.
/// </summary>
public sealed class ModelValidationFilter(IServiceProvider serviceProvider, ILogger<ModelValidationFilter> logger) : IAsyncActionFilter
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly ILogger<ModelValidationFilter> _logger = logger;

    private static readonly Action<ILogger, string, Exception?> ValidationFailed = LoggerMessage.Define<string>(
        LogLevel.Debug,
        new EventId(1, "ModelValidationFailed"),
        "اعتبارسنجی مدل {ModelType} ناموفق بود.");

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            // فقط مدل‌های مرجع (record/class) اعتبارسنجی می‌شوند؛ پارامترهای مسیر/کوئری رایج هستند.
            var argumentType = argument.GetType();
            if (!argumentType.IsClass)
            {
                continue;
            }

            var validator = _serviceProvider.GetService(typeof(IValidator<>).MakeGenericType(argumentType));
            if (validator is not IValidator typedValidator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await typedValidator.ValidateAsync(validationContext, context.HttpContext.RequestAborted);

            if (result.IsValid)
            {
                continue;
            }

            ValidationFailed(_logger, argumentType.Name, null);

            // کلیدها به camelCase تبدیل می‌شوند تا مستقیماً با فیلدهای فرم کلاینت تطابق داشته باشند.
            var errors = result.Errors
                .GroupBy(failure => ToCamelCase(failure.PropertyName))
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(failure => failure.ErrorMessage).Distinct().ToArray());

            context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors)
            {
                Title = "اعتبارسنجی ورودی‌ها ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["code"] = "validation_failed" }
            });

            return;
        }

        await next();
    }

    /// <summary>تبدیل نام ویژگی (مثلاً «SurveyId») به camelCase (مثلاً «surveyId»).</summary>
    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        if (name.Length == 1)
        {
            return name.ToLowerInvariant();
        }

        return $"{char.ToLowerInvariant(name[0])}{name[1..]}";
    }
}
