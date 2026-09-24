using ODCC.Domain.Common;

namespace ODCC.Domain.Modules.Survey.Entities;

/// <summary>
/// ترجمه‌ی عنوان، توضیح و پیام‌های اول/آخر یک نظرسنجی.
/// پیام‌های خوش‌آمدگویی و تشکر هم ترجمه می‌شوند تا تجربه‌ی پاسخ‌گویی
/// در هر زبان یکدست باشد.
/// </summary>
public class SurveyLocalization : Localization
{
    /// <summary>عنوان نظرسنجی.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>توضیح نظرسنجی (اختیاری).</summary>
    public string? Description { get; set; }

    /// <summary>پیامی که پاسخ‌دهنده در شروع می‌بیند (اختیاری).</summary>
    public string? WelcomeMessage { get; set; }

    /// <summary>پیامی که پاسخ‌دهنده پس از ارسال می‌بیند (اختیاری).</summary>
    public string? ThankYouMessage { get; set; }

    /// <summary>شناسه‌ی نظرسنجی والد.</summary>
    public Guid SurveyId { get; set; }

    /// <summary>به‌روزرسانی مقادیر ترجمه.</summary>
    public void Update(string title, string? description, string? welcomeMessage, string? thankYouMessage)
    {
        Title = title;
        Description = description;
        WelcomeMessage = welcomeMessage;
        ThankYouMessage = thankYouMessage;
    }
}
