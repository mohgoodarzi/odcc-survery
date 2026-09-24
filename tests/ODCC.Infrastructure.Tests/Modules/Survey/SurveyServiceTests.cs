using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Survey.Enums;
using ODCC.Infrastructure.Modules.Survey.Persistence;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Survey;

/// <summary>
/// آزمون‌های سرویس نظرسنجی‌ها: چرخه‌ی عمر، تنظیمات، پرچم ناشناس و قالب‌ها.
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ
/// پایگاه‌داده‌ی واقعی را لمس نمی‌کنند.
/// </summary>
public class SurveyServiceTests
{
    /// <summary>ساخت چند سؤال گزینه‌ای در کتابخانه‌ی سؤالات.</summary>
    private static async Task<List<QuestionDto>> SeedQuestionsAsync(TestEnvironment env, int count)
    {
        var service = env.Services.GetRequiredService<IQuestionService>();
        var questions = new List<QuestionDto>();

        for (var i = 0; i < count; i++)
        {
            var request = new SaveQuestionRequest
            {
                Code = $"QB-S-{i:D2}",
                Type = QuestionType.SingleChoice,
                Localizations =
                [
                    new QuestionLocalizationDto { Language = Language.Fa, Text = $"سؤال {i + 1}" }
                ],
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
            };

            var result = await service.CreateAsync(request);
            result.IsSuccess.Should().BeTrue("سؤال نمونه باید ساخته شود");
            questions.Add(result.Value!);
        }

        return questions;
    }

    /// <summary>ساخت و انتشار یک پرسشنامه‌ی فعال (پیش‌نیاز هر نظرسنجی).</summary>
    private static async Task<QuestionnaireDto> SeedActiveQuestionnaireAsync(TestEnvironment env)
    {
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 1);

        var created = await service.CreateAsync(new SaveQuestionnaireRequest
        {
            Code = "QS-SV-01",
            Localizations = [ new QuestionnaireLocalizationDto { Language = Language.Fa, Title = "پرسشنامه‌ی نظرسنجی" } ],
            Sections =
            [
                new SaveSectionRequest
                {
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش اول" } ],
                    Items = [ new SaveItemRequest { QuestionId = questions[0].Id, IsRequired = true } ]
                }
            ]
        });

        created.IsSuccess.Should().BeTrue();
        var published = await service.PublishAsync(created.Value!.Id);
        published.IsSuccess.Should().BeTrue();
        return published.Value!;
    }

    /// <summary>درخواست نظرسنجی‌ی نمونه.</summary>
    private static SaveSurveyRequest CreateRequest(
        string code,
        Guid questionnaireId,
        DateTime? startDate = null,
        DateTime? endDate = null,
        bool isAnonymous = false) => new()
        {
            Code = code,
            QuestionnaireId = questionnaireId,
            IsAnonymous = isAnonymous,
            StartDate = startDate,
            EndDate = endDate,
            EstimatedMinutes = 10,
            Localizations =
            [
                new SurveyLocalizationDto { Language = Language.Fa, Title = "نظرسنجی محیط کار" },
                new SurveyLocalizationDto { Language = Language.En, Title = "Workplace survey" }
            ]
        };

    [Fact]
    public async Task Create_Creates_Survey_With_Settings_And_Localizations()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);

        var result = await service.CreateAsync(CreateRequest("SV-001", questionnaire.Id, isAnonymous: true));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("SV-001");
        result.Value.Status.Should().Be(SurveyStatus.Draft);
        result.Value.QuestionnaireId.Should().Be(questionnaire.Id);
        result.Value.QuestionnaireVersion.Should().Be(questionnaire.Version);
        result.Value.QuestionnaireCode.Should().Be(questionnaire.Code);
        result.Value.IsAnonymous.Should().BeTrue();
        result.Value.Title.Should().Be("نظرسنجی محیط کار");
        result.Value.EstimatedMinutes.Should().Be(10);
        result.Value.IsPublishable.Should().BeTrue();
        result.Value.AcceptsResponses.Should().BeFalse("پیش‌نویس هنوز پاسخ نمی‌پذیرد");

        var entity = await env.SurveyDbContext.Surveys.FirstOrDefaultAsync(s => s.Code == "SV-001");
        entity.Should().NotBeNull();
        entity!.Localizations.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_Fails_When_Questionnaire_Is_Not_Active()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionService = env.Services.GetRequiredService<IQuestionService>();
        var questionnaireService = env.Services.GetRequiredService<IQuestionnaireService>();
        var question = (await SeedQuestionsAsync(env, 1))[0];

        // پرسشنامه‌ی منتشرنشده (پیش‌نویس) نباید قابل استفاده در نظرسنجی باشد.
        var draft = await questionnaireService.CreateAsync(new SaveQuestionnaireRequest
        {
            Code = "QS-DRAFT",
            Localizations = [ new QuestionnaireLocalizationDto { Language = Language.Fa, Title = "پیش‌نویس" } ],
            Sections =
            [
                new SaveSectionRequest
                {
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش" } ],
                    Items = [ new SaveItemRequest { QuestionId = question.Id } ]
                }
            ]
        });
        draft.IsSuccess.Should().BeTrue();

        var result = await service.CreateAsync(CreateRequest("SV-002", draft.Value!.Id));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("questionnaire_not_active");
    }

    [Fact]
    public async Task Create_Fails_With_Duplicate_Code()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);

        await service.CreateAsync(CreateRequest("SV-DUP", questionnaire.Id));

        var second = await service.CreateAsync(CreateRequest("SV-DUP", questionnaire.Id));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("survey_code_taken");
    }

    [Fact]
    public async Task Publish_Transitions_To_Active_When_No_StartDate()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);
        var created = (await service.CreateAsync(CreateRequest("SV-PUB-1", questionnaire.Id))).Value!;

        var result = await service.PublishAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(SurveyStatus.Active);
        result.Value.PublishedAt.Should().NotBeNull();
        result.Value.AcceptsResponses.Should().BeTrue();
    }

    [Fact]
    public async Task Publish_Transitions_To_Scheduled_When_StartDate_In_Future()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);
        var start = DateTime.UtcNow.AddDays(7);
        var created = (await service.CreateAsync(CreateRequest("SV-SCHED", questionnaire.Id, startDate: start))).Value!;

        var result = await service.PublishAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(SurveyStatus.Scheduled);
        result.Value.AcceptsResponses.Should().BeFalse("پنجره‌ی پاسخ‌گویی هنوز باز نشده");
    }

    [Fact]
    public async Task Start_Transitions_Scheduled_To_Active()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);
        var start = DateTime.UtcNow.AddDays(7);
        var created = (await service.CreateAsync(CreateRequest("SV-START", questionnaire.Id, startDate: start))).Value!;
        (await service.PublishAsync(created.Id)).IsSuccess.Should().BeTrue();

        var result = await service.StartAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(SurveyStatus.Active);
        result.Value.ActivatedAt.Should().NotBeNull();
        result.Value.AcceptsResponses.Should().BeTrue();
    }

    [Fact]
    public async Task Pause_And_Resume_Toggle_Accepting_Responses()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);
        var created = (await service.CreateAsync(CreateRequest("SV-PAUSE", questionnaire.Id))).Value!;
        (await service.PublishAsync(created.Id)).IsSuccess.Should().BeTrue();

        var paused = await service.PauseAsync(created.Id);
        paused.IsSuccess.Should().BeTrue();
        paused.Value!.Status.Should().Be(SurveyStatus.Paused);
        paused.Value.AcceptsResponses.Should().BeFalse();

        var resumed = await service.ResumeAsync(created.Id);
        resumed.IsSuccess.Should().BeTrue();
        resumed.Value!.Status.Should().Be(SurveyStatus.Active);
        resumed.Value.AcceptsResponses.Should().BeTrue();
    }

    [Fact]
    public async Task Close_Ends_Response_Window()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);
        var created = (await service.CreateAsync(CreateRequest("SV-CLOSE", questionnaire.Id))).Value!;
        (await service.PublishAsync(created.Id)).IsSuccess.Should().BeTrue();

        var result = await service.CloseAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(SurveyStatus.Closed);
        result.Value.ClosedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Archive_Removes_From_Active_Circulation()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);
        var created = (await service.CreateAsync(CreateRequest("SV-ARCH", questionnaire.Id))).Value!;

        var result = await service.ArchiveAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(SurveyStatus.Archived);
        result.Value.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_Rejects_Non_Draft_Survey()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);
        var created = (await service.CreateAsync(CreateRequest("SV-UPD", questionnaire.Id))).Value!;
        (await service.PublishAsync(created.Id)).IsSuccess.Should().BeTrue();

        var result = await service.UpdateAsync(created.Id, CreateRequest("SV-UPD-2", questionnaire.Id));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("survey_is_not_draft");
    }

    [Fact]
    public async Task Delete_Removes_Draft_But_Rejects_Published()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);

        var draft = (await service.CreateAsync(CreateRequest("SV-DEL-1", questionnaire.Id))).Value!;
        var published = (await service.CreateAsync(CreateRequest("SV-DEL-2", questionnaire.Id))).Value!;
        (await service.PublishAsync(published.Id)).IsSuccess.Should().BeTrue();

        var deleteDraft = await service.DeleteAsync(draft.Id);
        deleteDraft.IsSuccess.Should().BeTrue();

        var deletePublished = await service.DeleteAsync(published.Id);
        deletePublished.IsFailure.Should().BeTrue();
        deletePublished.Error.Code.Should().Be("survey_not_deletable");
    }

    [Fact]
    public async Task Create_Writes_Audit_Entry_Via_Domain_Event()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);

        await service.CreateAsync(CreateRequest("SV-AUDIT", questionnaire.Id));

        // رویداد دامنه باید از طریق مرز مجاز ماژول‌ها ممیزی ثبت کرده باشد.
        var auditService = env.Services.GetRequiredService<IAuditService>();
        var entries = await auditService.SearchAsync(new AuditSearchRequest("survey", null, null, null, null));

        entries.Should().ContainSingle().Which.Action.Should().Be("create");
    }

    [Fact]
    public async Task Search_Filters_By_Status()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);

        var first = (await service.CreateAsync(CreateRequest("SV-S-1", questionnaire.Id))).Value!;
        await service.CreateAsync(CreateRequest("SV-S-2", questionnaire.Id));
        (await service.PublishAsync(first.Id)).IsSuccess.Should().BeTrue();

        var activeResult = await service.SearchAsync(new SurveySearchRequest { Status = SurveyStatus.Active });
        activeResult.TotalCount.Should().Be(1);
        activeResult.Items.Should().ContainSingle().Which.Code.Should().Be("SV-S-1");

        var draftResult = await service.SearchAsync(new SurveySearchRequest { Status = SurveyStatus.Draft });
        draftResult.TotalCount.Should().Be(1);
        draftResult.Items.Should().ContainSingle().Which.Code.Should().Be("SV-S-2");
    }

    [Fact]
    public async Task Search_Excludes_Archived_By_Default()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);

        var archived = (await service.CreateAsync(CreateRequest("SV-ARC", questionnaire.Id))).Value!;
        await service.CreateAsync(CreateRequest("SV-VIS", questionnaire.Id));
        (await service.ArchiveAsync(archived.Id)).IsSuccess.Should().BeTrue();

        var result = await service.SearchAsync(new SurveySearchRequest());

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Code.Should().Be("SV-VIS");

        var withArchived = await service.SearchAsync(new SurveySearchRequest { IncludeArchived = true });
        withArchived.TotalCount.Should().Be(2);
    }

    // --- قالب‌ها ---------------------------------------------------------------

    [Fact]
    public async Task Template_Create_And_Instantiate_Copies_Settings()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var templateService = env.Services.GetRequiredService<ISurveyTemplateService>();
        var surveyService = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);

        var template = await templateService.CreateAsync(new SaveSurveyTemplateRequest
        {
            Code = "TPL-01",
            QuestionnaireId = questionnaire.Id,
            IsAnonymous = true,
            EstimatedMinutes = 15,
            Localizations = [ new SurveyTemplateLocalizationDto { Language = Language.Fa, Title = "قالب نظرسنجی محیط کار" } ]
        });

        template.IsSuccess.Should().BeTrue();
        template.Value!.IsAnonymous.Should().BeTrue();
        template.Value.EstimatedMinutes.Should().Be(15);
        template.Value.QuestionnaireCode.Should().Be(questionnaire.Code);

        var survey = await surveyService.CreateFromTemplateAsync(new CreateSurveyFromTemplateRequest
        {
            TemplateId = template.Value.Id,
            Code = "SV-FROM-TPL"
        });

        survey.IsSuccess.Should().BeTrue();
        survey.Value!.Code.Should().Be("SV-FROM-TPL");
        survey.Value.TemplateId.Should().Be(template.Value.Id);
        survey.Value.IsAnonymous.Should().BeTrue("تنظیمات باید از قالب کپی شود");
        survey.Value.EstimatedMinutes.Should().Be(15);
        survey.Value.QuestionnaireId.Should().Be(questionnaire.Id);
        survey.Value.Title.Should().Be("قالب نظرسنجی محیط کار", "ترجمه‌ها باید از قالب کپی شوند");
    }

    [Fact]
    public async Task Template_Archive_Prevents_Instantiation()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var templateService = env.Services.GetRequiredService<ISurveyTemplateService>();
        var surveyService = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);

        var template = (await templateService.CreateAsync(new SaveSurveyTemplateRequest
        {
            Code = "TPL-ARCH",
            QuestionnaireId = questionnaire.Id,
            Localizations = [ new SurveyTemplateLocalizationDto { Language = Language.Fa, Title = "قالب" } ]
        })).Value!;

        (await templateService.ArchiveAsync(template.Id)).IsSuccess.Should().BeTrue();

        var survey = await surveyService.CreateFromTemplateAsync(new CreateSurveyFromTemplateRequest
        {
            TemplateId = template.Id,
            Code = "SV-ARCHIVED-TPL"
        });

        survey.IsFailure.Should().BeTrue();
        survey.Error.Code.Should().Be("template_archived");
    }

    [Fact]
    public async Task Template_Delete_Rejects_When_Surveys_Reference_It()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var templateService = env.Services.GetRequiredService<ISurveyTemplateService>();
        var surveyService = env.Services.GetRequiredService<ISurveyService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);

        var template = (await templateService.CreateAsync(new SaveSurveyTemplateRequest
        {
            Code = "TPL-USE",
            QuestionnaireId = questionnaire.Id,
            Localizations = [ new SurveyTemplateLocalizationDto { Language = Language.Fa, Title = "قالب" } ]
        })).Value!;

        await surveyService.CreateFromTemplateAsync(new CreateSurveyFromTemplateRequest
        {
            TemplateId = template.Id,
            Code = "SV-USING-TPL"
        });

        var result = await templateService.DeleteAsync(template.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("template_in_use");
    }

    [Fact]
    public async Task Template_Search_Paginates_Results()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var templateService = env.Services.GetRequiredService<ISurveyTemplateService>();
        var questionnaire = await SeedActiveQuestionnaireAsync(env);

        for (var i = 0; i < 5; i++)
        {
            await templateService.CreateAsync(new SaveSurveyTemplateRequest
            {
                Code = $"TPL-PAGE-{i:D2}",
                QuestionnaireId = questionnaire.Id,
                Localizations = [ new SurveyTemplateLocalizationDto { Language = Language.Fa, Title = $"قالب {i}" } ]
            });
        }

        var result = await templateService.SearchAsync(new SurveyTemplateSearchRequest { Page = 1, PageSize = 2 });

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.TotalPages.Should().Be(3);
    }
}
