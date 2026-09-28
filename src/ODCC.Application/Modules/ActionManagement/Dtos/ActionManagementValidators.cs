using FluentValidation;
using ODCC.Domain.Modules.ActionManagement.Enums;

namespace ODCC.Application.Modules.ActionManagement.Dtos;

/// <summary>
/// اعتبارسنجی‌های ماژول مدیریت اقدامات با پیام‌های فارسی.
/// </summary>
public sealed class SaveActionPlanRequestValidator : AbstractValidator<SaveActionPlanRequest>
{
    public SaveActionPlanRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان برنامه‌ی اقدام الزامی است.")
            .MaximumLength(300).WithMessage("عنوان برنامه نهایتاً می‌تواند ۳۰۰ کاراکتر باشد.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("توضیحات نهایتاً می‌تواند ۲۰۰۰ کاراکتر باشد.");

        RuleFor(x => x.DueDate)
            .Must(BeValidUtc).When(x => x.DueDate.HasValue)
            .WithMessage("مهلت نهایی باید یک تاریخ معتبر باشد.");

        // مهلت نباید در گذشته (بیش از یک ساعت) باشد — یک برنامه‌ی جدید با
        // مهلت سپری‌شده بی‌فایده است.
        RuleFor(x => x.DueDate)
            .Must(BeInFuture).When(x => x.DueDate.HasValue)
            .WithMessage("مهلت نهایی نمی‌تواند در گذشته باشد.");

        When(x => x.Items is not null, () =>
        {
            RuleForEach(x => x.Items!).SetValidator(new SaveActionItemRequestValidator());
        });
    }

    private static bool BeValidUtc(DateTime? date) => date.HasValue && date.Value != default;

    private static bool BeInFuture(DateTime? date)
    {
        if (!date.HasValue)
        {
            return true;
        }

        // یک ساعت تلورانس برای تفاوت ساعت سرور/کلاینت.
        return date.Value > DateTime.UtcNow.AddHours(-1);
    }
}

public sealed class SaveActionItemRequestValidator : AbstractValidator<SaveActionItemRequest>
{
    public SaveActionItemRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("عنوان آیتم الزامی است.")
            .MaximumLength(300).WithMessage("عنوان آیتم نهایتاً می‌تواند ۳۰۰ کاراکتر باشد.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("شرح آیتم نهایتاً می‌تواند ۲۰۰۰ کاراکتر باشد.");

        RuleFor(x => x.DueDate)
            .Must(BeInFuture).When(x => x.DueDate.HasValue)
            .WithMessage("مهلت نهایی آیتم نمی‌تواند در گذشته باشد.");

        RuleFor(x => x.RemindAt)
            .Must(BeInFuture).When(x => x.RemindAt.HasValue)
            .WithMessage("زمان یادآوری نمی‌تواند در گذشته باشد.");

        // یادآور باید قبل از مهلت نهایی باشد (در غیر این صورت بی‌معنی است).
        RuleFor(x => x)
            .Must(x => !x.RemindAt.HasValue || !x.DueDate.HasValue || x.RemindAt <= x.DueDate)
            .WithMessage("زمان یادآوری باید قبل از مهلت نهایی باشد.");
    }

    private static bool BeInFuture(DateTime? date)
    {
        if (!date.HasValue)
        {
            return true;
        }

        return date.Value > DateTime.UtcNow.AddHours(-1);
    }
}

public sealed class TransitionActionItemRequestValidator : AbstractValidator<TransitionActionItemRequest>
{
    public TransitionActionItemRequestValidator()
    {
        RuleFor(x => x.NewStatus)
            .IsInEnum().WithMessage("وضعیت جدید نامعتبر است.")
            .NotEqual(ActionItemStatus.Open).WithMessage("برای باز کردن آیتم از وضعیت «لغو/ازسرگیری» استفاده کنید.");
    }
}

public sealed class AssessEffectivenessRequestValidator : AbstractValidator<AssessEffectivenessRequest>
{
    public AssessEffectivenessRequestValidator()
    {
        RuleFor(x => x.Rating)
            .IsInEnum().WithMessage("درجه‌ی اثربخشی نامعتبر است.")
            .NotEqual(EffectivenessRating.NotAssessed).WithMessage("باید یک درجه‌ی اثربخشی واقعی انتخاب شود.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("یادداشت اثربخشی نهایتاً می‌تواند ۱۰۰۰ کاراکتر باشد.");
    }
}

public sealed class AddActionCommentRequestValidator : AbstractValidator<AddActionCommentRequest>
{
    public AddActionCommentRequestValidator()
    {
        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("متن دیدگاه الزامی است.")
            .MaximumLength(1000).WithMessage("متن دیدگاه نهایتاً می‌تواند ۱۰۰۰ کاراکتر باشد.");
    }
}

public sealed class RecordOutcomeRequestValidator : AbstractValidator<RecordOutcomeRequest>
{
    public RecordOutcomeRequestValidator()
    {
        // مقدار خروجی می‌تواند null باشد (مثلاً هنوز شاخصی محاسبه نشده).
        RuleFor(x => x.Value)
            .InclusiveBetween(-100m, 100m).When(x => x.Value.HasValue)
            .WithMessage("مقدار شاخص باید بین ۱۰۰- و ۱۰۰ باشد.");
    }
}

public sealed class UploadActionEvidenceRequestValidator : AbstractValidator<UploadActionEvidenceRequest>
{
    /// <summary>حداکثر اندازه‌ی مجاز پیوست: ۲۰ مگابایت.</summary>
    public const long MaxFileBytes = 20 * 1024 * 1024;

    private static readonly string[] AllowedContentTypes =
    [
        "application/pdf",
        "image/png", "image/jpeg", "image/gif", "image/webp",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain", "application/zip"
    ];

    public UploadActionEvidenceRequestValidator()
    {
        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("نام فایل الزامی است.")
            .MaximumLength(256).WithMessage("نام فایل نهایتاً می‌تواند ۲۵۶ کاراکتر باشد.")
            .Must(HaveAllowedExtension).WithMessage("پسوند فایل پشتیبانی نمی‌شود.");

        RuleFor(x => x.FileSizeBytes)
            .GreaterThan(0).WithMessage("فایل خالی قابل آپلود نیست.")
            .LessThanOrEqualTo(MaxFileBytes).WithMessage("اندازه‌ی فایل نمی‌تواند بیشتر از ۲۰ مگابایت باشد.");

        RuleFor(x => x.ContentType)
            .Must(BeAllowed).WithMessage("نوع محتوای فایل پشتیبانی نمی‌شود.");
    }

    private static bool HaveAllowedExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);

        return !string.IsNullOrEmpty(extension)
            && AllowedExtensions.Contains(extension.ToLowerInvariant());
    }

    private static readonly HashSet<string> AllowedExtensions =
    [
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp",
        ".docx", ".xlsx", ".txt", ".zip"
    ];

    private static bool BeAllowed(string contentType)
    {
        return AllowedContentTypes.Contains(contentType.ToLowerInvariant());
    }
}
