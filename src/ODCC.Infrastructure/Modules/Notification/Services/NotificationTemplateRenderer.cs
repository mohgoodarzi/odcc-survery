using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Infrastructure.Modules.Notification.Services;

/// <summary>
/// پیاده‌سازی رندر قالب: اولویت با قالب فعال پایگاه داده است؛ اگر نبود،
/// متن پیش‌فرض توکار استفاده می‌شود. سپس متغیرها جایگزین می‌شوند.
/// </summary>
public sealed class NotificationTemplateRenderer(
    INotificationTemplateRepository templateRepository) : INotificationTemplateRenderer
{
    private readonly INotificationTemplateRepository _templateRepository = templateRepository;

    /// <inheritdoc/>
    public async Task<RenderedTemplate> RenderAsync(
        string templateCode,
        Language language,
        IReadOnlyDictionary<string, string?> properties,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateCode);
        properties ??= new Dictionary<string, string?>();

        string subject;
        string body;

        // اولویت اول: قالب فعال در پایگاه داده (قابل ویرایش توسط مدیر).
        var template = await _templateRepository.FindActiveByCodeAsync(templateCode, ct);

        if (template is not null)
        {
            var localization = template.Localizations.FirstOrDefault(l => l.Language == language)
                ?? template.Localizations.FirstOrDefault(l => l.Language == Language.Fa)
                ?? template.Localizations.FirstOrDefault();

            subject = localization?.Subject ?? DefaultNotificationTemplates.Get(templateCode, language).Subject;
            body = localization?.Body ?? DefaultNotificationTemplates.Get(templateCode, language).Body;
        }
        else
        {
            // اولویت دوم: متن پیش‌فرض توکار.
            (subject, body) = DefaultNotificationTemplates.Get(templateCode, language);
        }

        // متغیرهای پایه همیشه در دسترس قالب هستند.
        var effectiveProperties = new Dictionary<string, string?>(properties.Count + 4, StringComparer.OrdinalIgnoreCase)
        {
            ["now"] = DateTime.UtcNow.ToString("u")
        };

        foreach (var pair in properties)
        {
            effectiveProperties[pair.Key] = pair.Value;
        }

        return new RenderedTemplate(
            TemplateVariableReplacer.Replace(subject, effectiveProperties),
            TemplateVariableReplacer.Replace(body, effectiveProperties));
    }
}
