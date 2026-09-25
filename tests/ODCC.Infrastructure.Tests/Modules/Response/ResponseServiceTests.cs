using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Application.Modules.Campaign.Abstractions;
using ODCC.Application.Modules.Campaign.Dtos;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Application.Modules.Response.Abstractions;
using ODCC.Application.Modules.Response.Dtos;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Enums;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Response.Enums;
using ODCC.Domain.Modules.Survey.Enums;
using ODCC.Infrastructure.Modules.Campaign.Persistence;
using ODCC.Infrastructure.Modules.Response.Persistence;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Response;

/// <summary>
/// آزمون‌های ماژول پاسخ‌ها: چرخه‌ی عمر نشست (شروع، ذخیره‌ی جزئی، ارسال)،
/// سیاست‌های ناشناس بودن و پاسخ یگانه، اعتبارسنجی پاسخ‌ها نسبت به ساختار
/// پرسشنامه و علامت‌زدن دعوت‌نامه به‌عنوان پاسخ‌داده.
///
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ
/// پایگاه‌داده‌ی واقعی را لمس نمی‌کنند.
/// </summary>
public class ResponseServiceTests
{
    /// <summary>ساخت یک سؤال گزینه‌ای با دو گزینه.</summary>
    private static async Task<QuestionDto> SeedChoiceQuestionAsync(TestEnvironment env, string code)
    {
        var service = env.Services.GetRequiredService<IQuestionService>();

        return (await service.CreateAsync(new SaveQuestionRequest
        {
            Code = code,
            Type = QuestionType.SingleChoice,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "میزان رضایت شما؟" } ],
            Options =
            [
                new SaveQuestionOptionRequest
                {
                    Code = "A", DisplayOrder = 0,
                    Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "کم" } ]
                },
                new SaveQuestionOptionRequest
                {
                    Code = "B", DisplayOrder = 1,
                    Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "زیاد" } ]
                }
            ]
        })).Value!;
    }

    /// <summary>ساخت یک سؤال متنی اجباری.</summary>
    private static async Task<QuestionDto> SeedTextQuestionAsync(TestEnvironment env, string code)
    {
        var service = env.Services.GetRequiredService<IQuestionService>();

        return (await service.CreateAsync(new SaveQuestionRequest
        {
            Code = code,
            Type = QuestionType.LongText,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "توضیحات شما؟" } ]
        })).Value!;
    }

    /// <summary>ساخت یک سؤال امتیازدهی با طیف ۵.</summary>
    private static async Task<QuestionDto> SeedRatingQuestionAsync(TestEnvironment env, string code)
    {
        var service = env.Services.GetRequiredService<IQuestionService>();

        return (await service.CreateAsync(new SaveQuestionRequest
        {
            Code = code,
            Type = QuestionType.Rating,
            ScaleMax = 5,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "امتیاز شما؟" } ]
        })).Value!;
    }

    /// <summary>ساخت و انتشار یک پرسشنامه با سؤال‌های داده‌شده.</summary>
    private static async Task<QuestionnaireDto> SeedQuestionnaireAsync(TestEnvironment env, params QuestionDto[] questions)
    {
        var service = env.Services.GetRequiredService<IQuestionnaireService>();

        var created = await service.CreateAsync(new SaveQuestionnaireRequest
        {
            Code = "QS-R-01",
            Localizations = [ new QuestionnaireLocalizationDto { Language = Language.Fa, Title = "پرسشنامه" } ],
            Sections =
            [
                new SaveSectionRequest
                {
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش اول" } ],
                    Items = questions.Select(q => new SaveItemRequest { QuestionId = q.Id, IsRequired = true }).ToList()
                }
            ]
        });

        created.IsSuccess.Should().BeTrue();
        var published = await service.PublishAsync(created.Value!.Id);
        published.IsSuccess.Should().BeTrue();
        return published.Value!;
    }

    /// <summary>ساخت یک نظرسنجی‌ی فعال روی پرسشنامه.</summary>
    private static async Task<SurveyDto> SeedActiveSurveyAsync(
        TestEnvironment env,
        Guid questionnaireId,
        string code = "SV-R-01",
        bool isAnonymous = false,
        bool allowEditResponse = true)
    {
        var service = env.Services.GetRequiredService<ISurveyService>();

        var created = await service.CreateAsync(new SaveSurveyRequest
        {
            Code = code,
            QuestionnaireId = questionnaireId,
            IsAnonymous = isAnonymous,
            AllowEditResponse = allowEditResponse,
            Localizations = [ new SurveyLocalizationDto { Language = Language.Fa, Title = "نظرسنجی نمونه" } ]
        });

        created.IsSuccess.Should().BeTrue();
        var published = await service.PublishAsync(created.Value!.Id);
        published.IsSuccess.Should().BeTrue();
        return published.Value!;
    }

    /// <summary>ساخت کارمند برای کاربر جاری (تا دعوت‌نامه قابل پیوند باشد).</summary>
    private static async Task<Guid> SeedEmployeeForCurrentUserAsync(TestEnvironment env, Guid userId)
    {
        var units = await env.SeedOrgHierarchyAsync();

        var employee = new ODCC.Domain.Modules.Organization.Entities.Employee
        {
            EmployeeCode = "EMP-R-01",
            FirstName = "پاسخ",
            LastName = "دهنده",
            UserId = userId,
            OrgUnitId = units.companyId,
            Status = ODCC.Domain.Modules.Organization.Enums.EmployeeStatus.Active,
            StartDate = new DateOnly(2020, 1, 1),
            WorkEmail = "respondent@test.local"
        };

        env.OrganizationDbContext.Employees.Add(employee);
        await env.OrganizationDbContext.SaveChangesAsync();

        return employee.Id;
    }

    /// <summary>ساخت کارمند و دعوت‌نامه ارسال‌شده برای او.</summary>
    private static async Task<(Guid employeeId, Guid campaignId, Guid distributionId)> SeedSentDistributionAsync(
        TestEnvironment env, Guid surveyId, Guid userId)
    {
        var employeeId = await SeedEmployeeForCurrentUserAsync(env, userId);

        var campaignService = env.Services.GetRequiredService<ICampaignService>();
        var campaign = (await campaignService.CreateAsync(new SaveCampaignRequest
        {
            Code = "CMP-R-01",
            SurveyId = surveyId,
            AudienceType = TargetAudienceType.Employees,
            TargetEmployeeIds = [employeeId],
            Channel = DistributionChannel.Email,
            Localizations = [ new CampaignLocalizationDto { Language = Language.Fa, Title = "کمپین پاسخ‌گویی" } ]
        })).Value!;

        var launch = await campaignService.LaunchAsync(campaign.Id);
        launch.IsSuccess.Should().BeTrue();

        // شبیه‌سازی موفقیت ارسال توسط ماژول اعلان‌ها.
        var distribution = await env.CampaignDbContext.Distributions
            .FirstAsync(d => d.CampaignId == campaign.Id);

        distribution.MarkSent();
        env.CampaignDbContext.Distributions.Update(distribution);
        await env.CampaignDbContext.SaveChangesAsync();

        return (employeeId, campaign.Id, distribution.Id);
    }

    [Fact]
    public async Task StartSession_Creates_Session_For_Active_Survey()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var result = await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ResponseStatus.InProgress);
        result.Value.SurveyCode.Should().Be(survey.Code);
        result.Value.RespondentDisplayName.Should().NotBeNull();
        result.Value.IsAnonymous.Should().BeFalse();

        var entity = await env.ResponseDbContext.Sessions
            .Include(s => s.Answers)
            .FirstAsync(s => s.Id == result.Value.Id);

        entity.RespondentUserId.Should().Be(userId);
        entity.Status.Should().Be(ResponseStatus.InProgress);
    }

    [Fact]
    public async Task StartSession_Rejects_When_Survey_Is_Not_Active()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);

        var surveyService = env.Services.GetRequiredService<ISurveyService>();
        var survey = (await surveyService.CreateAsync(new SaveSurveyRequest
        {
            Code = "SV-R-01",
            QuestionnaireId = questionnaire.Id,
            Localizations = [ new SurveyLocalizationDto { Language = Language.Fa, Title = "نظرسنجی" } ]
        })).Value!;

        // نظرسنجی هنوز پیش‌نویس است و پاسخ نمی‌پذیرد.
        var service = env.Services.GetRequiredService<IResponseService>();

        var result = await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("survey_not_respondable");
    }

    [Fact]
    public async Task StartSession_Does_Not_Store_Respondent_Identity_When_Anonymous()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id, isAnonymous: true);

        var service = env.Services.GetRequiredService<IResponseService>();

        var result = await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id });

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsAnonymous.Should().BeTrue();
        result.Value.RespondentDisplayName.Should().BeNull();

        var entity = await env.ResponseDbContext.Sessions.FirstAsync(s => s.Id == result.Value.Id);

        entity.RespondentUserId.Should().BeNull();
        entity.RespondentEmployeeId.Should().BeNull();
        entity.RespondentDisplayName.Should().BeNull();
        entity.CampaignId.Should().BeNull();
    }

    [Fact]
    public async Task StartSession_Rejects_Second_Submission_When_Editing_Not_Allowed()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id, allowEditResponse: false);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;
        await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [question.Options[0].Id] } ]
        });

        var second = await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id });

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("response_already_submitted");
    }

    [Fact]
    public async Task StartSession_Reopens_Submitted_Response_When_Editing_Allowed()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id, allowEditResponse: true);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;
        await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [question.Options[0].Id] } ]
        });

        var reopened = await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id });

        reopened.IsSuccess.Should().BeTrue();
        reopened.Value!.Id.Should().Be(session.Id);
        reopened.Value.Status.Should().Be(ResponseStatus.InProgress);
        reopened.Value.SubmittedAt.Should().BeNull();
    }

    [Fact]
    public async Task StartSession_Resumes_In_Progress_Session()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var first = await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id });
        var second = await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id });

        second.IsSuccess.Should().BeTrue();
        second.Value!.Id.Should().Be(first.Value!.Id);

        var count = await env.ResponseDbContext.Sessions.CountAsync(s => s.SurveyId == survey.Id);
        count.Should().Be(1, "نشست تکراری نباید ساخته شود");
    }

    [Fact]
    public async Task StartSession_Rejects_Forged_Distribution_For_Another_User()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        // دعوت‌نامه برای یک کارمند دیگر.
        var units = await env.SeedOrgHierarchyAsync();

        var otherEmployee = new ODCC.Domain.Modules.Organization.Entities.Employee
        {
            EmployeeCode = "EMP-R-99",
            FirstName = "دیگر",
            LastName = "کاربر",
            OrgUnitId = units.companyId,
            Status = ODCC.Domain.Modules.Organization.Enums.EmployeeStatus.Active,
            StartDate = new DateOnly(2020, 1, 1),
            WorkEmail = "other@test.local"
        };
        env.OrganizationDbContext.Employees.Add(otherEmployee);
        await env.OrganizationDbContext.SaveChangesAsync();

        var campaignService = env.Services.GetRequiredService<ICampaignService>();
        var campaign = (await campaignService.CreateAsync(new SaveCampaignRequest
        {
            Code = "CMP-R-01",
            SurveyId = survey.Id,
            AudienceType = TargetAudienceType.Employees,
            TargetEmployeeIds = [otherEmployee.Id],
            Channel = DistributionChannel.Email,
            Localizations = [ new CampaignLocalizationDto { Language = Language.Fa, Title = "کمپین" } ]
        })).Value!;
        await campaignService.LaunchAsync(campaign.Id);

        var distribution = await env.CampaignDbContext.Distributions.FirstAsync(d => d.CampaignId == campaign.Id);
        distribution.MarkSent();
        env.CampaignDbContext.Distributions.Update(distribution);
        await env.CampaignDbContext.SaveChangesAsync();

        var service = env.Services.GetRequiredService<IResponseService>();

        var result = await service.StartSessionAsync(new StartSessionRequest
        {
            SurveyId = survey.Id,
            DistributionId = distribution.Id
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("distribution_not_valid");
    }

    [Fact]
    public async Task StartSession_Links_Session_To_Verified_Distribution()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var (_, campaignId, distributionId) = await SeedSentDistributionAsync(env, survey.Id, userId);

        var service = env.Services.GetRequiredService<IResponseService>();

        var result = await service.StartSessionAsync(new StartSessionRequest
        {
            SurveyId = survey.Id,
            DistributionId = distributionId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.CampaignId.Should().Be(campaignId);
        result.Value.Source.Should().Be(ResponseSource.CampaignEmail);
    }

    [Fact]
    public async Task SaveAnswers_Persists_Partial_Answers_Without_Required_Validation()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var choice = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var text = await SeedTextQuestionAsync(env, "QB-R-02");
        var questionnaire = await SeedQuestionnaireAsync(env, choice, text);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;

        // پاسخ ناقص: فقط یکی از دو سؤال اجباری پاسخ داده شده.
        var result = await service.SaveAnswersAsync(session.Id, new SaveAnswersRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [choice.Options[1].Id] } ]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Answers.Should().ContainSingle();
        result.Value.Answers[0].Selections.Should().ContainSingle().Which.OptionCode.Should().Be("B");
        result.Value.Status.Should().Be(ResponseStatus.InProgress);
    }

    [Fact]
    public async Task Submit_Rejects_When_Required_Questions_Are_Missing()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var choice = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var text = await SeedTextQuestionAsync(env, "QB-R-02");
        var questionnaire = await SeedQuestionnaireAsync(env, choice, text);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;

        var result = await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [choice.Options[0].Id] } ]
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("required_questions_not_answered");

        // نشست نباید ارسال‌شده باشد.
        var entity = await env.ResponseDbContext.Sessions.FirstAsync(s => s.Id == session.Id);
        entity.Status.Should().Be(ResponseStatus.InProgress);
    }

    [Fact]
    public async Task Submit_Completes_Session_And_Records_Answer_Count()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var choice = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var text = await SeedTextQuestionAsync(env, "QB-R-02");
        var questionnaire = await SeedQuestionnaireAsync(env, choice, text);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;

        var result = await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers =
            [
                new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [choice.Options[0].Id] },
                new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[1].Id, TextValue = "بسیار عالی بود" }
            ]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ResponseStatus.Submitted);
        result.Value.SubmittedAt.Should().NotBeNull();
        result.Value.AnswerCount.Should().Be(2);
        result.Value.IsEditable.Should().BeFalse();

        var entity = await env.ResponseDbContext.Sessions
            .Include(s => s.Answers).ThenInclude(a => a.Selections)
            .FirstAsync(s => s.Id == session.Id);

        entity.Status.Should().Be(ResponseStatus.Submitted);
        entity.Answers.Should().HaveCount(2);
        entity.Answers.First(a => a.QuestionType == QuestionType.SingleChoice).Selections.Should().ContainSingle();
        entity.Answers.First(a => a.QuestionType == QuestionType.LongText).TextValue.Should().Be("بسیار عالی بود");
    }

    [Fact]
    public async Task Submit_Rejects_Option_Not_Belonging_To_Question()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var questionA = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionB = await SeedChoiceQuestionAsync(env, "QB-R-02");
        var questionnaire = await SeedQuestionnaireAsync(env, questionA);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;

        var result = await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            // گزینه متعلق به سؤال دیگر.
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [questionB.Options[0].Id] } ]
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("answer_option_not_valid");
    }

    [Fact]
    public async Task Submit_Rejects_Multiple_Options_For_Single_Choice()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;

        var result = await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers =
            [
                new SaveAnswerRequest
                {
                    QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id,
                    SelectedOptionIds = [question.Options[0].Id, question.Options[1].Id]
                }
            ]
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("answer_too_many_options");
    }

    [Fact]
    public async Task Submit_Rejects_Out_Of_Scale_Rating()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var rating = await SeedRatingQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, rating);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;

        var result = await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, NumericValue = 9 } ]
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("answer_out_of_scale");
    }

    [Fact]
    public async Task Submit_Rejects_Conflicting_Answer_Shapes()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var rating = await SeedRatingQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, rating);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;

        var result = await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers =
            [
                new SaveAnswerRequest
                {
                    QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id,
                    NumericValue = 3,
                    TextValue = "متن اضافه"
                }
            ]
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("answer_shape_invalid");
    }

    [Fact]
    public async Task Submit_Denies_Access_To_Session_Of_Another_User()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;

        // کاربر دیگر.
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, userName: "other");

        var result = await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [question.Options[0].Id] } ]
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("response_access_denied");
    }

    [Fact]
    public async Task Submit_Marks_Linked_Distribution_As_Responded()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var (_, _, distributionId) = await SeedSentDistributionAsync(env, survey.Id, userId);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest
        {
            SurveyId = survey.Id,
            DistributionId = distributionId
        })).Value!;

        var result = await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [question.Options[0].Id] } ]
        });

        result.IsSuccess.Should().BeTrue();

        var distribution = await env.CampaignDbContext.Distributions.FirstAsync(d => d.Id == distributionId);

        distribution.Status.Should().Be(DistributionStatus.Responded);
        distribution.RespondedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Submit_Logs_Audit_Entry()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;
        await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [question.Options[0].Id] } ]
        });

        var auditService = env.Services.GetRequiredService<IAuditService>();
        var entries = await auditService.SearchAsync(new AuditSearchRequest("response_session", null, null, null, null));

        entries.Should().Contain(e =>
            e.EntityType == "response_session" && e.Action == "submit" && e.EntityId == session.Id);
    }

    [Fact]
    public async Task Submit_Logs_Audit_Entry_Without_Respondent_For_Anonymous_Survey()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id, isAnonymous: true);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;
        await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [question.Options[0].Id] } ]
        });

        var entity = await env.ResponseDbContext.Sessions
            .Include(s => s.Answers)
            .FirstAsync(s => s.Id == session.Id);

        entity.RespondentUserId.Should().BeNull();
        entity.IsAnonymous.Should().BeTrue();
        entity.Answers.Should().HaveCount(1);

        var auditService = env.Services.GetRequiredService<IAuditService>();
        var entries = await auditService.SearchAsync(new AuditSearchRequest("response_session", null, null, null, null));

        entries.Should().Contain(e =>
            e.EntityType == "response_session" && e.Action == "submit"
            && e.Description != null && e.Description.Contains("ناشناس"));
    }

    [Fact]
    public async Task GetRespondentContext_Returns_Structure_Options_And_Session()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        // قبل از شروع نشست.
        var context = await service.GetRespondentContextAsync(survey.Id);

        context.IsSuccess.Should().BeTrue();
        context.Value!.Sections.Should().ContainSingle();
        context.Value.Sections[0].Items.Should().ContainSingle();
        context.Value.Sections[0].Items[0].Options.Should().HaveCount(2);
        context.Value.Sections[0].Items[0].Options[0].Code.Should().Be("A");
        context.Value.Sections[0].Items[0].IsRequired.Should().BeTrue();
        context.Value.Session.Should().BeNull();

        // بعد از شروع نشست.
        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;

        var after = await service.GetRespondentContextAsync(survey.Id);

        after.Value!.Session.Should().NotBeNull();
        after.Value.Session!.Id.Should().Be(session.Id);
    }

    [Fact]
    public async Task GetMySurveys_Lists_Active_Surveys_With_Session_Status()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var before = await service.GetMySurveysAsync();

        before.Items.Should().Contain(s => s.SurveyId == survey.Id);
        before.Items.First(s => s.SurveyId == survey.Id).SessionStatus.Should().BeNull();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;
        await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [question.Options[0].Id] } ]
        });

        var after = await service.GetMySurveysAsync();

        after.Items.First(s => s.SurveyId == survey.Id).SessionStatus.Should().Be(ResponseStatus.Submitted);
    }

    [Fact]
    public async Task GetMySurveys_Hides_Anonymous_Surveys_Without_Invitation()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id, isAnonymous: true);

        var service = env.Services.GetRequiredService<IResponseService>();

        var result = await service.GetMySurveysAsync();

        result.Items.Should().NotContain(s => s.SurveyId == survey.Id);
    }

    [Fact]
    public async Task Search_Filters_By_Survey_And_Status()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;
        await service.SubmitAsync(session.Id, new SubmitResponseRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [question.Options[0].Id] } ]
        });

        var submittedOnly = await service.SearchAsync(new ResponseSearchRequest { SurveyId = survey.Id, Status = ResponseStatus.Submitted });

        submittedOnly.TotalCount.Should().Be(1);
        submittedOnly.Items[0].SurveyCode.Should().Be(survey.Code);
        submittedOnly.Items[0].RespondentDisplayName.Should().NotBeNull();

        var inProgressOnly = await service.SearchAsync(new ResponseSearchRequest { SurveyId = survey.Id, Status = ResponseStatus.InProgress });

        inProgressOnly.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetById_Returns_Session_With_Answers()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;
        await service.SaveAnswersAsync(session.Id, new SaveAnswersRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [question.Options[0].Id] } ]
        });

        var result = await service.GetByIdAsync(session.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Answers.Should().ContainSingle();
        result.Value.Answers[0].DisplayText.Should().Be("A");
    }

    [Fact]
    public async Task Delete_Soft_Deletes_Session()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id);

        var service = env.Services.GetRequiredService<IResponseService>();

        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;

        var delete = await service.DeleteAsync(session.Id);
        delete.IsSuccess.Should().BeTrue();

        var entity = await env.ResponseDbContext.Sessions
            .IgnoreQueryFilters()
            .FirstAsync(s => s.Id == session.Id);

        entity.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Submit_Rejects_When_Survey_Window_Closed()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);

        // نظرسنجی با تاریخ پایان در گذشته.
        var service = env.Services.GetRequiredService<ISurveyService>();
        var created = await service.CreateAsync(new SaveSurveyRequest
        {
            Code = "SV-R-01",
            QuestionnaireId = questionnaire.Id,
            EndDate = DateTime.UtcNow.AddDays(-1),
            Localizations = [ new SurveyLocalizationDto { Language = Language.Fa, Title = "نظرسنجی" } ]
        });
        var published = await service.PublishAsync(created.Value!.Id);

        var responseService = env.Services.GetRequiredService<IResponseService>();

        var start = await responseService.StartSessionAsync(new StartSessionRequest { SurveyId = published.Value!.Id });

        start.IsFailure.Should().BeTrue();
        start.Error.Code.Should().Be("survey_not_respondable");
    }

    [Fact]
    public async Task Anonymous_Session_Can_Be_Edited_By_Session_Id_Holder()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var question = await SeedChoiceQuestionAsync(env, "QB-R-01");
        var questionnaire = await SeedQuestionnaireAsync(env, question);
        var survey = await SeedActiveSurveyAsync(env, questionnaire.Id, isAnonymous: true);

        var service = env.Services.GetRequiredService<IResponseService>();

        // شناسه‌ی نشست در حالت ناشناس مانند توکن قابلیت عمل می‌کند؛ پاسخ‌دهنده
        // آن را در نشست مرورگر خود نگه می‌دارد.
        var session = (await service.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id })).Value!;

        var save = await service.SaveAnswersAsync(session.Id, new SaveAnswersRequest
        {
            Answers = [ new SaveAnswerRequest { QuestionnaireItemId = questionnaire.Sections[0].Items[0].Id, SelectedOptionIds = [question.Options[0].Id] } ]
        });

        save.IsSuccess.Should().BeTrue();
        save.Value!.IsAnonymous.Should().BeTrue();
        save.Value.RespondentDisplayName.Should().BeNull();
        save.Value.Answers.Should().ContainSingle();

        var entity = await env.ResponseDbContext.Sessions.FirstAsync(s => s.Id == session.Id);

        entity.RespondentUserId.Should().BeNull();
        entity.IsAnonymous.Should().BeTrue();
    }
}
