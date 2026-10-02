using System.Text.RegularExpressions;
using System.Globalization;
using ODCC.Domain.Common;

namespace ODCC.Infrastructure.Modules.Notification.Services;

/// <summary>
/// قالب‌های پیش‌فرض توکار: متن آماده برای تمام کدهای قالبی که سامانه استفاده
/// می‌کند. اگر مدیر قالبی را در پایگاه داده تعریف نکرده یا غیرفعال کرده باشد،
/// رندر از این متن‌ها استفاده می‌کند تا سامانه همیشه کار کند.
///
/// <b>قواعد متغیرها:</b> متغیرها در قالب به شکل <c>{{name}}</c> نوشته می‌شوند
/// و در زمان رندر با مقدار جایگزین می‌شوند. متغیرهای ناموجود با رشته‌ی خالی
/// جایگزین می‌شوند (نه خطا) تا قالب‌ها هرگز بشکنند.
/// </summary>
internal static class DefaultNotificationTemplates
{
    /// <summary>نگاشت کد قالب → متن پیش‌فرض به ازای هر زبان.</summary>
    private static readonly Dictionary<string, Dictionary<Language, (string Subject, string Body)>> Templates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // --- دعوت‌نامه و یادآور کمپین -------------------------------------
            ["campaign_invitation"] = new()
            {
                [Language.Fa] = (
                    "دعوت به نظرسنجی «{{survey_title}}»",
                    "سلام {{recipient_name}} عزیز،\n\nشما برای شرکت در نظرسنجی «{{survey_title}}» دعوت شده‌اید. پاسخ شما به بهبود کار ما کمک می‌کند.\n\nلطفاً از طریق لینک زیر پاسخ دهید:\n{{link}}\n\nبا تشکر,\nسامانه نظرسنجی سازمانی"),
                [Language.En] = (
                    "Invitation to survey \"{{survey_title}}\"",
                    "Hello {{recipient_name}},\n\nYou are invited to participate in the survey \"{{survey_title}}\". Your feedback helps us improve.\n\nPlease respond via the following link:\n{{link}}\n\nThank you,\nEnterprise Survey Platform")
            },
            ["campaign_reminder"] = new()
            {
                [Language.Fa] = (
                    "یادآوری: نظرسنجی «{{survey_title}}» هنوز پاسخ داده نشده است",
                    "سلام {{recipient_name}} عزیز,\n\nاین یک یادآوری دوستانه است که نظرسنجی «{{survey_title}}» هنوز توسط شما پاسخ داده نشده است. کمی زمان باقی است.\n\nپاسخ از طریق لینک:\n{{link}}\n\nبا تشکر,\nسامانه نظرسنجی سازمانی"),
                [Language.En] = (
                    "Reminder: survey \"{{survey_title}}\" is still unanswered",
                    "Hello {{recipient_name}},\n\nThis is a friendly reminder that you have not yet responded to the survey \"{{survey_title}}\". There is still time.\n\nRespond via the link:\n{{link}}\n\nThank you,\nEnterprise Survey Platform")
            },

            // --- چرخه‌ی عمر نظرسنجی --------------------------------------------
            ["survey_closed"] = new()
            {
                [Language.Fa] = (
                    "نظرسنجی «{{survey_title}}» بسته شد",
                    "نظرسنجی «{{survey_title}}» به‌طور رسمی بسته شد و دیگر پاسخ‌گویی نمی‌پذیرد. نتایج تجمیعی به‌زودی در بخش تحلیل ها در دسترس است."),
                [Language.En] = (
                    "Survey \"{{survey_title}}\" is closed",
                    "The survey \"{{survey_title}}\" has been officially closed and no longer accepts responses. Aggregate results will soon be available in the analytics section.")
            },
            ["survey_started"] = new()
            {
                [Language.Fa] = (
                    "نظرسنجی «{{survey_title}}» باز شد",
                    "نظرسنجی «{{survey_title}}» اکنون فعال است و پاسخ می‌پذیرد.\n\nشما می‌توانید از بخش «نظرسنجی‌های من» شروع به پاسخ دهید."),
                [Language.En] = (
                    "Survey \"{{survey_title}}\" is now open",
                    "The survey \"{{survey_title}}\" is now active and accepts responses.\n\nYou can start responding from \"My Surveys\".")
            },

            // --- تحلیلات -------------------------------------------------------
            ["analytics_low_metric"] = new()
            {
                [Language.Fa] = (
                    "هشدار: افت شاخص در نظرسنجی «{{survey_title}}»",
                    "شاخص «{{metric_name}}» در نظرسنجی «{{survey_title}}» به {{metric_value}} رسیده است که از آستانه‌ی هشدار ({{threshold}}) پایین‌تر است.\n\nتوصیه می‌شود نتایج را در بخش تحلیل ها بررسی کنید.\n\nتوجه: این هشدار فقط بر اساس داده‌های تجمیعی است و هیچ پاسخ‌دهنده‌ای شناسایی نمی‌شود."),
                [Language.En] = (
                    "Alert: metric drop in survey \"{{survey_title}}\"",
                    "The \"{{metric_name}}\" metric for survey \"{{survey_title}}\" reached {{metric_value}}, below the alert threshold ({{threshold}}).\n\nWe recommend reviewing the results in the analytics section.\n\nNote: this alert is based on aggregate data only; no respondent is identified.")
            },

            // --- گزارش‌گیری -----------------------------------------------------
            ["report_ready"] = new()
            {
                [Language.Fa] = (
                    "گزارش «{{report_name}}» آماده است",
                    "خروجی گزارش «{{report_name}}» با موفقیت تولید شد.\n\nتعداد ردیف: {{row_count}}\nاندازه‌ی فایل: {{file_size}}\n\nمی‌توانید آن را از بخش گزارش‌گیری دانلود کنید."),
                [Language.En] = (
                    "Report \"{{report_name}}\" is ready",
                    "The output of report \"{{report_name}}\" was generated successfully.\n\nRows: {{row_count}}\nFile size: {{file_size}}\n\nYou can download it from the reports section.")
            },
            ["report_failed"] = new()
            {
                [Language.Fa] = (
                    "شکست اجرای گزارش «{{report_name}}»",
                    "اجرای گزارش «{{report_name}}» ناموفق بود.\n\nخطا: {{error_message}}\n\nلطفاً جزئیات را در بخش گزارش‌گیری بررسی کنید."),
                [Language.En] = (
                    "Report \"{{report_name}}\" execution failed",
                    "Execution of report \"{{report_name}}\" failed.\n\nError: {{error_message}}\n\nPlease review the details in the reports section.")
            },

            // --- سیستمی ----------------------------------------------------------
            ["system_notification"] = new()
            {
                [Language.Fa] = ("{{subject}}", "{{body}}"),
                [Language.En] = ("{{subject}}", "{{body}}")
            },

            // --- مدیریت اقدامات ---------------------------------------------------
            // همه‌ی این قالب‌ها فقط متادیتای عمومی (عنوان کار، نام مسئول، مهلت)
            // را حمل می‌کنند — هرگز محتوای پاسخ یک پاسخ‌دهنده را نه.
            ["action_assigned"] = new()
            {
                [Language.Fa] = (
                    "کار جدید: {{action_title}}",
                    "سلام {{recipient_name}} عزیز،\n\nکار «{{action_title}}» در برنامه‌ی اقدام «{{plan_title}}» به شما منتقل شد.\nمهلت نهایی: {{due_date}}\nاولویت: {{priority}}\n\nمی‌توانید جزئیات را در بخش «اقدامات» ببینید."),
                [Language.En] = (
                    "New task: {{action_title}}",
                    "Hello {{recipient_name}},\n\nThe task \"{{action_title}}\" in action plan \"{{plan_title}}\" has been assigned to you.\nDue date: {{due_date}}\nPriority: {{priority}}\n\nYou can view the details in the \"Actions\" section.")
            },
            ["action_reminder"] = new()
            {
                [Language.Fa] = (
                    "یادآوری: کار «{{action_title}}» سررسیده است",
                    "سلام {{recipient_name}} عزیز،\n\nکار «{{action_title}}» در برنامه‌ی اقدام «{{plan_title}}» سررسیده شده است.\nمهلت نهایی: {{due_date}}\n\nلطفاً وضعیت آن را در بخش «اقدامات» به‌روزرسانی کنید."),
                [Language.En] = (
                    "Reminder: task \"{{action_title}}\" is due",
                    "Hello {{recipient_name}},\n\nThe task \"{{action_title}}\" in action plan \"{{plan_title}}\" is now due.\nDue date: {{due_date}}\n\nPlease update its status in the \"Actions\" section.")
            },
            ["action_escalated"] = new()
            {
                [Language.Fa] = (
                    "تشدید: کار «{{action_title}}» سررسیده شده است",
                    "کار «{{action_title}}» در برنامه‌ی اقدام «{{plan_title}}» سررسیده شده و یک درجه تشدید شد.\n\nمسئول: {{assignee_name}}\nمهلت نهایی: {{due_date}}\nدرجه‌ی تشدید: {{escalation_level}}\n\nلطفاً برای رفع تأخیر اقدام کنید."),
                [Language.En] = (
                    "Escalation: task \"{{action_title}}\" is overdue",
                    "The task \"{{action_title}}\" in action plan \"{{plan_title}}\" is overdue and has been escalated.\n\nAssignee: {{assignee_name}}\nDue date: {{due_date}}\nEscalation level: {{escalation_level}}\n\nPlease act to resolve the delay.")
            },
            ["action_completed"] = new()
            {
                [Language.Fa] = (
                    "کار «{{action_title}}» تکمیل شد",
                    "کار «{{action_title}}» در برنامه‌ی اقدام «{{plan_title}}» توسط {{assignee_name}} تکمیل شد.\n\nشما می‌توانید اثربخشی آن را در بخش «اقدامات» ارزیابی کنید."),
                [Language.En] = (
                    "Task \"{{action_title}}\" completed",
                    "The task \"{{action_title}}\" in action plan \"{{plan_title}}\" was completed by {{assignee_name}}.\n\nYou can assess its effectiveness in the \"Actions\" section.")
            },

            // --- گردش کار ----------------------------------------------------------
            // این قالب فقط متادیتای عمومی گذار (کد گذار، وضعیت مبدا/مقصد) را
            // حمل می‌کند — هرگز داده‌ی حساس.
            ["workflow_approval_requested"] = new()
            {
                [Language.Fa] = (
                    "درخواست تأیید: گذار «{{transition_code}}»",
                    "سلام {{recipient_name}} عزیز،\n\nیک درخواست تأیید برای گذار «{{transition_code}}» (از «{{from_state}}» به «{{to_state}}») در انتظار تصمیم شماست.\nانقضا: {{expiry}}\n\nلطفاً از بخش «گردش کار» تصمیم خود را ثبت کنید."),
                [Language.En] = (
                    "Approval requested: transition \"{{transition_code}}\"",
                    "Hello {{recipient_name}},\n\nAn approval is pending for transition \"{{transition_code}}\" (from \"{{from_state}}\" to \"{{to_state}}\").\nExpires: {{expiry}}\n\nPlease record your decision in the \"Workflow\" section.")
            }
        };

    /// <summary>آیا برای این کد قالب پیش‌فرض توکاری وجود دارد؟</summary>
    public static bool Has(string templateCode) =>
        Templates.ContainsKey(templateCode ?? string.Empty);

    /// <summary>
    /// گرفتن متن پیش‌فرض برای یک زبان. اگر زبان درخواستی نبود، فارسی و سپس
    /// اولین زبان موجود استفاده می‌شود.
    /// </summary>
    public static (string Subject, string Body) Get(string templateCode, Language language)
    {
        if (!Templates.TryGetValue(templateCode ?? string.Empty, out var languages))
        {
            return ($"[{templateCode}]", string.Empty);
        }

        if (languages.TryGetValue(language, out var text))
        {
            return text;
        }

        if (languages.TryGetValue(Language.Fa, out var persian))
        {
            return persian;
        }

        return languages.Values.First();
    }

    /// <summary>کد همه‌ی قالب‌های توکار (برای seed کردن).</summary>
    public static IReadOnlyCollection<string> Codes => Templates.Keys;
}

/// <summary>
/// جایگزینی متغیرهای <c>{{name}}</c> در یک متن. متغیرهای ناموجود خالی می‌شوند
/// تا قالب‌ها هرگز نشکنند. کلیدها به‌صورت case-insensitive تطبیق می‌شوند.
/// </summary>
internal static partial class TemplateVariableReplacer
{
    public static string Replace(string template, IReadOnlyDictionary<string, string?> properties)
    {
        if (string.IsNullOrEmpty(template) || properties.Count == 0)
        {
            return template;
        }

        var lookup = new Dictionary<string, string?>(properties.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var pair in properties)
        {
            lookup[pair.Key] = pair.Value;
        }

        return TemplateVariableRegex().Replace(template, match =>
        {
            var key = match.Groups[1].Value.Trim();

            return lookup.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
                ? value
                : string.Empty;
        });
    }

    [GeneratedRegex(@"\{\{\s*([A-Za-z0-9_]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex TemplateVariableRegex();
}

/// <summary>کمکی برای قالب‌بندی اعداد در زبان نشان داده‌شده.</summary>
internal static class NotificationFormatter
{
    /// <summary>قالب‌بندی یک عدد اعشاری با ارقام مناسب زبان.</summary>
    public static string FormatNumber(decimal value, Language language)
    {
        var culture = language == Language.Fa
            ? new CultureInfo("fa-IR")
            : CultureInfo.InvariantCulture;

        return Math.Round(value, 1).ToString("0.0", culture);
    }

    /// <summary>قالب‌بندی اندازه‌ی فایل (بایت) به شکل خوانا.</summary>
    public static string FormatFileSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes}B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1}KB";
        return $"{bytes / (1024.0 * 1024):F1}MB";
    }
}
