using FluentValidation;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Application.Modules.Notification.Dtos;

/// <summary>
/// اعتبارسنجی ارسال یک اعلان.
/// </summary>
public sealed class SendNotificationRequestValidator : AbstractValidator<SendNotificationRequest>
{
    public SendNotificationRequestValidator()
    {
        RuleFor(x => x.TemplateCode)
            .NotEmpty()
            .MaximumLength(128)
            .Matches("^[a-z0-9_-]+$")
            .WithMessage("کد قالب فقط می‌تواند شامل حروف کوچک، عدد، خط زیر و خط تیره باشد.");

        RuleFor(x => x.Recipient).NotNull();

        RuleFor(x => x.Url)
            .MaximumLength(512);

        // برای کانال درون‌برنامه‌ای، کاربر لازم است (چون صندوق ورودی به کاربر وصل است).
        RuleFor(x => x.Recipient.UserId)
            .NotNull()
            .When(x => x.Channel == NotificationChannel.InApp)
            .WithMessage("ارسال درون‌برنامه‌ای نیازمند شناسه‌ی کاربر است.");
    }
}

/// <summary>اعتبارسنجی ارسال چندین اعلان.</summary>
public sealed class SendNotificationsRequestValidator : AbstractValidator<SendNotificationsRequest>
{
    public SendNotificationsRequestValidator()
    {
        RuleFor(x => x.TemplateCode)
            .NotEmpty()
            .MaximumLength(128)
            .Matches("^[a-z0-9_-]+$")
            .WithMessage("کد قالب فقط می‌تواند شامل حروف کوچک، عدد، خط زیر و خط تیره باشد.");

        RuleFor(x => x.Recipients)
            .NotNull()
            .Must(list => list.Count > 0)
            .WithMessage("حداقل یک گیرنده لازم است.")
            .Must(list => list.Count <= 5000)
            .WithMessage("حداکثر ۵۰۰۰ گیرنده در هر درخواست.");

        RuleFor(x => x.Url).MaximumLength(512);

        // برای کانال درون‌برنامه‌ای، همه‌ی گیرندگان باید کاربر سامانه داشته
        // باشند (صندوق ورودی به کاربر وصل است). اعتبارسنجی در سطح مجموعه انجام
        // می‌شود تا به کانال درخواست اصلی دسترسی باشد.
        RuleFor(x => x.Recipients)
            .Must((request, list) => request.Channel != NotificationChannel.InApp
                || list.All(r => r.UserId is not null))
            .WithMessage("ارسال درون‌برنامه‌ای نیازمند شناسه‌ی کاربر برای همه‌ی گیرندگان است.");
    }
}

/// <summary>اعتبارسنجی ذخیره‌ی قالب.</summary>
public sealed class SaveNotificationTemplateRequestValidator : AbstractValidator<SaveNotificationTemplateRequest>
{
    public SaveNotificationTemplateRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(128)
            .Matches("^[a-z0-9_-]+$")
            .WithMessage("کد قالب فقط می‌تواند شامل حروف کوچک، عدد، خط زیر و خط تیره باشد.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Localizations)
            .NotNull()
            .Must(list => list.Count > 0)
            .WithMessage("حداقل یک زبان لازم است.");

        RuleForEach(x => x.Localizations).ChildRules(loc =>
        {
            loc.RuleFor(l => l.Subject).NotEmpty().MaximumLength(500);
            loc.RuleFor(l => l.Body).NotEmpty().MaximumLength(4000);
        });
    }
}

/// <summary>اعتبارسنجی به‌روزرسانی ترجیح.</summary>
public sealed class UpdateNotificationPreferenceRequestValidator : AbstractValidator<UpdateNotificationPreferenceRequest>
{
    public UpdateNotificationPreferenceRequestValidator()
    {
        RuleFor(x => x.Channel).IsInEnum();
    }
}
