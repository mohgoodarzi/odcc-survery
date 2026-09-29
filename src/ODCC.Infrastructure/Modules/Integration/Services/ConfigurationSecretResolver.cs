using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Infrastructure.Modules.Integration.Scheduled;

namespace ODCC.Infrastructure.Modules.Integration.Services;

/// <summary>
/// خواندن رازهای یکپارچه‌سازی از پیکربندی ایمن.
///
/// <b>امنیت (حیاتی):</b> رازها هرگز در پایگاه داده ذخیره نمی‌شوند. این سرویس
/// مقدار را از <c>IConfiguration</c> می‌خواند که می‌تواند از user secrets،
/// متغیرهای محیطی (<c>Integrations__Secrets__{name}</c>) یا یک فروشگاه راز
/// خارجی تامین شود. اگر رازی پیکربندی نشده باشد، <c>null</c> برمی‌گرداند
/// (fail-closed) تا هرگز با راز خالی یا پیش‌فرض کار نکنیم.
/// </summary>
public sealed class ConfigurationSecretResolver(
    IOptions<IntegrationOptions> options,
    IConfiguration configuration) : ISecretResolver
{
    private readonly IntegrationOptions _options = options.Value;
    private readonly IConfiguration _configuration = configuration;

    /// <inheritdoc/>
    public string? Resolve(string? secretRef)
    {
        if (string.IsNullOrWhiteSpace(secretRef))
        {
            return null;
        }

        // ۱. از بخش Secrets گزینه‌ها (می‌تواند از user secrets بیاید).
        if (_options.Secrets.TryGetValue(secretRef, out var fromOptions) && !string.IsNullOrWhiteSpace(fromOptions))
        {
            return fromOptions;
        }

        // ۲. از پیکربندی مسیر-نقطه: Integrations:Secrets:{name} (متغیر محیطی
        // با زیرخط دوتایی هم اینجا حل می‌شود).
        var fromConfig = _configuration[$"Integrations:Secrets:{secretRef}"];

        return string.IsNullOrWhiteSpace(fromConfig) ? null : fromConfig;
    }

    /// <inheritdoc/>
    public bool IsConfigured(string? secretRef) => !string.IsNullOrWhiteSpace(Resolve(secretRef));
}
