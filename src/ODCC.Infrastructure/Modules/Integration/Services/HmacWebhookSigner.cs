using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Infrastructure.Modules.Integration.Scheduled;

namespace ODCC.Infrastructure.Modules.Integration.Services;

/// <summary>
/// امضای وب‌هوک با HMAC-SHA256.
///
/// <b>امنیت:</b> از مقایسه‌ی زمان‌ثابت (<see cref="CryptographicOperations.FixedTimeEquals"/>)
/// در تأیید استفاده می‌کند تا در برابر حملات timing مقاوم باشد. امضا به شکل
/// <c>sha256=&lt;hex&gt;</c> است.
/// </summary>
public sealed class HmacWebhookSigner(IOptions<IntegrationOptions> options) : IWebhookSigner
{
    private readonly IntegrationOptions _options = options.Value;

    /// <inheritdoc/>
    public string Sign(string secret, string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <inheritdoc/>
    public bool Verify(string secret, string payload, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(signatureHeader))
        {
            return false;
        }

        // پشتیبانی از هر دو شکل: «sha256=<hex>» و «<hex>».
        var expected = Sign(secret, payload);
        var received = signatureHeader.Trim();

        if (!received.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            received = "sha256=" + received;
        }

        // مقایسه‌ی زمان‌ثابت برای جلوگیری از نشت اطلاعات از طریق زمان اجرا.
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var receivedBytes = Encoding.UTF8.GetBytes(received);

        return expectedBytes.Length == receivedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes);
    }

    /// <summary>نام هدر امضا از پیکربندی.</summary>
    public string SignatureHeaderName => _options.SignatureHeaderName;
}
