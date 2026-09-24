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
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Enums;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Survey.Enums;
using ODCC.Infrastructure.Modules.Campaign.Persistence;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Campaign;

/// <summary>
/// آزمون‌های سرویس کمپین‌ها: زمان‌بندی، حل جمعیت هدف، توزیع، یادآورها و پیگیری.
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ
/// پایگاه‌داده‌ی واقعی را لمس نمی‌کنند.
/// </summary>
public class CampaignServiceTests
{
    /// <summary>ساخت و انتشار یک پرسشنامه و سپس یک نظرسنجی‌ی فعال.</summary>
    private static async Task<SurveyDto> SeedActiveSurveyAsync(TestEnvironment env)
    {
        var questionService = env.Services.GetRequiredService<IQuestionService>();
        var questionnaireService = env.Services.GetRequiredService<IQuestionnaireService>();
        var surveyService = env.Services.GetRequiredService<ISurveyService>();

        var question = (await questionService.CreateAsync(new SaveQuestionRequest
        {
            Code = "QB-C-01",
            Type = QuestionType.SingleChoice,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "سؤال نمونه" } ],
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

        var questionnaire = (await questionnaireService.CreateAsync(new SaveQuestionnaireRequest
        {
            Code = "QS-C-01",
            Localizations = [ new QuestionnaireLocalizationDto { Language = Language.Fa, Title = "پرسشنامه" } ],
            Sections =
            [
                new SaveSectionRequest
                {
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش اول" } ],
                    Items = [ new SaveItemRequest { QuestionId = question.Id, IsRequired = true } ]
                }
            ]
        })).Value!;

        (await questionnaireService.PublishAsync(questionnaire.Id)).IsSuccess.Should().BeTrue();

        var survey = (await surveyService.CreateAsync(new SaveSurveyRequest
        {
            Code = "SV-C-01",
            QuestionnaireId = questionnaire.Id,
            Localizations = [ new SurveyLocalizationDto { Language = Language.Fa, Title = "نظرسنجی نمونه" } ]
        })).Value!;

        (await surveyService.PublishAsync(survey.Id)).IsSuccess.Should().BeTrue();

        return survey;
    }

    /// <summary>درخواست کمپین نمونه.</summary>
    private static SaveCampaignRequest CreateRequest(
        string code,
        Guid surveyId,
        TargetAudienceType audienceType = TargetAudienceType.AllCompany,
        IReadOnlyList<Guid>? targetOrgUnitIds = null,
        IReadOnlyList<Guid>? targetEmployeeIds = null,
        IReadOnlyList<SaveReminderRequest>? reminders = null,
        bool includeInactive = false) => new()
        {
            Code = code,
            SurveyId = surveyId,
            AudienceType = audienceType,
            IncludeInactiveEmployees = includeInactive,
            Channel = DistributionChannel.Email,
            TargetOrgUnitIds = targetOrgUnitIds ?? [],
            TargetEmployeeIds = targetEmployeeIds ?? [],
            Reminders = reminders ?? [],
            Localizations =
            [
                new CampaignLocalizationDto { Language = Language.Fa, Title = "کمپین نظرسنجی سه‌ماهه" }
            ]
        };

    private static SaveReminderRequest CreateReminder(DateTime sendAt) => new()
    {
        SendAt = sendAt,
        Localizations =
        [
            new ReminderLocalizationDto { Language = Language.Fa, Subject = "یادآوری پاسخ‌گویی", Body = "لطفاً به نظرسنجی پاسخ دهید." }
        ]
    };

    [Fact]
    public async Task Create_Creates_Campaign_With_Audience_And_Reminders()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var reminderId = Guid.CreateVersion7();
        var result = await service.CreateAsync(CreateRequest(
            "CMP-001",
            survey.Id,
            reminders: [ CreateReminder(DateTime.UtcNow.AddDays(1)) with { Id = reminderId } ]));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("CMP-001");
        result.Value.Status.Should().Be(CampaignStatus.Draft);
        result.Value.SurveyId.Should().Be(survey.Id);
        result.Value.AudienceType.Should().Be(TargetAudienceType.AllCompany);
        result.Value.IsAudienceConfigured.Should().BeTrue("تمام شرکت همواره پیکربندی‌شده است");
        result.Value.CanLaunch.Should().BeTrue();
        result.Value.Reminders.Should().ContainSingle();
        result.Value.Reminders[0].Subject.Should().Be("یادآوری پاسخ‌گویی");
        result.Value.TotalDistributions.Should().Be(0, "هنوز اجرا نشده");

        var entity = await env.CampaignDbContext.Campaigns
            .Include(c => c.Reminders).ThenInclude(r => r.Localizations)
            .FirstOrDefaultAsync(c => c.Code == "CMP-001");
        entity.Should().NotBeNull();
        entity!.Reminders.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_Fails_When_Audience_Type_Misses_Targets()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var result = await service.CreateAsync(CreateRequest("CMP-002", survey.Id, TargetAudienceType.OrgUnits));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("campaign_audience_targets_required");
    }

    [Fact]
    public async Task Create_Fails_With_Duplicate_Code()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        await service.CreateAsync(CreateRequest("CMP-DUP", survey.Id));

        var second = await service.CreateAsync(CreateRequest("CMP-DUP", survey.Id));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("campaign_code_taken");
    }

    [Fact]
    public async Task Schedule_Transitions_Draft_To_Scheduled()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);
        var created = (await service.CreateAsync(CreateRequest("CMP-SCHED", survey.Id))).Value!;

        var result = await service.ScheduleAsync(created.Id, DateTime.UtcNow.AddDays(3));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(CampaignStatus.Scheduled);
        result.Value.ScheduledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Schedule_Rejects_Past_Time()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);
        var created = (await service.CreateAsync(CreateRequest("CMP-PAST", survey.Id))).Value!;

        var result = await service.ScheduleAsync(created.Id, DateTime.UtcNow.AddDays(-1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("campaign_schedule_in_past");
    }

    [Fact]
    public async Task Launch_Resolves_All_Company_Audience_And_Creates_Distributions()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        // ۳ کارمند: ۱ فعال، ۱ مرخصی، ۱ ترک‌کار.
        var units = await env.SeedOrgHierarchyAsync();
        await env.SeedEmployeesAsync(new Dictionary<Guid, int> { { units.companyId, 3 } });

        var created = (await service.CreateAsync(
            CreateRequest("CMP-LAUNCH", survey.Id, includeInactive: false))).Value!;

        var result = await service.LaunchAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(CampaignStatus.Running);
        result.Value.ResolvedRecipientCount.Should().Be(2, "یک فعال + یک مرخصی؛ ترک‌کار فیلتر می‌شود");
        result.Value.CreatedDistributionCount.Should().Be(2);

        var campaign = await service.GetByIdAsync(created.Id);
        campaign.Value!.TotalDistributions.Should().Be(2);
        campaign.Value.DistributionCounts.Should().ContainSingle()
            .Which.Status.Should().Be(DistributionStatus.Pending);
    }

    [Fact]
    public async Task Launch_Includes_Inactive_Employees_When_Requested()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var units = await env.SeedOrgHierarchyAsync();
        await env.SeedEmployeesAsync(new Dictionary<Guid, int> { { units.companyId, 3 } });

        var created = (await service.CreateAsync(
            CreateRequest("CMP-INACTIVE", survey.Id, includeInactive: true))).Value!;

        var result = await service.LaunchAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ResolvedRecipientCount.Should().Be(3, "درخواست شامل کارمندان غیرشاغل شده");
    }

    [Fact]
    public async Task Launch_Resolves_OrgUnit_Subtree_Audience()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        // سلسله: شرکت → دایرکتی(مالی) → دپارتمان(پرداختنی) → تیم(فاکتورها).
        var units = await env.SeedOrgHierarchyAsync();
        await env.SeedEmployeesAsync(new Dictionary<Guid, int>
        {
            { units.companyId, 1 },
            { units.divisionId, 1 },
            { units.departmentId, 1 },
            { units.otherDepartmentId, 1 } // خارج از زیردرخت مالی
        });

        var created = (await service.CreateAsync(CreateRequest(
            "CMP-SUBTREE",
            survey.Id,
            TargetAudienceType.OrgUnits,
            targetOrgUnitIds: [ units.divisionId ]))).Value!;

        var result = await service.LaunchAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        // دایرکتی مالی + دپارتمان پرداختنی (۲ کارمند فعال/مرخصی)؛ شرکت و دپارتمان فناوری інформаتی داخل نمی‌آیند.
        result.Value!.ResolvedRecipientCount.Should().Be(2);
    }

    [Fact]
    public async Task Launch_Resolves_Explicit_Employee_Audience()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var units = await env.SeedOrgHierarchyAsync();
        var employees = await env.SeedEmployeesAsync(new Dictionary<Guid, int> { { units.companyId, 3 } });
        var picked = employees.Take(2).Select(e => e.Id).ToList();

        var created = (await service.CreateAsync(CreateRequest(
            "CMP-EXPLICIT",
            survey.Id,
            TargetAudienceType.Employees,
            targetEmployeeIds: picked))).Value!;

        var result = await service.LaunchAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ResolvedRecipientCount.Should().Be(2);
    }

    [Fact]
    public async Task Launch_Fails_When_Survey_Is_Not_Active()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();

        var questionService = env.Services.GetRequiredService<IQuestionService>();
        var questionnaireService = env.Services.GetRequiredService<IQuestionnaireService>();
        var surveyService = env.Services.GetRequiredService<ISurveyService>();

        var question = (await questionService.CreateAsync(new SaveQuestionRequest
        {
            Code = "QB-C-02",
            Type = QuestionType.YesNo,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "سؤال" } ]
        })).Value!;

        var questionnaire = (await questionnaireService.CreateAsync(new SaveQuestionnaireRequest
        {
            Code = "QS-C-02",
            Localizations = [ new QuestionnaireLocalizationDto { Language = Language.Fa, Title = "پرسشنامه" } ],
            Sections =
            [
                new SaveSectionRequest
                {
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش" } ],
                    Items = [ new SaveItemRequest { QuestionId = question.Id } ]
                }
            ]
        })).Value!;
        (await questionnaireService.PublishAsync(questionnaire.Id)).IsSuccess.Should().BeTrue();

        // نظرسنجی ساخته اما منتشرنشده (پیش‌نویس).
        var survey = (await surveyService.CreateAsync(new SaveSurveyRequest
        {
            Code = "SV-C-02",
            QuestionnaireId = questionnaire.Id,
            Localizations = [ new SurveyLocalizationDto { Language = Language.Fa, Title = "نظرسنجی" } ]
        })).Value!;

        var created = (await service.CreateAsync(CreateRequest("CMP-NOTACTIVE", survey.Id))).Value!;

        var result = await service.LaunchAsync(created.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("survey_not_active");
    }

    [Fact]
    public async Task Launch_Fails_When_Already_Running()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var created = (await service.CreateAsync(CreateRequest("CMP-RUN", survey.Id))).Value!;
        (await service.LaunchAsync(created.Id)).IsSuccess.Should().BeTrue();

        var second = await service.LaunchAsync(created.Id);

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("campaign_cannot_launch");
    }

    [Fact]
    public async Task Complete_Transitions_Running_To_Completed()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var created = (await service.CreateAsync(CreateRequest("CMP-COMPLETE", survey.Id))).Value!;
        (await service.LaunchAsync(created.Id)).IsSuccess.Should().BeTrue();

        var result = await service.CompleteAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(CampaignStatus.Completed);
        result.Value.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_Rejects_Non_Draft_Campaign()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var created = (await service.CreateAsync(CreateRequest("CMP-UPD", survey.Id))).Value!;
        (await service.LaunchAsync(created.Id)).IsSuccess.Should().BeTrue();

        var result = await service.UpdateAsync(created.Id, CreateRequest("CMP-UPD-2", survey.Id));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("campaign_is_not_draft");
    }

    [Fact]
    public async Task Delete_Removes_Draft_But_Rejects_Running()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var draft = (await service.CreateAsync(CreateRequest("CMP-DEL-1", survey.Id))).Value!;
        var running = (await service.CreateAsync(CreateRequest("CMP-DEL-2", survey.Id))).Value!;
        (await service.LaunchAsync(running.Id)).IsSuccess.Should().BeTrue();

        var deleteDraft = await service.DeleteAsync(draft.Id);
        deleteDraft.IsSuccess.Should().BeTrue();

        var deleteRunning = await service.DeleteAsync(running.Id);
        deleteRunning.IsFailure.Should().BeTrue();
        deleteRunning.Error.Code.Should().Be("campaign_not_deletable");
    }

    [Fact]
    public async Task RecordDistributionResult_Marks_Sent_And_Failed()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var units = await env.SeedOrgHierarchyAsync();
        var employees = await env.SeedEmployeesAsync(new Dictionary<Guid, int> { { units.companyId, 2 } });

        var created = (await service.CreateAsync(CreateRequest("CMP-REC", survey.Id))).Value!;
        var launch = await service.LaunchAsync(created.Id);
        launch.IsSuccess.Should().BeTrue();

        var distributions = await service.GetDistributionsAsync(created.Id, new DistributionSearchRequest());
        var first = distributions.Items[0];
        var second = distributions.Items[^1];

        var sent = await service.RecordDistributionResultAsync(first.Id, success: true, failureReason: null);
        sent.IsSuccess.Should().BeTrue();
        sent.Value!.Status.Should().Be(DistributionStatus.Sent);
        sent.Value.SentAt.Should().NotBeNull();

        var failed = await service.RecordDistributionResultAsync(second.Id, success: false, failureReason: "صندوق پستی یافت نشد");
        failed.IsSuccess.Should().BeTrue();
        failed.Value!.Status.Should().Be(DistributionStatus.Failed);
        failed.Value.FailureReason.Should().Be("صندوق پستی یافت نشد");
    }

    [Fact]
    public async Task ProcessDueReminders_Marks_Due_Reminders_And_Increments_Counters()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var units = await env.SeedOrgHierarchyAsync();
        await env.SeedEmployeesAsync(new Dictionary<Guid, int> { { units.companyId, 2 } });

        var created = (await service.CreateAsync(CreateRequest(
            "CMP-REMIND",
            survey.Id,
            reminders:
            [
                CreateReminder(DateTime.UtcNow.AddDays(-1)), // سررسیده
                CreateReminder(DateTime.UtcNow.AddDays(1))    // هنوز نرسیده
            ]))).Value!;

        (await service.LaunchAsync(created.Id)).IsSuccess.Should().BeTrue();

        // هنوز سررسیده‌ای پردازش نشده... اما یادآور سررسیده باید پردازش شود.
        var result = await service.ProcessDueRemindersAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.RemindersProcessed.Should().Be(1, "تنها یادآور سررسیده");
        result.Value.RecipientsNotified.Should().Be(2, "دو گیرنده‌ی در صف");

        var campaign = await service.GetByIdAsync(created.Id);
        campaign.Value!.Reminders.Should().Contain(r => r.Status == ReminderStatus.Sent).And
            .Contain(r => r.Status == ReminderStatus.Scheduled);

        var distributions = await service.GetDistributionsAsync(created.Id, new DistributionSearchRequest());
        distributions.Items.Should().AllSatisfy(d => d.ReminderCount.Should().Be(1));
    }

    [Fact]
    public async Task CancelReminder_Rejects_Sent_Reminder()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var created = (await service.CreateAsync(CreateRequest(
            "CMP-CANCEL",
            survey.Id,
            reminders: [ CreateReminder(DateTime.UtcNow.AddDays(-1)) ]))).Value!;
        (await service.LaunchAsync(created.Id)).IsSuccess.Should().BeTrue();
        (await service.ProcessDueRemindersAsync()).IsSuccess.Should().BeTrue();

        var reminderId = (await service.GetByIdAsync(created.Id)).Value!.Reminders[0].Id;

        var result = await service.CancelReminderAsync(created.Id, reminderId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("reminder_not_cancellable");
    }

    [Fact]
    public async Task Launch_Writes_Audit_Entry_Via_Domain_Event()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var units = await env.SeedOrgHierarchyAsync();
        await env.SeedEmployeesAsync(new Dictionary<Guid, int> { { units.companyId, 1 } });

        var created = (await service.CreateAsync(CreateRequest("CMP-AUDIT", survey.Id))).Value!;
        await service.LaunchAsync(created.Id);

        // رویداد دامنه باید از طریق مرز مجاز ماژول‌ها ممیزی ثبت کرده باشد.
        var auditService = env.Services.GetRequiredService<IAuditService>();
        var entries = await auditService.SearchAsync(new AuditSearchRequest("campaign", null, null, null, null));

        entries.Should().Contain(e => e.Action == "create");
        entries.Should().Contain(e => e.Action == "launch");
    }

    [Fact]
    public async Task Search_Filters_By_Status()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var first = (await service.CreateAsync(CreateRequest("CMP-S-1", survey.Id))).Value!;
        await service.CreateAsync(CreateRequest("CMP-S-2", survey.Id));
        (await service.LaunchAsync(first.Id)).IsSuccess.Should().BeTrue();

        var runningResult = await service.SearchAsync(new CampaignSearchRequest { Status = CampaignStatus.Running });
        runningResult.TotalCount.Should().Be(1);
        runningResult.Items.Should().ContainSingle().Which.Code.Should().Be("CMP-S-1");

        var draftResult = await service.SearchAsync(new CampaignSearchRequest { Status = CampaignStatus.Draft });
        draftResult.TotalCount.Should().Be(1);
        draftResult.Items.Should().ContainSingle().Which.Code.Should().Be("CMP-S-2");
    }

    [Fact]
    public async Task Search_Returns_Distribution_Count_Per_Campaign()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var units = await env.SeedOrgHierarchyAsync();
        await env.SeedEmployeesAsync(new Dictionary<Guid, int> { { units.companyId, 3 } });

        var launched = (await service.CreateAsync(CreateRequest("CMP-COUNT-1", survey.Id))).Value!;
        await service.CreateAsync(CreateRequest("CMP-COUNT-2", survey.Id));
        (await service.LaunchAsync(launched.Id)).IsSuccess.Should().BeTrue();

        var result = await service.SearchAsync(new CampaignSearchRequest());

        var launchedSummary = result.Items.Should().Contain(s => s.Code == "CMP-COUNT-1").Subject;
        // ۲ کارمند شاغل (یک فعال + یک مرخصی)؛ ترک‌کار فیلتر می‌شود.
        launchedSummary.TotalDistributions.Should().Be(2);

        result.Items.Should().Contain(s => s.Code == "CMP-COUNT-2")
            .Which.TotalDistributions.Should().Be(0, "هنوز اجرا نشده");
    }

    [Fact]
    public async Task Archive_Transitions_Running_To_Archived()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var created = (await service.CreateAsync(CreateRequest("CMP-ARCH", survey.Id))).Value!;
        (await service.LaunchAsync(created.Id)).IsSuccess.Should().BeTrue();

        var result = await service.ArchiveAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(CampaignStatus.Archived);
        result.Value.ArchivedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Update_Keeps_Reminders_And_Replaces_Targets()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<ICampaignService>();
        var survey = await SeedActiveSurveyAsync(env);

        var units = await env.SeedOrgHierarchyAsync();
        var reminderId = Guid.CreateVersion7();
        var created = (await service.CreateAsync(CreateRequest(
            "CMP-UPD-KEEP",
            survey.Id,
            TargetAudienceType.OrgUnits,
            targetOrgUnitIds: [units.companyId],
            reminders: [CreateReminder(DateTime.UtcNow.AddDays(2)) with { Id = reminderId }]))).Value!;

        var updated = await service.UpdateAsync(created.Id, CreateRequest(
            "CMP-UPD-KEEP",
            survey.Id,
            TargetAudienceType.OrgUnits,
            targetOrgUnitIds: [units.divisionId],
            reminders: [CreateReminder(DateTime.UtcNow.AddDays(3)) with { Id = reminderId }]));

        updated.IsSuccess.Should().BeTrue();
        updated.Value!.TargetUnits.Should().ContainSingle().Which.OrgUnitId.Should().Be(units.divisionId);
        updated.Value.Reminders.Should().ContainSingle();
        updated.Value.Reminders[0].Id.Should().Be(reminderId);
    }
}
