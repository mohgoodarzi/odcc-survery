using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Application.Modules.Analytics.Abstractions;
using ODCC.Application.Modules.Analytics.Dtos;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Application.Modules.Response.Abstractions;
using ODCC.Application.Modules.Response.Dtos;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Analytics.Enums;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Infrastructure.Modules.Analytics.Persistence;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Analytics;

/// <summary>
/// آزمون‌های ماژول تحلیلات: محاسبه‌ی شاخص‌های NPS/CSAT/CES، مدل خواندنی
/// (جایگزینی به‌جای الحاق)، حریم خصوصی (عدم ذخیره‌ی شناسه‌ی پاسخ‌گو)،
/// بخش‌بندی سازمانی با رفتار fail-closed، داشبورد، روند زمانی و محاسبه‌ی
/// خودکار پس از ارسال پاسخ.
///
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ
/// پایگاه‌داده‌ی واقعی را لمس نمی‌کنند.
/// </summary>
public class AnalyticsServiceTests
{
    private const string NpsCode = "QB-A-NPS";
    private const string CsatCode = "QB-A-CSAT";
    private const string CesCode = "QB-A-CES";
    private const string ChoiceCode = "QB-A-CH";

    /// <summary>آماده‌سازی یک نظرسنجی با سؤال‌های NPS (طیف ۱۰)، CSAT (طیف ۵)، CES (طیف ۷) و یک سؤال گزینه‌ای.</summary>
    private static async Task<(SurveyDto survey, QuestionnaireDto questionnaire, QuestionDto choice)>
        SeedSurveyWithMetricQuestionsAsync(TestEnvironment env, bool isAnonymous = false, string code = "SV-A-01")
    {
        var questionService = env.Services.GetRequiredService<IQuestionService>();

        var nps = (await questionService.CreateAsync(new SaveQuestionRequest
        {
            Code = NpsCode,
            Type = QuestionType.Rating,
            ScaleMax = 10,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "احتمال پیشنهاد" } ]
        })).Value!;

        var csat = (await questionService.CreateAsync(new SaveQuestionRequest
        {
            Code = CsatCode,
            Type = QuestionType.Rating,
            ScaleMax = 5,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "رضایت" } ]
        })).Value!;

        var ces = (await questionService.CreateAsync(new SaveQuestionRequest
        {
            Code = CesCode,
            Type = QuestionType.Rating,
            ScaleMax = 7,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "سهولت" } ]
        })).Value!;

        var choice = (await questionService.CreateAsync(new SaveQuestionRequest
        {
            Code = ChoiceCode,
            Type = QuestionType.SingleChoice,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "انتخاب" } ],
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

        var questionnaireService = env.Services.GetRequiredService<IQuestionnaireService>();

        var created = await questionnaireService.CreateAsync(new SaveQuestionnaireRequest
        {
            Code = "QS-A-01",
            Localizations = [ new QuestionnaireLocalizationDto { Language = Language.Fa, Title = "پرسشنامه تحلیلی" } ],
            Sections =
            [
                new SaveSectionRequest
                {
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش اول" } ],
                    Items =
                    [
                        new SaveItemRequest { QuestionId = nps.Id, IsRequired = true },
                        new SaveItemRequest { QuestionId = csat.Id, IsRequired = true },
                        new SaveItemRequest { QuestionId = ces.Id, IsRequired = true },
                        new SaveItemRequest { QuestionId = choice.Id, IsRequired = true }
                    ]
                }
            ]
        });

        created.IsSuccess.Should().BeTrue();
        var published = await questionnaireService.PublishAsync(created.Value!.Id);
        published.IsSuccess.Should().BeTrue();

        var surveyService = env.Services.GetRequiredService<ISurveyService>();

        var surveyCreated = await surveyService.CreateAsync(new SaveSurveyRequest
        {
            Code = code,
            QuestionnaireId = published.Value!.Id,
            IsAnonymous = isAnonymous,
            // پاسخ یگانه غیرفعال تا هر کاربر بتواند چند نشست مستقل بسازد.
            SingleResponsePerUser = false,
            Localizations = [ new SurveyLocalizationDto { Language = Language.Fa, Title = "نظرسنجی تحلیلی" } ]
        });

        surveyCreated.IsSuccess.Should().BeTrue();
        var surveyPublished = await surveyService.PublishAsync(surveyCreated.Value!.Id);
        surveyPublished.IsSuccess.Should().BeTrue();

        return (surveyPublished.Value!, published.Value!, choice);
    }

    /// <summary>
    /// ثبت یک پاسخ کامل (NPS، CSAT، CES و گزینه‌ای) به‌عنوان یک کاربر جدید.
    /// برای نظرسنجی‌های غیرناشناس، کارمندِ کاربر در واحد سازمانی داده‌شده
    /// ساخته می‌شود تا بخش‌بندی سازمانی قابل آزمون باشد.
    /// </summary>
    private static async Task SubmitResponseAsync(
        TestEnvironment env,
        Guid surveyId,
        QuestionnaireDto questionnaire,
        decimal nps,
        decimal csat,
        decimal ces,
        string choiceOptionCode,
        Guid? orgUnitId = null,
        bool isAnonymous = false)
    {
        var responseService = env.Services.GetRequiredService<IResponseService>();
        var current = env.Services.GetRequiredService<TestCurrentUserService>();

        var previousUserId = current.UserId;
        var previousOrgUnitId = current.OrgUnitId;
        var previousDisplayName = current.DisplayName;

        var userId = Guid.NewGuid();
        current.UserId = userId;
        current.IsAuthenticated = true;
        current.OrgUnitId = null;
        current.DisplayName = $"کاربر {userId.ToString()[..8]}";

        try
        {
            if (!isAnonymous && orgUnitId.HasValue)
                await EnsureEmployeeForUserAsync(env, userId, orgUnitId.Value);

            var start = await responseService.StartSessionAsync(new StartSessionRequest { SurveyId = surveyId });
            start.IsSuccess.Should().BeTrue();

            var npsItem = questionnaire.Sections[0].Items.First(i => i.QuestionCode == NpsCode);
            var csatItem = questionnaire.Sections[0].Items.First(i => i.QuestionCode == CsatCode);
            var cesItem = questionnaire.Sections[0].Items.First(i => i.QuestionCode == CesCode);
            var choiceItem = questionnaire.Sections[0].Items.First(i => i.QuestionCode == ChoiceCode);

            // گزینه‌ی انتخابی از ساختار پرسشنامه (نه از سؤال کتابخانه) گرفته می‌شود.
            var context = await responseService.GetRespondentContextAsync(surveyId);
            var structureChoiceItem = context.Value!.Sections
                .SelectMany(s => s.Items)
                .First(i => i.QuestionCode == ChoiceCode);
            var optionId = structureChoiceItem.Options.Single(o => o.Code == choiceOptionCode).Id;

            var submit = await responseService.SubmitAsync(start.Value!.Id, new SubmitResponseRequest
            {
                Answers =
                [
                    new SaveAnswerRequest { QuestionnaireItemId = npsItem.Id, NumericValue = nps },
                    new SaveAnswerRequest { QuestionnaireItemId = csatItem.Id, NumericValue = csat },
                    new SaveAnswerRequest { QuestionnaireItemId = cesItem.Id, NumericValue = ces },
                    new SaveAnswerRequest { QuestionnaireItemId = choiceItem.Id, SelectedOptionIds = [optionId] }
                ]
            });

            submit.IsSuccess.Should().BeTrue();
        }
        finally
        {
            current.UserId = previousUserId;
            current.OrgUnitId = previousOrgUnitId;
            current.DisplayName = previousDisplayName;
        }
    }

    /// <summary>ساخت (در صورت نبودن) کارمندِ متعلق به یک کاربر در یک واحد سازمانی.</summary>
    private static async Task EnsureEmployeeForUserAsync(TestEnvironment env, Guid userId, Guid orgUnitId)
    {
        var employeeRepository = env.Services.GetRequiredService<IEmployeeRepository>();

        if (await employeeRepository.FindByUserIdAsync(userId) is not null)
            return;

        var code = $"EMP-{userId.ToString()[..6].ToUpperInvariant()}";

        env.OrganizationDbContext.Employees.Add(new ODCC.Domain.Modules.Organization.Entities.Employee
        {
            EmployeeCode = code,
            FirstName = "پاسخ",
            LastName = "دهنده",
            UserId = userId,
            OrgUnitId = orgUnitId,
            Status = ODCC.Domain.Modules.Organization.Enums.EmployeeStatus.Active,
            StartDate = new DateOnly(2020, 1, 1),
            WorkEmail = $"{code.ToLowerInvariant()}@test.local"
        });

        await env.OrganizationDbContext.SaveChangesAsync();
    }

    // --- محاسبه‌ی شاخص‌ها ---------------------------------------------------------

    [Fact]
    public async Task Compute_Calculates_Nps_Csat_And_Ces_From_Rating_Scales()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        // NPS: ۳ ترویج‌کننده (۹، ۱۰، ۱۰) و ۱ منتقد (۳) → (۳-۱)/۴*۱۰۰ = ۵۰.
        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A");
        await SubmitResponseAsync(env, survey.Id, questionnaire, 10, 4, 5, "B");
        await SubmitResponseAsync(env, survey.Id, questionnaire, 10, 5, 7, "A");
        await SubmitResponseAsync(env, survey.Id, questionnaire, 3, 2, 2, "B");

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var result = await service.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        result.IsSuccess.Should().BeTrue();
        result.Value!.NpsScore.Should().Be(50m);
        result.Value.NpsPromoters.Should().Be(3);
        result.Value.NpsPassives.Should().Be(0);
        result.Value.NpsDetractors.Should().Be(1);

        // CSAT: درصد پاسخ‌های ۴ و ۵ → ۳ از ۴ = ۷۵.
        result.Value.CsatScore.Should().Be(75m);

        // CES: درصد پاسخ‌های ۵ به بالا → ۳ از ۴ = ۷۵.
        result.Value.CesScore.Should().Be(75m);
    }

    [Fact]
    public async Task Compute_Detects_Metric_Questions_By_Scale_Max()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        await SubmitResponseAsync(env, survey.Id, questionnaire, 10, 5, 7, "A");

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var result = await service.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        result.IsSuccess.Should().BeTrue();

        // هر سؤال بر اساس طیف خود به شاخص متناظر شناسایی می‌شود.
        result.Value!.Questions.Single(q => q.QuestionCode == NpsCode).DetectedMetric.Should().Be(MetricType.Nps);
        result.Value.Questions.Single(q => q.QuestionCode == CsatCode).DetectedMetric.Should().Be(MetricType.Csat);
        result.Value.Questions.Single(q => q.QuestionCode == CesCode).DetectedMetric.Should().Be(MetricType.Ces);

        // سؤال گزینه‌ای شاخصی ندارد.
        result.Value.Questions.Single(q => q.QuestionCode == ChoiceCode).DetectedMetric.Should().BeNull();
    }

    [Fact]
    public async Task Compute_Computes_Option_Distribution_And_Percentages()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A");
        await SubmitResponseAsync(env, survey.Id, questionnaire, 10, 5, 6, "A");
        await SubmitResponseAsync(env, survey.Id, questionnaire, 10, 5, 6, "B");

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var result = await service.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        var metric = result.Value!.Questions.Single(q => q.QuestionCode == ChoiceCode);

        metric.ResponseCount.Should().Be(3);
        metric.Options.Should().HaveCount(2);

        var optionA = metric.Options.Single(o => o.OptionCode == "A");
        optionA.Count.Should().Be(2);
        optionA.Percentage.Should().BeApproximately(100m * 2 / 3, 0.01m);

        var optionB = metric.Options.Single(o => o.OptionCode == "B");
        optionB.Count.Should().Be(1);
        optionB.Percentage.Should().BeApproximately(100m / 3, 0.01m);
    }

    [Fact]
    public async Task Compute_Computes_Completion_Rate_With_Incomplete_Sessions()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        // دو پاسخ ارسال‌شده.
        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A");
        await SubmitResponseAsync(env, survey.Id, questionnaire, 10, 5, 6, "B");

        // یک نشست شروع‌شده ولی ارسال‌نشده (افت پاسخ).
        var responseService = env.Services.GetRequiredService<IResponseService>();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, userName: "partial");
        await responseService.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id });

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var result = await service.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        result.IsSuccess.Should().BeTrue();
        result.Value!.TotalSessions.Should().Be(3, "مخرج نرخ تکمیل کل نشست‌های شروع‌شده است");
        result.Value.CompletedSessions.Should().Be(2);
        result.Value.CompletionRate.Should().BeApproximately(100m * 2 / 3, 0.01m);
    }

    // --- مدل خواندنی -------------------------------------------------------------

    [Fact]
    public async Task Compute_Replaces_Read_Model_Instead_Of_Appending()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        await SubmitResponseAsync(env, survey.Id, questionnaire, 10, 5, 7, "A");

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        await service.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        var firstCount = await env.AnalyticsDbContext.SurveyMetrics.CountAsync();
        firstCount.Should().Be(1, "هر نظرسنجی در هر بُعد فقط یک عکس‌العمل دارد");

        var firstRow = await env.AnalyticsDbContext.SurveyMetrics.FirstAsync();
        var firstComputedAt = firstRow.ComputedAt;

        // پاسخ جدید اضافه می‌شود و محاسبه تکرار می‌شود.
        await SubmitResponseAsync(env, survey.Id, questionnaire, 1, 1, 1, "B");

        await Task.Delay(15);
        await service.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        // همچنان فقط یک ردیف وجود دارد — مقادیر جایگزین شده‌اند.
        var afterCount = await env.AnalyticsDbContext.SurveyMetrics.CountAsync();
        afterCount.Should().Be(1);

        var row = await env.AnalyticsDbContext.SurveyMetrics.FirstAsync();
        row.Id.Should().Be(firstRow.Id, "همان ردیف به‌روزرسانی می‌شود، ردیف جدید ساخته نمی‌شود");
        row.CompletedSessions.Should().Be(2);
        row.ComputedAt.Should().BeAfter(firstComputedAt);

        // NPS از ۱۰۰ (یک ترویج‌کننده) به ۰ (یک ترویج‌کننده + یک منتقد) افت می‌کند.
        row.NpsScore.Should().Be(0m);
    }

    [Fact]
    public async Task Get_Returns_Stored_Read_Model_Without_Recomputing()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        // هنوز هیچ عکس‌العملی وجود ندارد → نتیجه‌ی تهی (بدون ایجاد ردیف).
        var before = await service.GetAsync(survey.Id, new AnalyticsFilter());

        before.IsSuccess.Should().BeTrue();
        before.Value!.CompletedSessions.Should().Be(0);
        before.Value.Questions.Should().BeEmpty();

        var rowsBefore = await env.AnalyticsDbContext.SurveyMetrics.CountAsync();
        rowsBefore.Should().Be(0, "مسیر GET عکس‌العمل نمی‌سازد");

        // حالا محاسبه می‌کنیم.
        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A");
        await service.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        // مسیر GET حالا عکس‌العمل را برمی‌گرداند.
        var after = await service.GetAsync(survey.Id, new AnalyticsFilter());

        after.Value!.CompletedSessions.Should().Be(1);
        after.Value.NpsScore.Should().Be(100m);
        after.Value.Questions.Should().HaveCount(4);

        // فقط یک ردیف اضافه شده (از مسیر محاسبه، نه GET).
        var rowsAfter = await env.AnalyticsDbContext.SurveyMetrics.CountAsync();
        rowsAfter.Should().Be(1);
    }

    // --- حریم خصوصی --------------------------------------------------------------

    [Fact]
    public async Task Compute_Never_Stores_Respondent_Identity()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        var units = await env.SeedOrgHierarchyAsync();

        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A",
            orgUnitId: units.companyId);

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var result = await service.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        result.IsSuccess.Should().BeTrue();

        var row = await env.AnalyticsDbContext.SurveyMetrics.FirstAsync();

        // عکس‌العمل فقط تجمع است — هیچ شناسه‌ی پاسخ‌گویی در آن نیست.
        row.SurveyId.Should().Be(survey.Id);
        row.SegmentType.Should().Be(AnalyticsSegment.Survey);
        row.OrgUnitId.Should().BeNull();
        row.CampaignId.Should().BeNull();

        // متن JSON هم نباید شناسه‌ای از پاسخ‌گو داشته باشد.
        row.QuestionMetrics.Should().NotContain(userId.ToString());
    }

    [Fact]
    public async Task Segments_By_OrgUnit_Returns_Empty_For_Anonymous_Survey()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env, isAnonymous: true);

        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A", isAnonymous: true);

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var result = await service.GetAsync(survey.Id, new AnalyticsFilter());

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsAnonymous.Should().BeTrue();
        result.Value.CanSegmentByOrgUnit.Should().BeFalse();

        var segments = await service.GetSegmentsByOrgUnitAsync(survey.Id, new AnalyticsFilter());

        segments.IsSuccess.Should().BeTrue();
        segments.Value.Should().BeEmpty("نظرسنجی ناشناس هیچ پیوند سازمانی ذخیره نکرده است");
    }

    [Fact]
    public async Task Segments_By_OrgUnit_Groups_Sessions_By_Respondent_Unit()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        var units = await env.SeedOrgHierarchyAsync();

        // دو پاسخ در «حساب‌های پرداختنی» و یکی در «فناوری اطلاعات».
        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A", orgUnitId: units.departmentId);
        await SubmitResponseAsync(env, survey.Id, questionnaire, 10, 5, 7, "B", orgUnitId: units.departmentId);
        await SubmitResponseAsync(env, survey.Id, questionnaire, 3, 2, 2, "A", orgUnitId: units.otherDepartmentId);

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var result = await service.GetSegmentsByOrgUnitAsync(survey.Id, new AnalyticsFilter());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);

        // برچسب بخش، مسیر مادی واحد سازمانی است (ساخته‌شده از کدهای واحد).
        var ap = result.Value.Single(s => s.Label == "/hq/fin/ap");
        ap.TotalSessions.Should().Be(2);
        ap.CompletedSessions.Should().Be(2);
        ap.CompletionRate.Should().Be(100m);

        var it = result.Value.Single(s => s.Label == "/hq/ops/it");
        it.TotalSessions.Should().Be(1);
    }

    [Fact]
    public async Task Segments_By_OrgUnit_Fails_Closed_When_No_Visible_Scope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        // کاربر بدون دامنه‌ی سازمانی قابل‌مشاهده.
        env.SetCurrentUser(orgUnitId: null, DataScope.Own);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        var units = await env.SeedOrgHierarchyAsync();

        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A", orgUnitId: units.companyId);

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var result = await service.GetSegmentsByOrgUnitAsync(survey.Id, new AnalyticsFilter());

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty("بدون دامنه‌ی قابل‌مشاهده هیچ داده‌ای برنمی‌گردد (fail-closed)");
    }

    [Fact]
    public async Task Segments_By_OrgUnit_Denies_Explicit_Unit_Outside_User_Scope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var units = await env.SeedOrgHierarchyAsync();
        // کاربر به دایرکتی «امور مالی» محدود است.
        env.SetCurrentUser(units.divisionId, DataScope.Department);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        // پاسخ از واحدی خارج از دامنه‌ی کاربر.
        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A", orgUnitId: units.otherDepartmentId);

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var result = await service.GetSegmentsByOrgUnitAsync(survey.Id, new AnalyticsFilter
        {
            OrgUnitId = units.otherDepartmentId,
            IncludeDescendants = true
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty("درخواست صریحِ واحدِ خارج از دامنه نباید داده برگرداند");
    }

    [Fact]
    public async Task Segments_By_OrgUnit_Respects_Explicit_Unit_Within_User_Scope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var units = await env.SeedOrgHierarchyAsync();
        env.SetCurrentUser(units.companyId, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A", orgUnitId: units.departmentId);
        await SubmitResponseAsync(env, survey.Id, questionnaire, 10, 5, 7, "B", orgUnitId: units.otherDepartmentId);

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var result = await service.GetSegmentsByOrgUnitAsync(survey.Id, new AnalyticsFilter
        {
            OrgUnitId = units.departmentId,
            IncludeDescendants = true
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value.Single().Label.Should().Be("/hq/fin/ap");
        result.Value.Single().TotalSessions.Should().Be(1);
    }

    // --- داشبورد و روند -----------------------------------------------------------

    [Fact]
    public async Task Dashboard_Aggregates_Visible_Surveys_And_Trend()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        // قبل از هر پاسخی، داشبورد شمارشِ ارسال ندارد.
        var empty = await service.GetDashboardAsync(new AnalyticsFilter());
        empty.Value!.TotalSurveys.Should().Be(1);
        empty.Value.ActiveSurveys.Should().Be(1);
        empty.Value.CompletedSessions.Should().Be(0);

        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A");
        await SubmitResponseAsync(env, survey.Id, questionnaire, 10, 5, 7, "B");

        // شنونده‌ی تحلیلات پس از هر ارسال، عکس‌العمل را به‌روز می‌کند.
        await service.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        var dashboard = await service.GetDashboardAsync(new AnalyticsFilter());

        dashboard.IsSuccess.Should().BeTrue();
        dashboard.Value!.TotalSurveys.Should().Be(1);
        dashboard.Value.ActiveSurveys.Should().Be(1);
        dashboard.Value.CompletedSessions.Should().Be(2);
        dashboard.Value.CompletionRate.Should().Be(100m);
        dashboard.Value.AverageNps.Should().Be(100m);
        dashboard.Value.TopSurveys.Should().ContainSingle();
        dashboard.Value.TopSurveys[0].SurveyCode.Should().Be(survey.Code);
        dashboard.Value.TopSurveys[0].CompletedSessions.Should().Be(2);

        // روند زمانی باید هر دو ارسال را در یک نقطه تجمعی نشان دهد.
        dashboard.Value.Trend.Should().NotBeEmpty();
        dashboard.Value.Trend[^1].CumulativeCount.Should().Be(2);
    }

    [Fact]
    public async Task Trend_Groups_Submissions_By_Period()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A");
        await SubmitResponseAsync(env, survey.Id, questionnaire, 10, 5, 7, "B");

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var day = await service.GetTrendAsync(new TrendRequest
        {
            SurveyId = survey.Id,
            Period = AnalyticsPeriod.Day
        });

        day.IsSuccess.Should().BeTrue();
        day.Value.Should().HaveCount(1, "هر دو ارسال در یک روز انجام شده‌اند");
        day.Value[0].Count.Should().Be(2);
        day.Value[0].CumulativeCount.Should().Be(2);
    }

    // --- جستجو --------------------------------------------------------------------

    [Fact]
    public async Task Search_Filters_Metrics_By_Survey_And_Segment()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A");

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        await service.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        var bySurvey = await service.SearchAsync(new AnalyticsSearchRequest { SurveyId = survey.Id });

        bySurvey.TotalCount.Should().Be(1);
        bySurvey.Items[0].SurveyCode.Should().Be(survey.Code);
        bySurvey.Items[0].SegmentType.Should().Be(AnalyticsSegment.Survey);
        bySurvey.Items[0].CompletedSessions.Should().Be(1);

        var bySegment = await service.SearchAsync(new AnalyticsSearchRequest
        {
            SurveyId = survey.Id,
            SegmentType = AnalyticsSegment.OrgUnit
        });

        bySegment.TotalCount.Should().Be(0, "بخش‌بندی سازمانی محاسبه نشده است");

        var otherSurvey = await service.SearchAsync(new AnalyticsSearchRequest { SurveyId = Guid.NewGuid() });
        otherSurvey.TotalCount.Should().Be(0);
    }

    // --- محاسبه‌ی خودکار پس از ارسال ------------------------------------------------

    [Fact]
    public async Task Response_Submission_Automatically_Computes_Analytics()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        var responseService = env.Services.GetRequiredService<IResponseService>();

        var start = await responseService.StartSessionAsync(new StartSessionRequest { SurveyId = survey.Id });
        start.IsSuccess.Should().BeTrue();

        var context = await responseService.GetRespondentContextAsync(survey.Id);
        var choiceItem = context.Value!.Sections.SelectMany(s => s.Items).First(i => i.QuestionCode == ChoiceCode);
        var optionId = choiceItem.Options.Single(o => o.Code == "A").Id;

        var npsItem = questionnaire.Sections[0].Items.First(i => i.QuestionCode == NpsCode);
        var csatItem = questionnaire.Sections[0].Items.First(i => i.QuestionCode == CsatCode);
        var cesItem = questionnaire.Sections[0].Items.First(i => i.QuestionCode == CesCode);
        var submitChoiceItem = questionnaire.Sections[0].Items.First(i => i.QuestionCode == ChoiceCode);

        await responseService.SubmitAsync(start.Value!.Id, new SubmitResponseRequest
        {
            Answers =
            [
                new SaveAnswerRequest { QuestionnaireItemId = npsItem.Id, NumericValue = 9 },
                new SaveAnswerRequest { QuestionnaireItemId = csatItem.Id, NumericValue = 5 },
                new SaveAnswerRequest { QuestionnaireItemId = cesItem.Id, NumericValue = 6 },
                new SaveAnswerRequest { QuestionnaireItemId = submitChoiceItem.Id, SelectedOptionIds = [optionId] }
            ]
        });

        // شنونده‌ی تحلیلات باید عکس‌العمل را به‌صورت خودکار ساخته باشد.
        var rows = await env.AnalyticsDbContext.SurveyMetrics.ToListAsync();

        rows.Should().ContainSingle();
        rows[0].SurveyId.Should().Be(survey.Id);
        rows[0].CompletedSessions.Should().Be(1);
        rows[0].NpsScore.Should().Be(100m, "یک پاسخ ۹ = یک ترویج‌کننده");

        // رویداد محاسبه نیز باید شنونده‌ی ممیزی را فعال کرده باشد.
        var auditService = env.Services.GetRequiredService<IAuditService>();
        var auditEntries = await auditService.SearchAsync(new AuditSearchRequest("survey_metric", null, null, null, null));

        auditEntries.Should().Contain(e => e.EntityType == "survey_metric" && e.Action == "compute");
    }

    // --- بنچمارک‌ها ---------------------------------------------------------------

    [Fact]
    public async Task Compare_With_Benchmarks_Matches_Actual_Against_Target()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, questionnaire, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        await SubmitResponseAsync(env, survey.Id, questionnaire, 9, 5, 6, "A");
        await SubmitResponseAsync(env, survey.Id, questionnaire, 10, 5, 7, "B");

        var benchmarkService = env.Services.GetRequiredService<IBenchmarkService>();

        await benchmarkService.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف NPS سازمانی",
            Metric = MetricType.Nps,
            TargetValue = 50m,
            IsCompanyWide = true
        });

        var analyticsService = env.Services.GetRequiredService<IAnalyticsService>();

        await analyticsService.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        var comparisons = await analyticsService.CompareWithBenchmarksAsync(survey.Id);

        comparisons.IsSuccess.Should().BeTrue();
        comparisons.Value.Should().NotBeEmpty();

        var npsComparison = comparisons.Value.Single(c => c.Metric == MetricType.Nps);
        npsComparison.ActualValue.Should().Be(100m);
        npsComparison.TargetValue.Should().Be(50m);
        npsComparison.Delta.Should().Be(50m);
        npsComparison.IsAboveTarget.Should().BeTrue();
    }

    [Fact]
    public async Task Compute_Fails_For_Archived_Survey()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, _, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        var surveyService = env.Services.GetRequiredService<ISurveyService>();
        await surveyService.CloseAsync(survey.Id);
        await surveyService.ArchiveAsync(survey.Id);

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var result = await service.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("survey_archived");
    }

    [Fact]
    public async Task Compute_Returns_Empty_Result_When_No_Responses()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var (survey, _, _) = await SeedSurveyWithMetricQuestionsAsync(env);

        var service = env.Services.GetRequiredService<IAnalyticsService>();

        var result = await service.ComputeAsync(new ComputeAnalyticsRequest { SurveyId = survey.Id });

        result.IsSuccess.Should().BeTrue();
        result.Value!.CompletedSessions.Should().Be(0);
        result.Value.Questions.Should().BeEmpty();

        // نظرسنجی بدون پاسخ نباید ردیف تحلیلی بسازد.
        var rows = await env.AnalyticsDbContext.SurveyMetrics.CountAsync();
        rows.Should().Be(0);
    }
}
