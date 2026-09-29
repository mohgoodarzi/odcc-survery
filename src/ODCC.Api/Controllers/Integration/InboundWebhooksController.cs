using System.Text;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Application.Modules.Integration.Dtos;
using ODCC.Infrastructure.Modules.Integration.Scheduled;

namespace ODCC.Api.Controllers.Integration;

/// <summary>
/// دریافت وب‌هوک‌های ورودی از سامانه‌های خارجی.
///
/// <b>مسیر عمومی:</b> این کنترلر نیاز به احراز هویت ندارد چون توسط سامانه‌های
/// خارجی صدا زده می‌شود. امنیت آن به‌جای JWT به <b>تأیید امضای HMAC</b>
/// متکی است. اگر اندپوینت احراز هویت‌شده باشد و امضا نامعتبر باشد، درخواست
/// رد می‌شود.
///
/// <b>Antiforgery:</b> فیلتسر سراسری ValidateAntiforgeryToken روی همه‌ی
/// درخواست‌های POST اعمال می‌شود. سامانه‌های بیرونی نمی‌توانند توکن CSRF را
/// دریافت کنند، پس این اکشن صریحاً از آن معاف است (<c>[IgnoreAntiforgeryToken]</c>).
/// امنیت به‌جای آن با امضای HMAC تضمین می‌شود.
///
/// <b>بدنه‌ی خام:</b> امضا روی دقیقاً همان بایت‌هایی که فرستنده ارسال کرده
/// محاسبه می‌شود، پس بدنه به‌صورت خام (نه از طریق JSON binder) خوانده می‌شود
/// تا نرمال‌سازی فرمت‌ها باعث رد شدن امضای معتبر نشود.
///
/// مسیر نهایی: <c>/api/webhooks/{endpointCode}</c>
/// </summary>
[ApiController]
[Route("api/webhooks/{endpointCode}")]
[AllowAnonymous]
[EnableRateLimiting("Public")]
public sealed class InboundWebhooksController(
    IIntegrationService integrationService,
    IValidator<ReceiveInboundWebhookRequest> validator,
    IOptions<IntegrationOptions> integrationOptions) : ControllerBase
{
    private readonly IIntegrationService _integrationService = integrationService;
    private readonly IValidator<ReceiveInboundWebhookRequest> _validator = validator;
    private readonly IntegrationOptions _integrationOptions = integrationOptions.Value;

    /// <summary>سقف اندازه‌ی بدنه‌ی وب‌هوک ورودی (۱ مگابایت) — تطبیق با اعتبارسنج.</summary>
    private const long MaxRequestBodyBytes = 1_048_576;

    /// <summary>دریافت یک وب‌هوک ورودی.</summary>
    [HttpPost]
    [IgnoreAntiforgeryToken]
    // سقف اندازه‌ی بدنه در سطح Kestrel (تطبیق با سقع اعتبارسنجی). بدون این،
    // بدنه‌ی بسیار بزرگ قبل از بررسی اندازه به‌طور کامل بافر می‌شود.
    [RequestSizeLimit(MaxRequestBodyBytes)]
    [ProducesResponseType(typeof(WebhookDeliveryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Receive(
        string endpointCode,
        CancellationToken ct)
    {
        // بدنه‌ی خام را می‌خوانیم تا امضا روی بایت‌های دقیق فرستنده محاسبه شود.
        Request.EnableBuffering();

        string payload;

        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            payload = await reader.ReadToEndAsync(ct);
        }

        // بازگرداندن موقعیت جریان برای خواندن‌های بعدی (در صورت نیاز).
        Request.Body.Position = 0;

        // هدر امضا از تنظیمات سراسری خوانده می‌شود (یک منبع واحد).
        var signatureHeaderName = string.IsNullOrWhiteSpace(_integrationOptions.SignatureHeaderName)
            ? "X-ODCC-Signature"
            : _integrationOptions.SignatureHeaderName;

        var signatureHeader = HttpContext.Request.Headers.TryGetValue(signatureHeaderName, out var headerValue)
            ? headerValue.ToString()
            : null;

        // هدر برچسب زمانی (اختیاری) برای محافظت در برابر بازپخش.
        var timestampHeaderName = string.IsNullOrWhiteSpace(_integrationOptions.InboundTimestampHeaderName)
            ? "X-ODCC-Timestamp"
            : _integrationOptions.InboundTimestampHeaderName;

        var timestampHeader = HttpContext.Request.Headers.TryGetValue(timestampHeaderName, out var tsValue)
            ? tsValue.ToString()
            : null;

        var request = new ReceiveInboundWebhookRequest
        {
            EndpointCode = endpointCode,
            PayloadJson = payload,
            SignatureHeader = signatureHeader,
            TimestampHeader = timestampHeader,
            SourceIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        // اعتبارسنج در مرز API (معادل فیلتر اعتبارسنجی مدل که فقط روی
        // پارامترهای [FromBody] سیار اجرا می‌شود).
        var validation = await _validator.ValidateAsync(request, ct);

        if (!validation.IsValid)
        {
            var errors = validation.Errors
                .GroupBy(f => char.ToLowerInvariant(f.PropertyName[0]) + f.PropertyName[1..])
                .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).Distinct().ToArray());

            return BadRequest(new ValidationProblemDetails(errors)
            {
                Title = "اعتبارسنجی ورودی‌ها ناموفق بود",
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["code"] = "validation_failed" }
            });
        }

        var result = await _integrationService.ReceiveInboundWebhookAsync(request, ct);

        if (result.IsFailure)
        {
            // 401 برای امضای نامعتبر (احراز هویت ناموفق)؛ 404 برای اندپوینت ناموجود
            // (عدم افشای وجود).
            var statusCode = result.Error.Code switch
            {
                "integration_signature_invalid" or "integration_signature_missing"
                    or "integration_secret_not_configured"
                    or "integration_timestamp_expired" => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status404NotFound
            };

            return StatusCode(statusCode, new ProblemDetails
            {
                Title = statusCode == StatusCodes.Status401Unauthorized ? "احراز هویت وب‌هوک ناموفق بود" : "اندپوینت یافت نشد",
                Status = statusCode,
                Detail = result.Error.Message,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Ok(result.Value);
    }
}
