using FluentValidation;
using ODCC.Domain.Modules.Workflow.Enums;

namespace ODCC.Application.Modules.Workflow.Dtos;

/// <summary>
/// اعتبارسنجی‌های ماژول گردش کار با پیام‌های فارسی.
/// </summary>
public sealed class SaveWorkflowRequestValidator : AbstractValidator<SaveWorkflowRequest>
{
    public SaveWorkflowRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام گردش کار الزامی است.")
            .MaximumLength(300).WithMessage("نام گردش کار نهایتاً می‌تواند ۳۰۰ کاراکتر باشد.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد گردش کار الزامی است.")
            .MaximumLength(128).WithMessage("کد گردش کار نهایتاً می‌تواند ۱۲۸ کاراکتر باشد.")
            .Matches("^[a-z0-9][a-z0-9-]*$").WithMessage("کد باید با حروف کوچک انگلیسی، عدد یا خط‌تیره باشد.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("توضیحات نهایتاً می‌تواند ۲۰۰۰ کاراکتر باشد.");

        RuleFor(x => x.States)
            .NotEmpty().WithMessage("حداقل یک وضعیت الزامی است.")
            .Must(states => states.Any(s => s.IsInitial)).WithMessage("دقیقاً یک وضعیت باید «اولیه» باشد.")
            .Must(states => states.Count(s => s.IsInitial) == 1).WithMessage("فقط یک وضعیت می‌تواند «اولیه» باشد.");

        RuleForEach(x => x.States).SetValidator(new WorkflowStateRequestValidator());

        When(x => x.Transitions is { Count: > 0 }, () =>
        {
            RuleForEach(x => x.Transitions!).SetValidator(new WorkflowTransitionRequestValidator());

            // گذارها باید بین وضعیت‌های تعریف‌شده باشند.
            RuleFor(x => x)
                .Must(HaveValidStateReferences)
                .WithMessage("گذارها باید بین وضعیت‌های تعریف‌شده باشند.");

            // کد گذار یکتا.
            RuleFor(x => x)
                .Must(HaveUniqueTransitionCodes)
                .WithMessage("کد گذارها باید یکتا باشد.");

            // کد وضعیت یکتا.
            RuleFor(x => x)
                .Must(HaveUniqueStateCodes)
                .WithMessage("کد وضعیت‌ها باید یکتا باشد.");
        });
    }

    private static bool HaveValidStateReferences(SaveWorkflowRequest x)
    {
        var codes = new HashSet<string>(x.States.Select(s => s.Code), StringComparer.Ordinal);
        return x.Transitions!.All(t => codes.Contains(t.FromStateCode) && codes.Contains(t.ToStateCode));
    }

    private static bool HaveUniqueTransitionCodes(SaveWorkflowRequest x)
    {
        var codes = x.Transitions!.Select(t => t.Code).ToList();
        return codes.Distinct(StringComparer.Ordinal).Count() == codes.Count;
    }

    private static bool HaveUniqueStateCodes(SaveWorkflowRequest x)
    {
        var codes = x.States.Select(s => s.Code).ToList();
        return codes.Distinct(StringComparer.Ordinal).Count() == codes.Count;
    }
}

public sealed class WorkflowStateRequestValidator : AbstractValidator<WorkflowStateRequest>
{
    public WorkflowStateRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد وضعیت الزامی است.")
            .MaximumLength(64).WithMessage("کد وضعیت نهایتاً می‌تواند ۶۴ کاراکتر باشد.")
            .Matches("^[a-z0-9][a-z0-9-]*$").WithMessage("کد وضعیت باید با حروف کوچک انگلیسی، عدد یا خط‌تیره باشد.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام وضعیت الزامی است.")
            .MaximumLength(200).WithMessage("نام وضعیت نهایتاً می‌تواند ۲۰۰ کاراکتر باشد.");
    }
}

public sealed class WorkflowTransitionRequestValidator : AbstractValidator<WorkflowTransitionRequest>
{
    public WorkflowTransitionRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("کد گذار الزامی است.")
            .MaximumLength(64).WithMessage("کد گذار نهایتاً می‌تواند ۶۴ کاراکتر باشد.")
            .Matches("^[a-z0-9][a-z0-9-]*$").WithMessage("کد گذار باید با حروف کوچک انگلیسی، عدد یا خط‌تیره باشد.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("نام گذار الزامی است.")
            .MaximumLength(200).WithMessage("نام گذار نهایتاً می‌تواند ۲۰۰ کاراکتر باشد.");

        RuleFor(x => x.FromStateCode)
            .NotEmpty().WithMessage("وضعیت مبدا الزامی است.");

        RuleFor(x => x.ToStateCode)
            .NotEmpty().WithMessage("وضعیت مقصد الزامی است.")
            .NotEqual(x => x.FromStateCode).WithMessage("وضعیت مقصد نمی‌تواند با وضعیت مبدا یکی باشد.");

        // گذار نیازمند تأیید باید مجوز تأییدکننده داشته باشد.
        RuleFor(x => x.ApproverPermission)
            .NotEmpty().WithMessage("گذار نیازمند تأیید، یک مجوز تأییدکننده باید مشخص کند.")
            .When(x => x.RequiresApproval);
    }
}

public sealed class StartWorkflowInstanceRequestValidator : AbstractValidator<StartWorkflowInstanceRequest>
{
    public StartWorkflowInstanceRequestValidator()
    {
        RuleFor(x => x.WorkflowCode)
            .NotEmpty().WithMessage("کد گردش کار الزامی است.");

        RuleFor(x => x.EntityId)
            .NotEqual(Guid.Empty).WithMessage("شناسه‌ی موجودیت الزامی است.");

        RuleFor(x => x.ContextJson)
            .MaximumLength(4000).WithMessage("زمینه‌ی نمونه نهایتاً می‌تواند ۴۰۰۰ کاراکتر باشد.");
    }
}

public sealed class TransitionWorkflowInstanceRequestValidator : AbstractValidator<TransitionWorkflowInstanceRequest>
{
    public TransitionWorkflowInstanceRequestValidator()
    {
        RuleFor(x => x.TransitionCode)
            .NotEmpty().WithMessage("کد گذار الزامی است.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("یادداشت نهایتاً می‌تواند ۱۰۰۰ کاراکتر باشد.");
    }
}

public sealed class DecideWorkflowApprovalRequestValidator : AbstractValidator<DecideWorkflowApprovalRequest>
{
    public DecideWorkflowApprovalRequestValidator()
    {
        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("یادداشت تصمیم نهایتاً می‌تواند ۱۰۰۰ کاراکتر باشد.");
    }
}
