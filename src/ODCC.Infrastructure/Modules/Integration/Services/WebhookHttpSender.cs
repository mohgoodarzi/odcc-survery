using System.Net.Http.Headers;
using System.Text;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Domain.Modules.Integration.Entities;
using ODCC.Domain.Modules.Integration.Enums;

namespace ODCC.Infrastructure.Modules.Integration.Services;

/// <summary>
/// کمک‌کننده‌ی ارسال یک درخواست وب‌هوک به یک اندپوینت خارجی.
///
/// <b>امنیت:</b> راز از <see cref="ISecretResolver"/> خوانده می‌شود و هرگز لاگ
/// نمی‌شود. فقط متادیتای پاسخ (کد وضعیت و پیام خطا) ثبت می‌شود.
/// </summary>
internal static class WebhookHttpSender
{
    /// <summary>
    /// ارسال payload به اندپوینت. مهلت زمانی از تنظیمات اندپوینت خوانده می‌شود.
    /// </summary>
    /// <returns>آیا موفق بود، کد وضعیت و پیام خطا.</returns>
    public static async Task<(bool Success, int StatusCode, string? Error)> SendAsync(
        HttpClient httpClient,
        IntegrationEndpoint endpoint,
        string payloadJson,
        IWebhookSigner signer,
        ISecretResolver secretResolver,
        CancellationToken ct = default)
    {
        // امنیت (fail-closed): اگر اندپوینت احراز هویت‌شده باشد ولی راز آن دیگر
        // در پیکربندی نباشد، هرگز درخواست را بدون احراز هویت ارسال نمی‌کنیم.
        // در غیر این صورت می‌توانیم payload را به سمت طرف ثالث به‌صورت
        // غیراحراز هویت‌شده لو بدهیم.
        if (endpoint.AuthType != IntegrationAuthType.None && !secretResolver.IsConfigured(endpoint.SecretRef))
        {
            return (false, 0, "راز اندپوینت پیکربندی نشده است؛ ارسال غیراحراز هویت‌شده ممکن نیست.");
        }

        try
        {
            // مهلت زمانی به‌ازای هر درخواست اعمال می‌شود. تغییر Timeout روی
            // HttpClientِ به‌اشتراک‌گذاشته‌شده (IHttpClientFactory نمونه‌ها را
            // کش می‌کند) بعد از اولین درخواست مجاز نیست و استثنا پرتاب می‌کند.
            var timeout = TimeSpan.FromSeconds(Math.Clamp(endpoint.TimeoutSeconds, 5, 120));

            using var request = new HttpRequestMessage(
                new HttpMethod(endpoint.HttpMethod.ToUpperInvariant()),
                endpoint.Url);

            request.Content = new StringContent(payloadJson, Encoding.UTF8);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            ApplyAuth(request, endpoint, signer, secretResolver);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeout);

            using var response = await httpClient.SendAsync(request, timeoutCts.Token);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (response.IsSuccessStatusCode)
            {
                return (true, (int)response.StatusCode, null);
            }

            var error = TrimForStorage($"HTTP {(int)response.StatusCode}: {Trim(body, 500)}");
            return (false, (int)response.StatusCode, error);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // مهلت زمانی تمام شد (نه لغو توسط فراخوان).
            return (false, 0, "مهلت زمانی تماس با اندپوینت تمام شد.");
        }
        catch (OperationCanceledException)
        {
            throw; // لغو واقعی — باید بالا برود.
        }
        catch (Exception ex)
        {
            return (false, 0, TrimForStorage($"{ex.GetType().Name}: {Trim(ex.Message, 500)}"));
        }
    }

    /// <summary>
    /// اعمال روش احراز هویت روی درخواست. راز از <see cref="ISecretResolver"/>
    /// خوانده می‌شود. فراخوان باید قبل از ارسال مطمئن شود راز پیکربندی شده
    /// است (fail-closed) — اینجا فقط در صورت وجود راز هدر را اضافه می‌کنیم.
    /// </summary>
    private static void ApplyAuth(
        HttpRequestMessage request,
        IntegrationEndpoint endpoint,
        IWebhookSigner signer,
        ISecretResolver secretResolver)
    {
        var secret = secretResolver.Resolve(endpoint.SecretRef);

        switch (endpoint.AuthType)
        {
            case IntegrationAuthType.HmacSignature:
                if (!string.IsNullOrEmpty(secret))
                {
                    var signature = signer.Sign(secret, request.Content is not null
                        ? request.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                        : string.Empty);

                    var headerName = string.IsNullOrWhiteSpace(endpoint.AuthHeaderName)
                        ? "X-ODCC-Signature"
                        : endpoint.AuthHeaderName;

                    request.Headers.TryAddWithoutValidation(headerName, signature);
                }
                break;

            case IntegrationAuthType.BearerToken:
                if (!string.IsNullOrEmpty(secret))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
                }
                break;

            case IntegrationAuthType.ApiKey:
                if (!string.IsNullOrEmpty(secret))
                {
                    var headerName = string.IsNullOrWhiteSpace(endpoint.AuthHeaderName)
                        ? "X-API-Key"
                        : endpoint.AuthHeaderName;

                    request.Headers.TryAddWithoutValidation(headerName, secret);
                }
                break;

            case IntegrationAuthType.Basic:
                if (!string.IsNullOrEmpty(secret))
                {
                    var basicValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(secret));
                    request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicValue);
                }
                break;

            case IntegrationAuthType.None:
            default:
                // بدون احراز هویت.
                break;
        }
    }

    private static string Trim(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static string TrimForStorage(string value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value[..Math.Min(value.Length, 1000)];
}
