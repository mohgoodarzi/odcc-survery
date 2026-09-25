using System.Globalization;
using ODCC.Application.Abstractions;
using ODCC.Application.Languages;
using ODCC.Application.Modules.Campaign.Abstractions;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Application.Modules.Response.Abstractions;
using ODCC.Application.Modules.Response.Dtos;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Enums;
using ODCC.Domain.Modules.QuestionBank.Entities;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Questionnaire.Enums;
using ODCC.Domain.Modules.Response.Entities;
using ODCC.Domain.Modules.Response.Enums;
using ODCC.Domain.Modules.Response.Events;
using ODCC.Domain.Modules.Survey.Enums;
using ResponseSessionEntity = ODCC.Domain.Modules.Response.Entities.ResponseSession;

namespace ODCC.Infrastructure.Modules.Response.Services;

/// <summary>
/// پیاده‌سازی سرویس مدیریت پاسخ‌ها.
///
/// **چرخه‌ی عمر نشست:** «شروع ← ذخیره‌ی جزئی ← ارسال» روی ماشین وضعیت سمت سرور
/// است. فقط یک نشست «در حال تکمیل» به ازای هر کاربر و نظرسنجی می‌تواند باشد؛
/// شروع دوباره نشستِ در حال تکمیلِ قبلی را از سر می‌گیرد.
///
/// **ناشناس بودن:** وقتی نظرسنجی ناشناس است، هیچ شناسه‌ی پاسخ‌گو (کاربر، کارمند،
/// نام) در پایگاه داده‌ی پاسخ‌ها ذخیره نمی‌شود؛ پیوند به‌صورت عمدی قطع می‌شود.
/// شناسه‌ی توزیع (دعوت‌نامه) فقط به‌صورت گذرا در <see cref="ResponseSubmittedEvent"/>
/// منتقل می‌شود تا ماژول کمپین دعوت‌نامه را «پاسخ‌داده» علامت بزند.
///
/// **پاسخ یگانه و ویرایش پاسخ:** اگر نظرسنجی <c>SingleResponsePerUser</c> باشد،
/// هر کاربر فقط یک نشست ارسال‌شده دارد. اگر <c>AllowEditResponse</c> هم فعال باشد،
/// <see cref="StartSessionAsync"/> نشست ارسال‌شده را برای ویرایش باز می‌کند؛ در غیر
/// این صورت شروع مجدد رد می‌شود.
///
/// **اعتبارسنجی:** پاسخ‌ها نسبت به ساختار پرسشنامه‌ی چسبیده‌ی نظرسنجی بررسی
/// می‌شوند: شکل پاسخ باید با نوع سؤال همخوانی داشته باشد، گزینه‌های انتخابی باید
/// از گزینه‌های مجاز آن سؤال باشند و سؤال‌های اجباری باید در زمان ارسال پاسخ
/// داشته باشند (مگر اینکه با قانون انشعاب دور زده شده باشند).
///
/// **مرزهای ماژول‌ها:** این سرویس برای خواندن ساختار پرسشنامه فقط از قرارداد
/// <see cref="IQuestionnaireService"/> و برای خواندن گزینه‌ها فقط از قرارداد
/// <see cref="IQuestionRepository"/> استفاده می‌کند — هرگز از DbContext آن ماژول‌ها.
/// </summary>
public sealed class ResponseService(
    IResponseRepository responseRepository,
    ISurveyRepository surveyRepository,
    IQuestionnaireService questionnaireService,
    IQuestionRepository questionRepository,
    ICampaignRepository campaignRepository,
    IDistributionRepository distributionRepository,
    IEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IResponseUnitOfWork unitOfWork) : IResponseService
{
    private readonly IResponseRepository _responseRepository = responseRepository;
    private readonly ISurveyRepository _surveyRepository = surveyRepository;
    private readonly IQuestionnaireService _questionnaireService = questionnaireService;
    private readonly IQuestionRepository _questionRepository = questionRepository;
    private readonly ICampaignRepository _campaignRepository = campaignRepository;
    private readonly IDistributionRepository _distributionRepository = distributionRepository;
    private readonly IEmployeeRepository _employeeRepository = employeeRepository;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IResponseUnitOfWork _unitOfWork = unitOfWork;

    // --- پاسخ‌گو ---------------------------------------------------------------

    public async Task<Result<RespondentSurveyContextDto>> GetRespondentContextAsync(Guid surveyId, CancellationToken ct = default)
    {
        var survey = await LoadRespondableSurveyAsync(surveyId, ct);
        if (survey is null)
        {
            return Result.Failure<RespondentSurveyContextDto>("survey_not_respondable", "نظرسنجی برای پاسخ‌گویی در دسترس نیست.");
        }

        var structure = await LoadStructureAsync(survey, ct);
        if (structure is null)
        {
            return Result.Failure<RespondentSurveyContextDto>("questionnaire_not_available", "ساختار پرسشنامه‌ی این نظرسنجی در دسترس نیست.");
        }

        var picked = survey.Localizations.Pick(Language.Fa);

        ResponseSessionDto? sessionDto = null;

        if (!survey.IsAnonymous && _currentUserService.UserId is { } userId)
        {
            var existing = await GetRespondentSessionAsync(survey.Id, userId, ct);
            sessionDto = existing is not null ? ToDto(existing) : null;
        }

        return Result.Success(new RespondentSurveyContextDto
        {
            SurveyId = survey.Id,
            SurveyCode = survey.Code,
            Title = picked?.Title ?? survey.Localizations.FirstOrDefault()?.Title ?? survey.Code,
            Description = picked?.Description,
            WelcomeMessage = picked?.WelcomeMessage,
            ThankYouMessage = picked?.ThankYouMessage,
            EstimatedMinutes = survey.EstimatedMinutes,
            ShowProgressBar = survey.ShowProgressBar,
            IsAnonymous = survey.IsAnonymous,
            AllowEditResponse = survey.AllowEditResponse,
            EndDate = survey.EndDate,
            Sections = structure.Questionnaire.Sections
                .OrderBy(section => section.DisplayOrder)
                .Select(section => new RespondentSectionDto
                {
                    Id = section.Id,
                    Title = section.Title,
                    DisplayOrder = section.DisplayOrder,
                    IsOptional = section.IsOptional,
                    Items = section.Items
                        .OrderBy(item => item.DisplayOrder)
                        .Select(item => MapRespondentItem(item, structure))
                        .ToList()
                })
                .ToList(),
            Session = sessionDto
        });
    }

    public async Task<Result<ResponseSessionDto>> StartSessionAsync(StartSessionRequest request, CancellationToken ct = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<ResponseSessionDto>("authentication_required", "برای پاسخ‌گویی باید وارد سامانه شوید.");
        }

        var userId = _currentUserService.UserId.Value;

        var survey = await LoadRespondableSurveyAsync(request.SurveyId, ct);
        if (survey is null)
        {
            return Result.Failure<ResponseSessionDto>("survey_not_respondable", "نظرسنجی برای پاسخ‌گویی در دسترس نیست.");
        }

        // --- اعمال دعوت‌نامه (در صورت ورود از طریق کمپین) ------------------------
        // در نظرسنجی‌های غیرناشناس، دعوت‌نامه باید متعلق به این کاربر و این
        // نظرسنجی باشد تا جعل آن ممکن نباشد. در نظرسنجی‌های ناشناس هویتی برای
        // تطبیق وجود ندارد؛ فقط بررسی می‌شود که دعوت‌نامه به این نظرسنجی تعلق
        // داشته باشد (خودِ دعوت‌نامه توکن قابلیت است).
        DistributionContext? distribution = null;

        if (request.DistributionId is { } distributionId)
        {
            distribution = survey.IsAnonymous
                ? await LoadAnonymousDistributionAsync(distributionId, survey.Id, ct)
                : await LoadVerifiedDistributionAsync(distributionId, survey.Id, userId, ct);

            if (distribution is null)
            {
                return Result.Failure<ResponseSessionDto>("distribution_not_valid", "دعوت‌نامه معتبر نیست.");
            }
        }
        else if (request.CampaignId is { } campaignId)
        {
            // فقط کمپین دعوت‌کننده‌ی همین نظرسنجی پذیرفته می‌شود.
            var campaign = await _campaignRepository.GetByIdAsync(campaignId, ct);
            if (campaign is null || campaign.SurveyId != survey.Id)
            {
                return Result.Failure<ResponseSessionDto>("campaign_not_valid", "کمپین دعوت‌کننده معتبر نیست.");
            }

            distribution = await FindDistributionForUserAsync(campaign, userId, ct);
        }

        var source = ResolveSource(request.Source, distribution?.Channel);

        // --- سیاست پاسخ یگانه ---------------------------------------------------
        var submitted = await _responseRepository.FindSubmittedAsync(survey.Id, userId, ct);

        if (submitted is not null)
        {
            // ویرایش پاسخ ارسال‌شده فقط با اجازه‌ی نظرسنجی مجاز است.
            if (!survey.AllowEditResponse)
            {
                return Result.Failure<ResponseSessionDto>("response_already_submitted", "شما قبلاً به این نظرسنجی پاسخ داده‌اید و ویرایش آن مجاز نیست.");
            }

            submitted.Reopen();
            submitted.Source = source;
            submitted.ResponseLanguage = request.ResponseLanguage;
            submitted.Touch();

            submitted.RaiseDomainEvent(new ResponseStartedEvent(
                submitted.Id, survey.Id, survey.Code, submitted.CampaignId, distribution?.Id, survey.IsAnonymous, userId));

            _responseRepository.Update(submitted);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(ToDto(submitted));
        }

        // --- از سرگیری نشست نیمه‌تمام -------------------------------------------
        var inProgress = await _responseRepository.FindInProgressAsync(survey.Id, userId, ct);
        if (inProgress is not null)
        {
            inProgress.Source = source;
            inProgress.ResponseLanguage = request.ResponseLanguage;
            inProgress.Touch();

            _responseRepository.Update(inProgress);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(ToDto(inProgress));
        }

        // --- ساخت نشست جدید -----------------------------------------------------
        // برای نظرسنجی‌های ناشناس هیچ شناسه‌ای از پاسخ‌گو ذخیره نمی‌شود.
        var isAnonymous = survey.IsAnonymous;
        string? displayName = null;
        Guid? employeeId = null;

        if (!isAnonymous)
        {
            displayName = _currentUserService.DisplayName ?? _currentUserService.UserName;
            employeeId = await GetEmployeeIdAsync(userId, ct);
        }

        var session = new ResponseSessionEntity
        {
            SurveyId = survey.Id,
            SurveyCode = survey.Code,
            CampaignId = isAnonymous ? null : distribution?.CampaignId,
            CampaignCode = isAnonymous ? null : distribution?.CampaignCode,
            RespondentUserId = isAnonymous ? null : userId,
            RespondentEmployeeId = isAnonymous ? null : employeeId,
            RespondentDisplayName = isAnonymous ? null : displayName,
            IsAnonymous = isAnonymous,
            Status = ResponseStatus.InProgress,
            Source = source,
            ResponseLanguage = request.ResponseLanguage,
            StartedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        session.RaiseDomainEvent(new ResponseStartedEvent(
            session.Id, survey.Id, survey.Code, distribution?.CampaignId, distribution?.Id, isAnonymous, userId));

        await _responseRepository.AddAsync(session, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(session));
    }

    public async Task<Result<ResponseSessionDto>> GetMySessionAsync(Guid surveyId, CancellationToken ct = default)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure<ResponseSessionDto>("authentication_required", "برای مشاهده‌ی پاسخ‌های خود باید وارد سامانه شوید.");
        }

        var survey = await _surveyRepository.GetByIdAsync(surveyId, ct);
        if (survey is null)
        {
            return Result.Failure<ResponseSessionDto>("survey_not_found", "نظرسنجی یافت نشد.");
        }

        // نظرسنجی ناشناس هیچ نشستی قابل پیوند به کاربر ندارد.
        if (survey.IsAnonymous)
        {
            return Result.Failure<ResponseSessionDto>("survey_is_anonymous", "این نظرسنجی ناشناس است و نشست قابل بازگشت نیست.");
        }

        var session = await GetRespondentSessionAsync(survey.Id, _currentUserService.UserId.Value, ct);
        if (session is null)
        {
            return Result.Failure<ResponseSessionDto>("response_session_not_found", "شما هنوز نشست پاسخ‌گویی برای این نظرسنجی ندارید.");
        }

        return Result.Success(ToDto(session));
    }

    public async Task<Result<ResponseSessionDto>> SaveAnswersAsync(Guid sessionId, SaveAnswersRequest request, CancellationToken ct = default)
    {
        return await ApplyAnswersToSessionAsync(
            sessionId,
            request.Answers,
            validateRequired: false,
            markSubmitted: false,
            distributionId: null,
            ct);
    }

    public async Task<Result<ResponseSessionDto>> SubmitAsync(Guid sessionId, SubmitResponseRequest request, CancellationToken ct = default)
    {
        return await ApplyAnswersToSessionAsync(
            sessionId,
            request.Answers,
            validateRequired: true,
            markSubmitted: true,
            distributionId: request.DistributionId,
            ct);
    }

    public async Task<PagedResult<RespondableSurveyDto>> GetMySurveysAsync(int page = 1, int pageSize = 50, CancellationToken ct = default)
    {
        var clampedPage = Math.Max(page, 1);
        var clampedPageSize = Math.Clamp(pageSize, 1, 200);

        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return new PagedResult<RespondableSurveyDto>
            {
                Items = [],
                TotalCount = 0,
                Page = clampedPage,
                PageSize = clampedPageSize
            };
        }

        var userId = _currentUserService.UserId.Value;

        // وضعیت پاسخ کاربر به هر نظرسنجی.
        var sessions = await _responseRepository.ListByRespondentAsync(userId, ct);
        var sessionBySurvey = sessions
            .GroupBy(s => s.SurveyId)
            .ToDictionary(g => g.Key, g => g.Max(s => (int)s.Status));

        // دعوت‌نامه‌های باز این کاربر (ارسال‌شده ولی هنوز پاسخ‌داده‌نشده).
        var employee = await _employeeRepository.FindByUserIdAsync(userId, ct);
        var openDistributions = employee is null
            ? new List<DistributionContext>()
            : await FindOpenDistributionsAsync(employee.Id, ct);

        // نظرسنجی‌های پاسخ‌پذیر: فعال و منقضی‌نشده (از طریق قرارداد ماژول نظرسنجی).
        var surveys = await _surveyRepository.GetRespondableAsync(ct);

        var items = new List<RespondableSurveyDto>();

        foreach (var survey in surveys)
        {
            // نظرسنجی ناشناس فقط در صورت دعوت شدن از طریق کمپین قابل‌مشاهده است
            // (در غیر این صورت پیوندی برای تعیین دسترسی وجود ندارد).
            if (survey.IsAnonymous)
            {
                continue;
            }

            if (!survey.AcceptsResponses)
            {
                continue;
            }

            var distribution = openDistributions.FirstOrDefault(d => d.SurveyId == survey.Id);

            items.Add(new RespondableSurveyDto
            {
                SurveyId = survey.Id,
                SurveyCode = survey.Code,
                Title = survey.Title,
                Description = survey.Description,
                EstimatedMinutes = survey.EstimatedMinutes,
                IsAnonymous = survey.IsAnonymous,
                CampaignId = distribution?.CampaignId,
                CampaignCode = distribution?.CampaignCode,
                DistributionId = distribution?.Id,
                SessionStatus = sessionBySurvey.TryGetValue(survey.Id, out var status)
                    ? (ResponseStatus)status
                    : null
            });
        }

        var totalCount = items.Count;

        return new PagedResult<RespondableSurveyDto>
        {
            Items = items
                .Skip((clampedPage - 1) * clampedPageSize)
                .Take(clampedPageSize)
                .ToList(),
            TotalCount = totalCount,
            Page = clampedPage,
            PageSize = clampedPageSize
        };
    }

    // --- مدیریت / تحلیلات ------------------------------------------------------

    public async Task<PagedResult<ResponseSessionSummaryDto>> SearchAsync(ResponseSearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _responseRepository.CountAsync(request, ct);
        var sessions = await _responseRepository.SearchAsync(request, ct);

        var dtos = sessions.Select(session => new ResponseSessionSummaryDto
        {
            Id = session.Id,
            SurveyCode = session.SurveyCode,
            CampaignCode = session.CampaignCode,
            Status = session.Status,
            IsAnonymous = session.IsAnonymous,
            RespondentDisplayName = session.IsAnonymous ? null : session.RespondentDisplayName,
            StartedAt = session.StartedAt,
            SubmittedAt = session.SubmittedAt,
            AnswerCount = session.AnswerCount,
            CreatedAt = session.CreatedAt
        }).ToList();

        return new PagedResult<ResponseSessionSummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    public async Task<Result<ResponseSessionDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var session = await _responseRepository.GetByIdWithAnswersAsync(id, ct);
        if (session is null)
        {
            return Result.Failure<ResponseSessionDto>("response_session_not_found", "نشست پاسخ‌گویی یافت نشد.");
        }

        return Result.Success(ToDto(session));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var session = await _responseRepository.GetByIdAsync(id, ct);
        if (session is null)
        {
            return Result.Failure("response_session_not_found", "نشست پاسخ‌گویی یافت نشد.");
        }

        _responseRepository.Remove(session);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // --- کمک‌کننده‌ها ----------------------------------------------------------

    /// <summary>
    /// اعمال پاسخ‌ها روی یک نشست: مشترک بین ذخیره‌ی جزئی و ارسال نهایی.
    /// در حالت ارسال، سؤال‌های اجباری اعتبارسنجی می‌شوند و نشست به
    /// «ارسالشده» گذار می‌یابد.
    /// </summary>
    private async Task<Result<ResponseSessionDto>> ApplyAnswersToSessionAsync(
        Guid sessionId,
        IReadOnlyList<SaveAnswerRequest> answers,
        bool validateRequired,
        bool markSubmitted,
        Guid? distributionId,
        CancellationToken ct)
    {
        var session = await _responseRepository.GetByIdWithAnswersAsync(sessionId, ct);
        if (session is null)
        {
            return Result.Failure<ResponseSessionDto>("response_session_not_found", "نشست پاسخ‌گویی یافت نشد.");
        }

        var ownership = CheckSessionOwnership(session);
        if (ownership.IsFailure)
        {
            return Result.Failure<ResponseSessionDto>(ownership.Error);
        }

        if (!session.IsEditable)
        {
            return Result.Failure<ResponseSessionDto>("response_not_editable", "این پاسخ قبلاً ارسال شده و قابل ویرایش نیست.");
        }

        var survey = await _surveyRepository.GetByIdAsync(session.SurveyId, ct);
        if (survey is null || !survey.AcceptsResponses)
        {
            return Result.Failure<ResponseSessionDto>("survey_not_open", "پنجره‌ی پاسخ‌گویی این نظرسنجی بسته است.");
        }

        var structure = await LoadStructureAsync(survey, ct);
        if (structure is null)
        {
            return Result.Failure<ResponseSessionDto>("questionnaire_not_available", "ساختار پرسشنامه در دسترس نیست.");
        }

        var applyResult = ApplyAnswers(session, structure, answers, validateRequired);
        if (applyResult.IsFailure)
        {
            return Result.Failure<ResponseSessionDto>(applyResult.Error);
        }

        session.Touch();

        if (markSubmitted)
        {
            // شناسه‌ی دعوت‌نامه برای رویداد ارسال: در صورت داده شدن، راستی‌آزمایی
            // می‌شود تا جعل آن ممکن نباشد. در غیر این صورت از طریق کمپینِ نشست
            // (در نظرسنجی‌های غیرناشناس) پیدا می‌شود.
            //
            // **ناشناس:** هویتی برای تطبیق کارمندی وجود ندارد؛ خودِ دعوت‌نامه
            // توکن قابلیت است و فقط بررسی می‌شود که به همین نظرسنجی تعلق داشته باشد.
            DistributionContext? distribution;

            if (distributionId.HasValue)
            {
                distribution = session.IsAnonymous
                    ? await LoadAnonymousDistributionAsync(distributionId.Value, survey.Id, ct)
                    : await LoadVerifiedDistributionAsync(distributionId.Value, survey.Id, _currentUserService.UserId!.Value, ct);
            }
            else
            {
                distribution = await ResolveSessionDistributionAsync(session, ct);
            }

            session.Submit();

            session.RaiseDomainEvent(new ResponseSubmittedEvent(
                session.Id,
                survey.Id,
                survey.Code,
                session.CampaignId,
                distribution?.Id,
                session.IsAnonymous,
                session.AnswerCount,
                _currentUserService.UserId));

            _responseRepository.Update(session);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(ToDto(session));
        }

        _responseRepository.Update(session);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(session));
    }

    /// <summary>بارگذاری نظرسنجی فقط در صورتی که پاسخ می‌پذیرد.</summary>
    private async Task<Domain.Modules.Survey.Entities.Survey?> LoadRespondableSurveyAsync(Guid surveyId, CancellationToken ct)
    {
        if (surveyId == Guid.Empty)
        {
            return null;
        }

        var survey = await _surveyRepository.GetByIdAsync(surveyId, ct);
        return survey is { AcceptsResponses: true } ? survey : null;
    }

    /// <summary>
    /// بارگذاری ساختار پاسخ‌گویی: پرسشنامه (از طریق قرارداد) به‌همراه
    /// گزینه‌ها و طیف سؤال‌ها (از کتابخانه‌ی سؤالات).
    /// </summary>
    private async Task<QuestionnaireStructure?> LoadStructureAsync(Domain.Modules.Survey.Entities.Survey survey, CancellationToken ct)
    {
        var questionnaireResult = await _questionnaireService.GetByIdAsync(survey.QuestionnaireId, ct);
        if (questionnaireResult.IsFailure || questionnaireResult.Value is null)
        {
            return null;
        }

        var questionIds = questionnaireResult.Value.Sections
            .SelectMany(s => s.Items)
            .Select(i => i.QuestionId)
            .Distinct()
            .ToList();

        var questions = questionIds.Count > 0
            ? (await _questionRepository.GetByIdsAsync(questionIds, ct)).ToDictionary(q => q.Id)
            : new Dictionary<Guid, Question>();

        return new QuestionnaireStructure(questionnaireResult.Value, questions);
    }

    /// <summary>
    /// نشست فعلی پاسخ‌گو برای یک نظرسنجی: نشست ارسال‌شده اولویت دارد، در غیر
    /// این صورت نشست در حال تکمیل.
    /// </summary>
    private async Task<ResponseSessionEntity?> GetRespondentSessionAsync(Guid surveyId, Guid userId, CancellationToken ct)
    {
        return await _responseRepository.FindSubmittedAsync(surveyId, userId, ct)
            ?? await _responseRepository.FindInProgressAsync(surveyId, userId, ct);
    }

    /// <summary>
    /// مالکیت نشست: کاربر جاری باید همان پاسخ‌گو باشد.
    ///
    /// **نشست‌های ناشناس:** هیچ شناسه‌ی پاسخ‌گویی ذخیره نمی‌شود، بنابراین
    /// «مالکیت» با داشتن شناسه‌ی نشست اثبات می‌شود — شناسه‌ی نشست در این حالت
    /// مانند یک توکن قابلیت (capability) است که از طریق دعوت‌نامه به پاسخ‌گو
    /// رسیده و او آن را در نشست مرورگر خود نگه می‌دارد. به همین دلیل ارجاع
    /// مستقیم به شناسه‌ی نشست کافی است و نیازی به تطبیق هویت نیست.
    /// </summary>
    private Result CheckSessionOwnership(ResponseSessionEntity session)
    {
        if (session.IsAnonymous)
        {
            return Result.Success();
        }

        if (session.RespondentUserId is null)
        {
            return Result.Failure("response_access_denied", "شما به این نشست پاسخ دسترسی ندارید.");
        }

        if (!_currentUserService.IsAuthenticated || _currentUserService.UserId is null)
        {
            return Result.Failure("authentication_required", "برای ویرایش پاسخ باید وارد سامانه شوید.");
        }

        return session.RespondentUserId.Value == _currentUserService.UserId.Value
            ? Result.Success()
            : Result.Failure("response_access_denied", "شما به این نشست پاسخ دسترسی ندارید.");
    }

    /// <summary>
    /// اعمال پاسخ‌های ارسالی روی نشست. پاسخ‌ها نسبت به ساختار پرسشنامه‌ی
    /// چسبیده‌ی نظرسنجی اعتبارسنجی می‌شوند.
    /// </summary>
    private static Result ApplyAnswers(
        ResponseSessionEntity session,
        QuestionnaireStructure structure,
        IReadOnlyList<SaveAnswerRequest> answers,
        bool validateRequired)
    {
        var items = structure.Items;
        var answeredItemIds = new HashSet<Guid>();

        foreach (var answerRequest in answers)
        {
            if (answerRequest.QuestionnaireItemId == Guid.Empty)
            {
                continue;
            }

            if (!items.TryGetValue(answerRequest.QuestionnaireItemId, out var item))
            {
                return Result.Failure("answer_item_unknown", "یکی از پاسخ‌ها به آیتمی تعلق دارد که در این پرسشنامه وجود ندارد.");
            }

            answeredItemIds.Add(item.Id);

            var shapeError = ValidateAnswerShape(item, answerRequest, structure);
            if (shapeError is not null)
            {
                return Result.Failure(shapeError.Value.code, shapeError.Value.message);
            }

            var answer = session.GetOrCreateAnswer(
                item.Id,
                item.QuestionId,
                item.QuestionCode,
                item.QuestionType,
                item.DisplayOrder);

            ApplyAnswerValue(answer, item, answerRequest, structure);
        }

        if (validateRequired)
        {
            // آیتم مقصدِ یک قانون انشعابِ برقرارشده ملزم به پاسخ نیست.
            var skippedByBranch = ComputeBranchSkippedItems(structure, answers);

            var missing = structure.Questionnaire.Sections
                .SelectMany(s => s.Items)
                .Where(i => i.IsRequired && !answeredItemIds.Contains(i.Id) && !skippedByBranch.Contains(i.Id))
                .Select(i => i.QuestionCode)
                .ToList();

            if (missing.Count > 0)
            {
                return Result.Failure(
                    "required_questions_not_answered",
                    $"پاسخ به این سؤال‌های اجباری ناقص است: {string.Join("، ", missing)}");
            }
        }

        return Result.Success();
    }

    /// <summary>بررسی همخوانی شکل پاسخ با نوع سؤال و مجاز بودن گزینه‌ها.</summary>
    private static (string code, string message)? ValidateAnswerShape(
        QuestionnaireItemDto item,
        SaveAnswerRequest answer,
        QuestionnaireStructure structure)
    {
        var hasText = !string.IsNullOrWhiteSpace(answer.TextValue);
        var hasNumber = answer.NumericValue.HasValue;
        var hasOptions = answer.SelectedOptionIds is { Count: > 0 };

        // پاسخ خالی مجاز است (سؤال رها شده)؛ اعتبارسنجی اجباری بودن جدا انجام می‌شود.
        if (!hasText && !hasNumber && !hasOptions)
        {
            return null;
        }

        var shapes = (hasText ? 1 : 0) + (hasNumber ? 1 : 0) + (hasOptions ? 1 : 0);
        if (shapes > 1)
        {
            return ("answer_shape_invalid", "هر پاسخ فقط می‌تواند یکی از اشکال متن، عدد یا گزینه را داشته باشد.");
        }

        if (item.QuestionType.HasOptions())
        {
            if (!hasOptions)
            {
                return ("answer_shape_invalid", "پاسخ به این سؤال باید گزینه‌ی انتخاب‌شده داشته باشد.");
            }

            if (!structure.TryGetOptions(item.QuestionId, out var allowed) || allowed.Count == 0)
            {
                return ("answer_options_unavailable", "گزینه‌های این سؤال در دسترس نیست.");
            }

            var allowedIds = allowed.Select(o => o.Id).ToHashSet();

            // چندانتخابی می‌تواند چند گزینه داشته باشد؛ تک‌انتخابی فقط یکی.
            if (item.QuestionType == QuestionType.SingleChoice && answer.SelectedOptionIds!.Count > 1)
            {
                return ("answer_too_many_options", "این سؤال تک‌انتخابی است و فقط یک گزینه می‌پذیرد.");
            }

            if (answer.SelectedOptionIds!.Any(id => !allowedIds.Contains(id)))
            {
                return ("answer_option_not_valid", "یکی از گزینه‌های انتخاب‌شده به این سؤال تعلق ندارد.");
            }

            return null;
        }

        if (item.QuestionType == QuestionType.Rating)
        {
            if (!hasNumber)
            {
                return ("answer_shape_invalid", "پاسخ به سؤال امتیازدهی باید عدد باشد.");
            }

            var scaleMax = structure.GetScaleMax(item.QuestionId);

            if (scaleMax > 0)
            {
                var value = answer.NumericValue!.Value;

                if (value < 1 || value > scaleMax)
                {
                    return ("answer_out_of_scale", $"امتیاز باید بین ۱ و {scaleMax} باشد.");
                }
            }

            return null;
        }

        if (item.QuestionType == QuestionType.Number)
        {
            return hasNumber ? null : ("answer_shape_invalid", "پاسخ به این سؤال باید عدد باشد.");
        }

        if (item.QuestionType == QuestionType.YesNo)
        {
            if (!hasText)
            {
                return ("answer_shape_invalid", "پاسخ به این سؤال باید بله یا خیر باشد.");
            }

            var text = answer.TextValue!.Trim();

            return text is "yes" or "no"
                ? null
                : ("answer_shape_invalid", "پاسخ بله/خیر نامعتبر است.");
        }

        // سؤال‌های متنی.
        return hasText ? null : ("answer_shape_invalid", "پاسخ به این سؤال باید متن باشد.");
    }

    /// <summary>نوشتن مقدار پاسخ روی موجودیت دامنه (با پاک کردن اشکال دیگر).</summary>
    private static void ApplyAnswerValue(
        ResponseAnswer answer,
        QuestionnaireItemDto item,
        SaveAnswerRequest answerRequest,
        QuestionnaireStructure structure)
    {
        if (item.QuestionType.HasOptions()
            && answerRequest.SelectedOptionIds is { Count: > 0 }
            && structure.TryGetOptions(item.QuestionId, out var allowed))
        {
            var byId = allowed.ToDictionary(o => o.Id);
            var selected = answerRequest.SelectedOptionIds
                .Where(byId.ContainsKey)
                .Select(id => (id, byId[id].Code, byId[id].DisplayOrder))
                .ToList();

            answer.SetSelections(selected);
            return;
        }

        if (item.QuestionType == QuestionType.YesNo)
        {
            answer.SetYesNo(answerRequest.TextValue switch
            {
                "yes" => true,
                "no" => false,
                _ => null
            });
            return;
        }

        if (item.QuestionType is QuestionType.Rating or QuestionType.Number)
        {
            answer.SetNumber(answerRequest.NumericValue);
            return;
        }

        answer.SetText(answerRequest.TextValue);
    }

    /// <summary>
    /// آیتم‌هایی که به‌خاطر قوانین انشعابِ برقرارشده نباید پاسخ داده شوند.
    /// ارزیابی سمت سرور از روی پاسخ‌های همان درخواست انجام می‌شود تا
    /// کلاینت نتواند سؤال‌های اجباری را دور بزند.
    /// </summary>
    private static HashSet<Guid> ComputeBranchSkippedItems(
        QuestionnaireStructure structure,
        IReadOnlyList<SaveAnswerRequest> answers)
    {
        var answerByItem = answers
            .Where(a => a.QuestionnaireItemId != Guid.Empty)
            .ToDictionary(a => a.QuestionnaireItemId);

        var skipped = new HashSet<Guid>();

        foreach (var item in structure.Questionnaire.Sections.SelectMany(s => s.Items))
        {
            if (!answerByItem.TryGetValue(item.Id, out var sourceAnswer))
            {
                continue;
            }

            foreach (var rule in item.BranchingRules)
            {
                if (IsBranchActive(rule.Condition, rule.ExpectedValue, item.QuestionType, sourceAnswer))
                {
                    skipped.Add(rule.TargetItemId);
                }
            }
        }

        return skipped;
    }

    /// <summary>آیا شرط انشعاب نسبت به پاسخ داده‌شده برقرار است؟</summary>
    private static bool IsBranchActive(BranchingCondition condition, string expected, QuestionType questionType, SaveAnswerRequest answer)
    {
        var comparison = StringComparison.Ordinal;

        return (questionType, condition) switch
        {
            // سؤال‌های گزینه‌ای: مقایسه روی کد گزینه.
            (QuestionType.SingleChoice, BranchingCondition.Equals) =>
                answer.SelectedOptionIds is { Count: 1 } list && list[0].ToString().Equals(expected, comparison),
            (QuestionType.SingleChoice, BranchingCondition.NotEquals) =>
                answer.SelectedOptionIds is not { Count: > 0 } || !answer.SelectedOptionIds[0].ToString().Equals(expected, comparison),
            (QuestionType.MultipleChoice, BranchingCondition.Contains) =>
                answer.SelectedOptionIds is { Count: > 0 } list && list.Any(id => id.ToString().Equals(expected, comparison)),
            (QuestionType.MultipleChoice, BranchingCondition.Equals) =>
                answer.SelectedOptionIds is { Count: 1 } list && list[0].ToString().Equals(expected, comparison),

            // سؤال‌های عددی/امتیازدهی.
            (QuestionType.Rating or QuestionType.Number, BranchingCondition.Equals) =>
                answer.NumericValue.HasValue && answer.NumericValue.Value == decimal.Parse(expected, CultureInfo.InvariantCulture),
            (QuestionType.Rating or QuestionType.Number, BranchingCondition.NotEquals) =>
                !answer.NumericValue.HasValue || answer.NumericValue.Value != decimal.Parse(expected, CultureInfo.InvariantCulture),
            (QuestionType.Rating or QuestionType.Number, BranchingCondition.GreaterThan) =>
                answer.NumericValue.HasValue && answer.NumericValue.Value > decimal.Parse(expected, CultureInfo.InvariantCulture),
            (QuestionType.Rating or QuestionType.Number, BranchingCondition.LessThan) =>
                answer.NumericValue.HasValue && answer.NumericValue.Value < decimal.Parse(expected, CultureInfo.InvariantCulture),

            // سؤال‌های متنی و بله/خیر.
            (_, BranchingCondition.Equals) =>
                !string.IsNullOrWhiteSpace(answer.TextValue) && answer.TextValue.Trim().Equals(expected, comparison),
            (_, BranchingCondition.NotEquals) =>
                string.IsNullOrWhiteSpace(answer.TextValue) || !answer.TextValue.Trim().Equals(expected, comparison),
            _ => false
        };
    }

    /// <summary>تبدیل یک آیتم پرسشنامه به DTO پاسخ‌گو به‌همراه گزینه‌ها.</summary>
    private static RespondentItemDto MapRespondentItem(QuestionnaireItemDto item, QuestionnaireStructure structure)
    {
        var options = item.QuestionType.HasOptions() && structure.TryGetOptions(item.QuestionId, out var itemOptions)
            ? itemOptions
            : [];

        return new RespondentItemDto
        {
            Id = item.Id,
            SectionId = item.SectionId,
            QuestionId = item.QuestionId,
            QuestionCode = item.QuestionCode,
            QuestionType = item.QuestionType,
            DisplayOrder = item.DisplayOrder,
            IsRequired = item.IsRequired,
            Text = string.IsNullOrWhiteSpace(item.TitleOverride) ? item.QuestionText : item.TitleOverride,
            ScaleMax = structure.GetScaleMax(item.QuestionId),
            Options = options
                .Select(o => new RespondentOptionDto
                {
                    Id = o.Id,
                    Code = o.Code,
                    Text = o.Text,
                    DisplayOrder = o.DisplayOrder
                })
                .ToList(),
            BranchingRules = item.BranchingRules
                .Select(rule => new RespondentBranchingRuleDto
                {
                    TargetItemId = rule.TargetItemId,
                    Condition = rule.Condition,
                    ExpectedValue = rule.ExpectedValue
                })
                .ToList()
        };
    }

    /// <summary>شناسه‌ی کارمند متناظر با کاربر جاری (در صورت وجود).</summary>
    private async Task<Guid?> GetEmployeeIdAsync(Guid userId, CancellationToken ct)
    {
        var employee = await _employeeRepository.FindByUserIdAsync(userId, ct);
        return employee?.Id;
    }

    /// <summary>
    /// بارگذاری و راستی‌آزمایی دعوت‌نامه: باید متعلق به این کاربر و این نظرسنجی
    /// باشد تا جعل آن ممکن نباشد.
    /// </summary>
    private async Task<DistributionContext?> LoadVerifiedDistributionAsync(Guid distributionId, Guid surveyId, Guid userId, CancellationToken ct)
    {
        var employee = await _employeeRepository.FindByUserIdAsync(userId, ct);
        if (employee is null)
        {
            return null;
        }

        var distribution = await _distributionRepository.GetByIdAsync(distributionId, ct);
        if (distribution is null || distribution.EmployeeId != employee.Id)
        {
            return null;
        }

        var campaign = await _campaignRepository.GetByIdAsync(distribution.CampaignId, ct);
        if (campaign is null || campaign.SurveyId != surveyId)
        {
            return null;
        }

        return new DistributionContext(distribution.Id, campaign.Id, campaign.Code, campaign.Channel, surveyId);
    }

    /// <summary>
    /// بارگذاری دعوت‌نامه برای نشست ناشناس: فقط بررسی می‌شود که دعوت‌نامه به این
    /// نظرسنجی تعلق داشته باشد. در حالت ناشناس هیچ هویتی برای تطبیق وجود ندارد —
    /// خودِ دعوت‌نامه (یا شناسه‌ی نشست) توکن قابلیت است.
    /// </summary>
    private async Task<DistributionContext?> LoadAnonymousDistributionAsync(Guid distributionId, Guid surveyId, CancellationToken ct)
    {
        var distribution = await _distributionRepository.GetByIdAsync(distributionId, ct);
        if (distribution is null)
        {
            return null;
        }

        var campaign = await _campaignRepository.GetByIdAsync(distribution.CampaignId, ct);
        if (campaign is null || campaign.SurveyId != surveyId)
        {
            return null;
        }

        return new DistributionContext(distribution.Id, campaign.Id, campaign.Code, campaign.Channel, surveyId);
    }

    /// <summary>یافتن دعوت‌نامه‌ی این کاربر برای یک کمپین (در صورت وجود).</summary>
    private async Task<DistributionContext?> FindDistributionForUserAsync(Domain.Modules.Campaign.Entities.Campaign campaign, Guid userId, CancellationToken ct)
    {
        var employee = await _employeeRepository.FindByUserIdAsync(userId, ct);
        if (employee is null)
        {
            return null;
        }

        var distributions = await _distributionRepository.ListByCampaignAsync(campaign.Id, ct);
        var distribution = distributions.FirstOrDefault(d => d.EmployeeId == employee.Id);

        return distribution is null
            ? null
            : new DistributionContext(distribution.Id, campaign.Id, campaign.Code, campaign.Channel, campaign.SurveyId);
    }

    /// <summary>
    /// دعوت‌نامه‌های باز (ارسال‌شده و پاسخ‌داده‌نشده) متعلق به یک کارمند.
    /// از طریق قرارداد ماژول کمپین خوانده می‌شود، نه DbContext آن.
    /// </summary>
    private async Task<List<DistributionContext>> FindOpenDistributionsAsync(Guid employeeId, CancellationToken ct)
    {
        var rows = await _distributionRepository.GetOpenByEmployeeAsync(employeeId, ct);

        return rows
            .Select(r => new DistributionContext(r.Id, r.CampaignId, r.CampaignCode, r.Channel, r.SurveyId))
            .ToList();
    }

    /// <summary>
    /// یافتن دعوت‌نامه برای رویداد ارسال، بر اساس کمپینِ ثبت‌شده روی نشست
    /// (فقط نظرسنجی‌های غیرناشناس).
    /// </summary>
    private async Task<DistributionContext?> ResolveSessionDistributionAsync(ResponseSessionEntity session, CancellationToken ct)
    {
        if (session.IsAnonymous || session.CampaignId is not { } campaignId || session.RespondentUserId is not { } userId)
        {
            return null;
        }

        var campaign = await _campaignRepository.GetByIdAsync(campaignId, ct);
        if (campaign is null)
        {
            return null;
        }

        return await FindDistributionForUserAsync(campaign, userId, ct);
    }
    /// <summary>
    /// تعیین کانال دسترسی: اگر پاسخ‌گو از طریق دعوت‌نامه آمده، کانال از کمپین
    /// گرفته می‌شود؛ در غیر این صورت مقدار درخواست معتبر است.
    /// </summary>
    private static ResponseSource ResolveSource(ResponseSource requested, DistributionChannel? campaignChannel) =>
        campaignChannel switch
        {
            DistributionChannel.Email => ResponseSource.CampaignEmail,
            DistributionChannel.Sms => ResponseSource.CampaignSms,
            DistributionChannel.InApp => ResponseSource.CampaignInApp,
            _ => requested
        };

    /// <summary>تبدیل تجمع نشست به DTO.</summary>
    private static ResponseSessionDto ToDto(ResponseSessionEntity session)
    {
        return new ResponseSessionDto
        {
            Id = session.Id,
            SurveyId = session.SurveyId,
            SurveyCode = session.SurveyCode,
            CampaignId = session.CampaignId,
            CampaignCode = session.CampaignCode,
            Status = session.Status,
            Source = session.Source,
            IsAnonymous = session.IsAnonymous,
            RespondentDisplayName = session.IsAnonymous ? null : session.RespondentDisplayName,
            StartedAt = session.StartedAt,
            SubmittedAt = session.SubmittedAt,
            LastActivityAt = session.LastActivityAt,
            AnswerCount = session.AnswerCount,
            IsEditable = session.IsEditable,
            Answers = session.Answers
                .OrderBy(a => a.DisplayOrder)
                .Select(answer => new ResponseAnswerDto
                {
                    Id = answer.Id,
                    QuestionnaireItemId = answer.QuestionnaireItemId,
                    QuestionId = answer.QuestionId,
                    QuestionCode = answer.QuestionCode,
                    QuestionType = answer.QuestionType,
                    DisplayOrder = answer.DisplayOrder,
                    TextValue = answer.TextValue,
                    NumericValue = answer.NumericValue,
                    Selections = answer.Selections
                        .OrderBy(s => s.DisplayOrder)
                        .Select(selection => new ResponseAnswerSelectionDto
                        {
                            OptionId = selection.OptionId,
                            OptionCode = selection.OptionCode,
                            DisplayOrder = selection.DisplayOrder
                        })
                        .ToList(),
                    DisplayText = answer.ToDisplayString()
                })
                .ToList()
        };
    }

    /// <summary>
    /// اطلاعات دعوت‌نامه‌ی راستی‌آزمایی‌شده (بدون نشست دادن به موجودیت کمپین).
    /// </summary>
    private sealed record DistributionContext(
        Guid Id,
        Guid CampaignId,
        string? CampaignCode,
        DistributionChannel? Channel,
        Guid SurveyId);

    /// <summary>
    /// ساختار پرسشنامه به‌همراه سؤال‌های کتابخانه (گزینه‌ها و طیف امتیازدهی).
    /// این کلاس مرز خواندن جزئیات سؤال‌ها را در یک نقطه نگه می‌دارد.
    /// </summary>
    private sealed class QuestionnaireStructure(QuestionnaireDto questionnaire, IReadOnlyDictionary<Guid, Question> questions)
    {
        public QuestionnaireDto Questionnaire { get; } = questionnaire;

        public Dictionary<Guid, QuestionnaireItemDto> Items { get; } = questionnaire.Sections
            .SelectMany(s => s.Items)
            .ToDictionary(i => i.Id);

        /// <summary>گزینه‌های یک سؤال (مرتب‌شده بر اساس ترتیب نمایش).</summary>
        public bool TryGetOptions(Guid questionId, out IReadOnlyList<QuestionOptionDto> options)
        {
            if (questions.TryGetValue(questionId, out var question))
            {
                options = question.Options
                    .OrderBy(o => o.DisplayOrder)
                    .Select(o => new QuestionOptionDto
                    {
                        Id = o.Id,
                        Code = o.Code,
                        DisplayOrder = o.DisplayOrder,
                        Text = o.Localizations.Pick(Language.Fa)?.Text
                            ?? o.Localizations.FirstOrDefault()?.Text
                            ?? o.Code
                    })
                    .ToList();

                return true;
            }

            options = [];
            return false;
        }

        /// <summary>حداکثر طیف یک سؤال امتیازدهی.</summary>
        public int GetScaleMax(Guid questionId) =>
            questions.TryGetValue(questionId, out var question) ? question.ScaleMax : 0;
    }
}
