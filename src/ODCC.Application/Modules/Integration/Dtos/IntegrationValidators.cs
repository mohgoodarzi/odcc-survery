using FluentValidation;
using ODCC.Domain.Modules.Integration.Enums;

namespace ODCC.Application.Modules.Integration.Dtos;

/// <summary>
/// اعتبارسنجی‌های ماژول یکپارچه‌سازی با پیام‌های فارسی.
/// </summary>
public sealed class SaveIntegrationEndpointRequestValidator : AbstractValidator<SaveIntegrationEndpointRequest>
{
    private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH" };

    public SaveIntegrationEndpointRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام اندپوینت الزامی است.")
            .MaximumLength(300).WithMessage("نام اندپوینت نهایتاً می‌تواند ۳۰۰ کاراکتر باشد.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد اندپوینت الزامی است.")
            .MaximumLength(128).WithMessage("کد اندپوینت نهایتاً می‌تواند ۱۲۸ کاراکتر باشد.")
            .Matches("^[a-z0-9][a-z0-9-]*$").WithMessage("کد باید با حروف کوچک انگلیسی، عدد یا خط‌تیره باشد.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("توضیحات نهایتاً می‌تواند ۲۰۰۰ کاراکتر باشد.");

        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("آدرس اندپوینت الزامی است.")
            .MaximumLength(2048).WithMessage("آدرس نهایتاً می‌تواند ۲۰۴۸ کاراکتر باشد.")
            .Must((x, url) => BeValidUrl(x, url)).WithMessage("آدرس باید یک URL معتبر باشد.");

        RuleFor(x => x.HttpMethod)
            .NotEmpty().WithMessage("روش HTTP الزامی است.")
            .Must(m => AllowedMethods.Contains(m.ToUpperInvariant())).WithMessage("روش HTTP باید POST، PUT یا PATCH باشد.");

        // اندپوینت‌های احراز هویت‌شده باید راز داشته باشند.
        RuleFor(x => x.SecretRef)
            .NotEmpty().WithMessage("اندپوینت‌های احراز هویت‌شده باید یک نام منطقی راز داشته باشند.")
            .MaximumLength(128).WithMessage("نام راز نهایتاً می‌تواند ۱۲۸ کاراکتر باشد.")
            .Matches("^[a-zA-Z0-9][a-zA-Z0-9_-]*$").WithMessage("نام راز فقط می‌تواند حروف انگلیسی، عدد، خط زیر یا خط تیره باشد.")
            .When(x => x.AuthType != IntegrationAuthType.None);

        // وب‌هوک خروجی باید https باشد تا رازها در مسیر لو نروند.
        RuleFor(x => x.Url)
            .Must(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .WithMessage("وب‌هوک‌های خروجی باید از HTTPS استفاده کنند تا رازها در مسیر افشا نشوند.")
            .When(x => x.Type == IntegrationType.OutboundWebhook);

        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(5, 120).WithMessage("مهلت زمانی باید بین ۵ و ۱۲۰ ثانیه باشد.");

        RuleFor(x => x.MaxRetries)
            .InclusiveBetween(0, 10).WithMessage("حداکثر تعداد تلاش مجدد باید بین ۰ و ۱۰ باشد.");

        RuleFor(x => x.AuthHeaderName)
            .MaximumLength(128).WithMessage("نام هدر احراز هویت نهایتاً می‌تواند ۱۲۸ کاراکتر باشد.");

        RuleForEach(x => x.SubscribedEvents)
            .NotEmpty().WithMessage("نوع رویداد نمی‌تواند خالی باشد.")
            .MaximumLength(128).WithMessage("نوع رویداد نهایتاً می‌تواند ۱۲۸ کاراکتر باشد.");
    }

    /// <summary>
    /// اعتبارسنجی آدرس: وب‌هوک ورودی مسیر نسبی (مثلاً «/webhooks/hr») می‌پذیرد
    /// چون سامانه خودش روی آن مسیر گوش می‌دهد؛ سایر انواع باید URL مطلق باشند.
    /// </summary>
    private static bool BeValidUrl(SaveIntegrationEndpointRequest request, string url)
    {
        if (request.Type == IntegrationType.InboundWebhook)
        {
            // مسیر نسبی باید با «/» شروع شود و نباید شامل مولفه‌ی scheme/authority باشد.
            return !url.Contains("://", StringComparison.Ordinal)
                && url.Length > 0 && url[0] == '/'
                && Uri.IsWellFormedUriString(url, UriKind.Relative);
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

/// <summary>
/// اعتبارسنجی درخواست آزمون اتصال اندپوینت: payload آزمونی باید محدود باشد
/// تا ارسال مقدار نامحدود به سمت سرویس بیرونی ممکن نباشد.
/// </summary>
public sealed class TestIntegrationEndpointRequestValidator : AbstractValidator<TestIntegrationEndpointRequest>
{
    public const int MaxPayloadBytes = 1 * 1024 * 1024; // ۱ مگابایت

    public TestIntegrationEndpointRequestValidator()
    {
        RuleFor(x => x.PayloadJson)
            .Must(BeWithinSizeLimit).WithMessage("payload آزمونی نمی‌تواند بیشتر از ۱ مگابایت باشد.");
    }

    private static bool BeWithinSizeLimit(string? payload)
    {
        return payload is null || System.Text.Encoding.UTF8.GetByteCount(payload) <= MaxPayloadBytes;
    }
}

public sealed class ReceiveInboundWebhookRequestValidator : AbstractValidator<ReceiveInboundWebhookRequest>
{
    public const int MaxPayloadBytes = 1 * 1024 * 1024; // ۱ مگابایت

    public ReceiveInboundWebhookRequestValidator()
    {
        RuleFor(x => x.EndpointCode)
            .NotEmpty().WithMessage("کد اندپوینت الزامی است.")
            .MaximumLength(128).WithMessage("کد اندپوینت نامعتبر است.");

        RuleFor(x => x.PayloadJson)
            .NotEmpty().WithMessage("بدنه‌ی وب‌هوک الزامی است.")
            .Must(BeWithinSizeLimit).WithMessage("بدنه‌ی وب‌هوک نمی‌تواند بیشتر از ۱ مگابایت باشد.");
    }

    private static bool BeWithinSizeLimit(string payload)
    {
        return System.Text.Encoding.UTF8.GetByteCount(payload) <= MaxPayloadBytes;
    }
}
