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
using SurveyEntity = ODCC.Domain.Modules.Survey.Entities.Survey;
using SurveyTemplateEntity = ODCC.Domain.Modules.Survey.Entities.SurveyTemplate;
using ODCC.Infrastructure.Modules.Survey.Persistence;

namespace ODCC.Infrastructure.Modules.Survey.Services;

/// <summary>
/// پیاده‌سازی سرویس نظرسنجی‌ها.
///
/// **چسبیدن به نسخه‌ی پرسشنامه:** در زمان انتشار نظرسنجی، نسخه‌ی فعلی پرسشنامه
/// ثبت می‌شود تا ساختار پاسخ‌گویی در طول عمر نظرسنجی ثابت بماند. در حالت پیش‌نویس
/// (که هنوز پاسخی ثبت نشده) نسخه به‌روزرسانی می‌شود، ولی پس از انتشار دیگر تغییر
/// نمی‌کند.
///
/// **ماشین وضعیت:** تمام گذارها در لایه‌ی کاربرد بررسی می‌شوند و در صورت عدم
/// مجوز بودن، نتیجه‌ی خطا (بدون استثنا) برگردانده می‌شود. فقط نظرسنجی «فعال»
/// پاسخ می‌پذیرد.
///
/// **مرز ماژول‌ها:** این سرویس برای خواندن پرسشنامه فقط از قرارداد
/// <see cref="IQuestionnaireRepository"/> استفاده می‌کند، هرگز از DbContext آن ماژول.
/// </summary>
public sealed class SurveyService(
    ISurveyRepository surveyRepository,
    ISurveyTemplateRepository templateRepository,
    IQuestionnaireRepository questionnaireRepository,
    ICurrentUserService currentUserService,
    ISurveyUnitOfWork unitOfWork) : ISurveyService
{
    private readonly ISurveyRepository _surveyRepository = surveyRepository;
    private readonly ISurveyTemplateRepository _templateRepository = templateRepository;
    private readonly IQuestionnaireRepository _questionnaireRepository = questionnaireRepository;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ISurveyUnitOfWork _unitOfWork = unitOfWork;

    public async Task<PagedResult<SurveySummaryDto>> SearchAsync(SurveySearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _surveyRepository.CountAsync(request, ct);
        var surveys = await _surveyRepository.SearchAsync(request, ct);

        var dtos = surveys.Select(s => new SurveySummaryDto
        {
            Id = s.Id,
            Code = s.Code,
            Status = s.Status,
            Title = s.Localizations.Pick(Language.Fa)?.Title
                ?? s.Localizations.FirstOrDefault()?.Title
                ?? s.Code,
            QuestionnaireCode = s.QuestionnaireCode,
            IsAnonymous = s.IsAnonymous,
            StartDate = s.StartDate,
            EndDate = s.EndDate,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt
        }).ToList();

        return new PagedResult<SurveySummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 100)
        };
    }

    public async Task<Result<SurveyDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(id, ct);
        if (survey is null)
        {
            return Result.Failure<SurveyDto>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        return Result.Success(ToDto(survey));
    }

    public async Task<Result<SurveyDto>> CreateAsync(SaveSurveyRequest request, CancellationToken ct = default)
    {
        var questionnaire = await LoadActiveQuestionnaireAsync(request.QuestionnaireId, ct);
        if (questionnaire is null)
        {
            return Result.Failure<SurveyDto>("questionnaire_not_active", "پرسشنامه‌ی ارجاع‌شده وجود ندارد یا فعال نیست.");
        }

        var codeError = await CheckCodeAsync(request.Code, null, ct);
        if (codeError is not null)
        {
            return Result.Failure<SurveyDto>(codeError.Value.Code, codeError.Value.Message);
        }

        var survey = new SurveyEntity
        {
            Code = request.Code,
            Status = SurveyStatus.Draft,
            QuestionnaireId = questionnaire.Id,
            QuestionnaireVersion = questionnaire.Version,
            QuestionnaireCode = questionnaire.Code,
            IsAnonymous = request.IsAnonymous,
            AllowEditResponse = request.AllowEditResponse,
            ShowProgressBar = request.ShowProgressBar,
            SingleResponsePerUser = request.SingleResponsePerUser,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            EstimatedMinutes = request.EstimatedMinutes
        };

        ApplyLocalizations(survey, request.Localizations);

        survey.RaiseDomainEvent(new SurveyCreatedEvent(survey.Id, survey.Code, _currentUserService.UserId));

        await _surveyRepository.AddAsync(survey, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(survey));
    }

    public async Task<Result<SurveyDto>> UpdateAsync(Guid id, SaveSurveyRequest request, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(id, ct);
        if (survey is null)
        {
            return Result.Failure<SurveyDto>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        // نظرسنجی منتشرشده قابل ویرایش نیست: پاسخ‌های احتمالی به ساختار فعلی وابسته‌اند.
        if (survey.Status != SurveyStatus.Draft)
        {
            return Result.Failure<SurveyDto>("survey_is_not_draft", "تنها نظرسنجی‌های پیش‌نویس قابل ویرایش هستند.");
        }

        var questionnaire = await LoadActiveQuestionnaireAsync(request.QuestionnaireId, ct);
        if (questionnaire is null)
        {
            return Result.Failure<SurveyDto>("questionnaire_not_active", "پرسشنامه‌ی ارجاع‌شده وجود ندارد یا فعال نیست.");
        }

        if (!string.Equals(survey.Code, request.Code, StringComparison.Ordinal))
        {
            var codeError = await CheckCodeAsync(request.Code, id, ct);
            if (codeError is not null)
            {
                return Result.Failure<SurveyDto>(codeError.Value.Code, codeError.Value.Message);
            }
        }

        survey.Code = request.Code;
        survey.QuestionnaireId = questionnaire.Id;
        survey.QuestionnaireVersion = questionnaire.Version;
        survey.QuestionnaireCode = questionnaire.Code;
        survey.IsAnonymous = request.IsAnonymous;
        survey.AllowEditResponse = request.AllowEditResponse;
        survey.ShowProgressBar = request.ShowProgressBar;
        survey.SingleResponsePerUser = request.SingleResponsePerUser;
        survey.StartDate = request.StartDate;
        survey.EndDate = request.EndDate;
        survey.EstimatedMinutes = request.EstimatedMinutes;

        ApplyLocalizations(survey, request.Localizations);

        survey.RaiseDomainEvent(new SurveyUpdatedEvent(survey.Id, survey.Code, _currentUserService.UserId));

        _surveyRepository.Update(survey);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(survey));
    }

    public async Task<Result<SurveyDto>> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(id, ct);
        if (survey is null)
        {
            return Result.Failure<SurveyDto>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        if (survey.Status != SurveyStatus.Draft)
        {
            return Result.Failure<SurveyDto>("survey_is_not_draft", "فقط نظرسنجی‌های پیش‌نویس قابل انتشار هستند.");
        }

        if (!survey.IsPublishable)
        {
            return Result.Failure<SurveyDto>("survey_not_publishable", "نظرسنجی برای انتشار باید حداقل یک عنوان داشته باشد.");
        }

        var questionnaire = await LoadActiveQuestionnaireAsync(survey.QuestionnaireId, ct);
        if (questionnaire is null)
        {
            return Result.Failure<SurveyDto>("questionnaire_not_active", "پرسشنامه‌ی این نظرسنجی دیگر فعال نیست.");
        }

        // در زمان انتشار، نسخه‌ی فعلی پرسشنامه ثابت می‌شود (ساختار پاسخ‌گویی ثابت می‌ماند).
        survey.QuestionnaireVersion = questionnaire.Version;
        survey.QuestionnaireCode = questionnaire.Code;

        survey.Publish();
        survey.RaiseDomainEvent(new SurveyPublishedEvent(survey.Id, survey.Code, survey.Status, _currentUserService.UserId));

        _surveyRepository.Update(survey);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(survey));
    }

    public async Task<Result<SurveyDto>> StartAsync(Guid id, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(id, ct);
        if (survey is null)
        {
            return Result.Failure<SurveyDto>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        if (survey.Status != SurveyStatus.Scheduled)
        {
            return Result.Failure<SurveyDto>("survey_is_not_scheduled", "فقط نظرسنجی‌های زمان‌بندی‌شده قابل شروع هستند.");
        }

        survey.Start();
        survey.RaiseDomainEvent(new SurveyStartedEvent(survey.Id, survey.Code, _currentUserService.UserId));

        _surveyRepository.Update(survey);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(survey));
    }

    public async Task<Result<SurveyDto>> PauseAsync(Guid id, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(id, ct);
        if (survey is null)
        {
            return Result.Failure<SurveyDto>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        if (survey.Status != SurveyStatus.Active)
        {
            return Result.Failure<SurveyDto>("survey_is_not_active", "فقط نظرسنجی‌های فعال قابل توقف هستند.");
        }

        survey.Pause();

        _surveyRepository.Update(survey);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(survey));
    }

    public async Task<Result<SurveyDto>> ResumeAsync(Guid id, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(id, ct);
        if (survey is null)
        {
            return Result.Failure<SurveyDto>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        if (survey.Status != SurveyStatus.Paused)
        {
            return Result.Failure<SurveyDto>("survey_is_not_paused", "فقط نظرسنجی‌های متوقف‌شده قابل از سرگیری هستند.");
        }

        survey.Resume();
        survey.RaiseDomainEvent(new SurveyStartedEvent(survey.Id, survey.Code, _currentUserService.UserId));

        _surveyRepository.Update(survey);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(survey));
    }

    public async Task<Result<SurveyDto>> CloseAsync(Guid id, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(id, ct);
        if (survey is null)
        {
            return Result.Failure<SurveyDto>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        if (survey.Status is not (SurveyStatus.Scheduled or SurveyStatus.Active or SurveyStatus.Paused))
        {
            return Result.Failure<SurveyDto>("survey_cannot_close", "نظرسنجی در وضعیت فعلی قابل بستن نیست.");
        }

        survey.Close();
        survey.RaiseDomainEvent(new SurveyClosedEvent(survey.Id, survey.Code, _currentUserService.UserId));

        _surveyRepository.Update(survey);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(survey));
    }

    public async Task<Result<SurveyDto>> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(id, ct);
        if (survey is null)
        {
            return Result.Failure<SurveyDto>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        if (survey.Status == SurveyStatus.Archived)
        {
            return Result.Failure<SurveyDto>("survey_is_archived", "نظرسنجی از قبل بایگانی شده است.");
        }

        survey.Archive();
        survey.RaiseDomainEvent(new SurveyArchivedEvent(survey.Id, survey.Code, _currentUserService.UserId));

        _surveyRepository.Update(survey);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(survey));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var survey = await _surveyRepository.GetByIdAsync(id, ct);
        if (survey is null)
        {
            return Result.Failure("survey_not_found", "نظرسنجی یافت نشد.");
        }

        // فقط پیش‌نویس‌ها حذف می‌شوند تا تاریخچه‌ی پاسخ‌ها در نظرسنجی‌های منتشرشده حفظ شود.
        if (survey.Status != SurveyStatus.Draft)
        {
            return Result.Failure("survey_not_deletable", "فقط نظرسنجی‌های پیش‌نویس قابل حذف هستند.");
        }

        _surveyRepository.Remove(survey);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<SurveyDto>> CreateFromTemplateAsync(CreateSurveyFromTemplateRequest request, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(request.TemplateId, ct);
        if (template is null)
        {
            return Result.Failure<SurveyDto>("template_not_found", "قالب نظرسنجی یافت نشد.");
        }

        if (!template.IsUsable)
        {
            return Result.Failure<SurveyDto>("template_archived", "قالب بایگانی‌شده قابل استفاده نیست.");
        }

        var questionnaire = await LoadActiveQuestionnaireAsync(template.QuestionnaireId, ct);
        if (questionnaire is null)
        {
            return Result.Failure<SurveyDto>("questionnaire_not_active", "پرسشنامه‌ی پایه‌ی این قالب دیگر فعال نیست.");
        }

        var codeError = await CheckCodeAsync(request.Code, null, ct);
        if (codeError is not null)
        {
            return Result.Failure<SurveyDto>(codeError.Value.Code, codeError.Value.Message);
        }

        var survey = new SurveyEntity
        {
            Code = request.Code,
            Status = SurveyStatus.Draft,
            QuestionnaireId = questionnaire.Id,
            QuestionnaireVersion = questionnaire.Version,
            QuestionnaireCode = questionnaire.Code,
            TemplateId = template.Id,
            IsAnonymous = template.IsAnonymous,
            AllowEditResponse = template.AllowEditResponse,
            ShowProgressBar = template.ShowProgressBar,
            SingleResponsePerUser = template.SingleResponsePerUser,
            EstimatedMinutes = template.EstimatedMinutes,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        // ترجمه‌ها: از درخواست، وگرنه از قالب کپی می‌شوند.
        var localizations = request.Localizations is { Count: > 0 }
            ? request.Localizations
            : template.Localizations.Select(l => new SurveyLocalizationDto
            {
                Language = l.Language,
                Title = l.Title,
                Description = l.Description
            }).ToList();

        ApplyLocalizations(survey, localizations);

        survey.RaiseDomainEvent(new SurveyCreatedEvent(survey.Id, survey.Code, _currentUserService.UserId));

        await _surveyRepository.AddAsync(survey, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(survey));
    }

    // --- کمک‌کننده‌ها ----------------------------------------------------------

    /// <summary>بارگذاری پرسشنامه فقط در صورت فعال بودن (مرز ماژول: از طریق قرارداد).</summary>
    private async Task<Domain.Modules.Questionnaire.Entities.Questionnaire?> LoadActiveQuestionnaireAsync(Guid questionnaireId, CancellationToken ct)
    {
        if (questionnaireId == Guid.Empty)
        {
            return null;
        }

        var questionnaire = await _questionnaireRepository.GetByIdAsync(questionnaireId, ct);
        return questionnaire is { Status: QuestionnaireStatus.Active } ? questionnaire : null;
    }

    /// <summary>بررسی یکتا بودن کد نظرسنجی. <c>null</c> در صورت نبود خطا.</summary>
    private async Task<(string Code, string Message)?> CheckCodeAsync(string code, Guid? currentId, CancellationToken ct)
    {
        var owner = await _surveyRepository.FindByCodeAsync(code, ct);
        if (owner is not null && owner.Id != currentId)
        {
            return ("survey_code_taken", "این کد نظرسنجی قبلاً استفاده شده است.");
        }

        return null;
    }

    private static void ApplyLocalizations(SurveyEntity survey, IReadOnlyList<SurveyLocalizationDto> localizations)
    {
        foreach (var loc in localizations)
        {
            survey.SetLocalization(
                loc.Language,
                loc.Title.Trim(),
                string.IsNullOrWhiteSpace(loc.Description) ? null : loc.Description.Trim(),
                string.IsNullOrWhiteSpace(loc.WelcomeMessage) ? null : loc.WelcomeMessage.Trim(),
                string.IsNullOrWhiteSpace(loc.ThankYouMessage) ? null : loc.ThankYouMessage.Trim());
        }

        var requested = localizations.Select(l => l.Language).ToHashSet();
        survey.Localizations.RemoveAll(l => !requested.Contains(l.Language));
    }

    private static SurveyDto ToDto(SurveyEntity survey)
    {
        var picked = survey.Localizations.Pick(Language.Fa);

        return new SurveyDto
        {
            Id = survey.Id,
            Code = survey.Code,
            Status = survey.Status,
            QuestionnaireId = survey.QuestionnaireId,
            QuestionnaireVersion = survey.QuestionnaireVersion,
            QuestionnaireCode = survey.QuestionnaireCode,
            TemplateId = survey.TemplateId,
            IsAnonymous = survey.IsAnonymous,
            AllowEditResponse = survey.AllowEditResponse,
            ShowProgressBar = survey.ShowProgressBar,
            SingleResponsePerUser = survey.SingleResponsePerUser,
            StartDate = survey.StartDate,
            EndDate = survey.EndDate,
            EstimatedMinutes = survey.EstimatedMinutes,
            Title = picked?.Title ?? survey.Localizations.FirstOrDefault()?.Title ?? survey.Code,
            Description = picked?.Description,
            WelcomeMessage = picked?.WelcomeMessage,
            ThankYouMessage = picked?.ThankYouMessage,
            Localizations = survey.Localizations.Select(l => new SurveyLocalizationDto
            {
                Language = l.Language,
                Title = l.Title,
                Description = l.Description,
                WelcomeMessage = l.WelcomeMessage,
                ThankYouMessage = l.ThankYouMessage
            }).ToList(),
            IsPublishable = survey.IsPublishable,
            AcceptsResponses = survey.AcceptsResponses,
            PublishedAt = survey.PublishedAt,
            ActivatedAt = survey.ActivatedAt,
            ClosedAt = survey.ClosedAt,
            ArchivedAt = survey.ArchivedAt,
            CreatedAt = survey.CreatedAt,
            UpdatedAt = survey.UpdatedAt
        };
    }
}
