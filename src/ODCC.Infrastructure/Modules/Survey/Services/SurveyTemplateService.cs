using ODCC.Application.Abstractions;
using ODCC.Application.Languages;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Questionnaire.Enums;
using ODCC.Domain.Modules.Survey.Entities;
using ODCC.Domain.Modules.Survey.Enums;
using ODCC.Domain.Modules.Survey.Events;
using SurveyTemplateEntity = ODCC.Domain.Modules.Survey.Entities.SurveyTemplate;

namespace ODCC.Infrastructure.Modules.Survey.Services;

/// <summary>
/// پیاده‌سازی سرویس قالب‌های نظرسنجی.
///
/// قالب یک «پرسشنامه + تنظیمات» قابل‌استفاده‌ی مجدد است. هنگام ساخت نظرسنجی از
/// قالب، تنظیمات کپی می‌شوند و ارتباط دائمی بین نظرسنجی و قالب وجود ندارد.
///
/// **مرز ماژول‌ها:** برای بررسی پرسشنامه فقط از قرارداد <see cref="IQuestionnaireRepository"/>
/// استفاده می‌شود.
/// </summary>
public sealed class SurveyTemplateService(
    ISurveyTemplateRepository templateRepository,
    ISurveyRepository surveyRepository,
    IQuestionnaireRepository questionnaireRepository,
    ICurrentUserService currentUserService,
    ISurveyUnitOfWork unitOfWork) : ISurveyTemplateService
{
    private readonly ISurveyTemplateRepository _templateRepository = templateRepository;
    private readonly ISurveyRepository _surveyRepository = surveyRepository;
    private readonly IQuestionnaireRepository _questionnaireRepository = questionnaireRepository;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ISurveyUnitOfWork _unitOfWork = unitOfWork;

    public async Task<PagedResult<SurveyTemplateSummaryDto>> SearchAsync(SurveyTemplateSearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _templateRepository.CountAsync(request, ct);
        var templates = await _templateRepository.SearchAsync(request, ct);

        var dtos = templates.Select(t => new SurveyTemplateSummaryDto
        {
            Id = t.Id,
            Code = t.Code,
            Status = t.Status,
            Title = t.Localizations.Pick(Language.Fa)?.Title
                ?? t.Localizations.FirstOrDefault()?.Title
                ?? t.Code,
            QuestionnaireCode = t.QuestionnaireCode,
            IsAnonymous = t.IsAnonymous,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();

        return new PagedResult<SurveyTemplateSummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 100)
        };
    }

    public async Task<Result<SurveyTemplateDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(id, ct);
        if (template is null)
        {
            return Result.Failure<SurveyTemplateDto>("template_not_found", "قالب نظرسنجی یافت نشد.");
        }

        return Result.Success(ToDto(template));
    }

    public async Task<Result<SurveyTemplateDto>> CreateAsync(SaveSurveyTemplateRequest request, CancellationToken ct = default)
    {
        var questionnaire = await LoadActiveQuestionnaireAsync(request.QuestionnaireId, ct);
        if (questionnaire is null)
        {
            return Result.Failure<SurveyTemplateDto>("questionnaire_not_active", "پرسشنامه‌ی ارجاع‌شده وجود ندارد یا فعال نیست.");
        }

        var owner = await _templateRepository.FindByCodeAsync(request.Code, ct);
        if (owner is not null)
        {
            return Result.Failure<SurveyTemplateDto>("template_code_taken", "این کد قالب قبلاً استفاده شده است.");
        }

        var template = new SurveyTemplateEntity
        {
            Code = request.Code,
            Status = SurveyTemplateStatus.Active,
            QuestionnaireId = questionnaire.Id,
            QuestionnaireCode = questionnaire.Code,
            IsAnonymous = request.IsAnonymous,
            AllowEditResponse = request.AllowEditResponse,
            ShowProgressBar = request.ShowProgressBar,
            SingleResponsePerUser = request.SingleResponsePerUser,
            EstimatedMinutes = request.EstimatedMinutes
        };

        ApplyLocalizations(template, request.Localizations);

        template.RaiseDomainEvent(new SurveyTemplateCreatedEvent(template.Id, template.Code, _currentUserService.UserId));

        await _templateRepository.AddAsync(template, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(template));
    }

    public async Task<Result<SurveyTemplateDto>> UpdateAsync(Guid id, SaveSurveyTemplateRequest request, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(id, ct);
        if (template is null)
        {
            return Result.Failure<SurveyTemplateDto>("template_not_found", "قالب نظرسنجی یافت نشد.");
        }

        if (template.Status != SurveyTemplateStatus.Active)
        {
            return Result.Failure<SurveyTemplateDto>("template_archived", "قالب بایگانی‌شده قابل ویرایش نیست.");
        }

        var questionnaire = await LoadActiveQuestionnaireAsync(request.QuestionnaireId, ct);
        if (questionnaire is null)
        {
            return Result.Failure<SurveyTemplateDto>("questionnaire_not_active", "پرسشنامه‌ی ارجاع‌شده وجود ندارد یا فعال نیست.");
        }

        if (!string.Equals(template.Code, request.Code, StringComparison.Ordinal))
        {
            var owner = await _templateRepository.FindByCodeAsync(request.Code, ct);
            if (owner is not null && owner.Id != id)
            {
                return Result.Failure<SurveyTemplateDto>("template_code_taken", "این کد قالب قبلاً استفاده شده است.");
            }
        }

        template.Code = request.Code;
        template.QuestionnaireId = questionnaire.Id;
        template.QuestionnaireCode = questionnaire.Code;
        template.IsAnonymous = request.IsAnonymous;
        template.AllowEditResponse = request.AllowEditResponse;
        template.ShowProgressBar = request.ShowProgressBar;
        template.SingleResponsePerUser = request.SingleResponsePerUser;
        template.EstimatedMinutes = request.EstimatedMinutes;

        ApplyLocalizations(template, request.Localizations);

        _templateRepository.Update(template);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(template));
    }

    public async Task<Result<SurveyTemplateDto>> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(id, ct);
        if (template is null)
        {
            return Result.Failure<SurveyTemplateDto>("template_not_found", "قالب نظرسنجی یافت نشد.");
        }

        if (template.Status == SurveyTemplateStatus.Archived)
        {
            return Result.Failure<SurveyTemplateDto>("template_archived", "قالب از قبل بایگانی شده است.");
        }

        template.Archive();
        template.RaiseDomainEvent(new SurveyTemplateArchivedEvent(template.Id, template.Code, _currentUserService.UserId));

        _templateRepository.Update(template);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(template));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(id, ct);
        if (template is null)
        {
            return Result.Failure("template_not_found", "قالب نظرسنجی یافت نشد.");
        }

        // قالبی که نظرسنجی‌هایی از روی آن ساخته شده را نمی‌توان حذف کرد.
        // (TemplateId فقط یک رد تاریخچه است؛ نظرسنجی حتی پس از حذف قالب کار می‌کند،
        // اما برای جلوگیری از حذف تصادفی قالب‌های در حال استفاده این بررسی انجام می‌شود.)
        var inUse = await _surveyRepository.CountAsync(
            new SurveySearchRequest { TemplateId = template.Id, IncludeArchived = true, PageSize = 1 }, ct);
        if (inUse > 0)
        {
            return Result.Failure("template_in_use", "از این قالب نظرسنجی ساخته شده و قابل حذف نیست.");
        }

        _templateRepository.Remove(template);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // --- کمک‌کننده‌ها ----------------------------------------------------------

    private async Task<Domain.Modules.Questionnaire.Entities.Questionnaire?> LoadActiveQuestionnaireAsync(Guid questionnaireId, CancellationToken ct)
    {
        if (questionnaireId == Guid.Empty)
        {
            return null;
        }

        var questionnaire = await _questionnaireRepository.GetByIdAsync(questionnaireId, ct);
        return questionnaire is { Status: QuestionnaireStatus.Active } ? questionnaire : null;
    }

    private static void ApplyLocalizations(SurveyTemplateEntity template, IReadOnlyList<SurveyTemplateLocalizationDto> localizations)
    {
        foreach (var loc in localizations)
        {
            template.SetLocalization(
                loc.Language,
                loc.Title.Trim(),
                string.IsNullOrWhiteSpace(loc.Description) ? null : loc.Description.Trim());
        }

        var requested = localizations.Select(l => l.Language).ToHashSet();
        template.Localizations.RemoveAll(l => !requested.Contains(l.Language));
    }

    private static SurveyTemplateDto ToDto(SurveyTemplateEntity template)
    {
        var picked = template.Localizations.Pick(Language.Fa);

        return new SurveyTemplateDto
        {
            Id = template.Id,
            Code = template.Code,
            Status = template.Status,
            QuestionnaireId = template.QuestionnaireId,
            QuestionnaireCode = template.QuestionnaireCode,
            IsAnonymous = template.IsAnonymous,
            AllowEditResponse = template.AllowEditResponse,
            ShowProgressBar = template.ShowProgressBar,
            SingleResponsePerUser = template.SingleResponsePerUser,
            EstimatedMinutes = template.EstimatedMinutes,
            Title = picked?.Title ?? template.Localizations.FirstOrDefault()?.Title ?? template.Code,
            Description = picked?.Description,
            Localizations = template.Localizations.Select(l => new SurveyTemplateLocalizationDto
            {
                Language = l.Language,
                Title = l.Title,
                Description = l.Description
            }).ToList(),
            IsUsable = template.IsUsable,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }
}
