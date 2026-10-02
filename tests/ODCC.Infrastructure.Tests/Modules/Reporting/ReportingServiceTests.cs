using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Application.Modules.Reporting.Abstractions;
using ODCC.Application.Modules.Reporting.Dtos;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Reporting.Entities;
using ODCC.Domain.Modules.Reporting.Enums;
using ODCC.Infrastructure.Modules.Reporting.Persistence;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Reporting;

/// <summary>
/// آزمون‌های ماژول گزارش‌گیری: چرخه‌ی عمر تعاریف (ایجاد، ویرایش، فعال‌سازی،
/// بایگانی)، اجرای دستی و زمان‌بندی‌شده، نگه‌داری پنجره‌ی خروجی، دانلود
/// فایل produced و حریم خصوصی (خروجی فقط تجمع است).
///
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ
/// پایگاه‌داده‌ی واقعی را لمس نمی‌کنند. فایل‌های خروجی در یک شاخسه‌ی موقت
/// نوشته می‌شوند که در پایان آزمون پاک می‌شود.
/// </summary>
public class ReportingServiceTests
{
    /// <summary>ساخت تعریف گزارش ساده با نظرسنجی منتشرشده.</summary>
    private static async Task<Guid> SeedSurveyAsync(TestEnvironment env)
    {
        var questionService = env.Services.GetRequiredService<IQuestionService>();

        var nps = (await questionService.CreateAsync(new SaveQuestionRequest
        {
            Code = "QB-R-NPS",
            Type = QuestionType.Rating,
            ScaleMax = 10,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "احتمال پیشنهاد" } ]
        })).Value!;

        var questionnaireService = env.Services.GetRequiredService<IQuestionnaireService>();

        var created = await questionnaireService.CreateAsync(new SaveQuestionnaireRequest
        {
            Code = "QS-R-01",
            Localizations = [ new QuestionnaireLocalizationDto { Language = Language.Fa, Title = "پرسشنامه گزارش" } ],
            Sections =
            [
                new SaveSectionRequest
                {
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش اول" } ],
                    Items = [ new SaveItemRequest { QuestionId = nps.Id, IsRequired = true } ]
                }
            ]
        });

        created.IsSuccess.Should().BeTrue();
        var published = await questionnaireService.PublishAsync(created.Value!.Id);
        published.IsSuccess.Should().BeTrue();

        var surveyService = env.Services.GetRequiredService<ISurveyService>();

        var surveyCreated = await surveyService.CreateAsync(new SaveSurveyRequest
        {
            Code = "SV-R-01",
            QuestionnaireId = published.Value!.Id,
            Localizations = [ new SurveyLocalizationDto { Language = Language.Fa, Title = "نظرسنجی گزارش" } ]
        });

        surveyCreated.IsSuccess.Should().BeTrue();
        var surveyPublished = await surveyService.PublishAsync(surveyCreated.Value!.Id);
        surveyPublished.IsSuccess.Should().BeTrue();

        return surveyPublished.Value!.Id;
    }

    // --- چرخه‌ی عمر تعاریف ---------------------------------------------------

    [Fact]
    public async Task Create_Persists_Active_Definition_By_Default()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش ماهانه NPS",
            Description = "هدف: پایش رضایت مشتریان",
            Type = ReportType.SurveyAnalytics,
            Format = ReportFormat.Pdf,
            Schedule = ReportSchedule.Monthly,
            SurveyId = surveyId,
            ActivateImmediately = true
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ReportStatus.Active);
        result.Value.Schedule.Should().Be(ReportSchedule.Monthly);
        result.Value.NextRunAt.Should().NotBeNull();
        result.Value.OwnerUserName.Should().Be("testuser");

        var row = await env.ReportingDbContext.ReportDefinitions.SingleAsync();
        row.Name.Should().Be("گزارش ماهانه NPS");
        row.Status.Should().Be(ReportStatus.Active);
        row.SurveyId.Should().Be(surveyId);
        row.RetentionCount.Should().Be(10);
    }

    [Fact]
    public async Task Create_As_Draft_Does_Not_Schedule()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش پیش‌نویس",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId,
            ActivateImmediately = false
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ReportStatus.Draft);
        result.Value.NextRunAt.Should().BeNull();
    }

    [Fact]
    public async Task Create_Dashboard_Summary_Ignores_SurveyId()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.CreateAsync(new SaveReportRequest
        {
            Name = "خلاصه‌ی داشبورد",
            Type = ReportType.DashboardSummary,
            SurveyId = surveyId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.SurveyId.Should().BeNull();
    }

    [Fact]
    public async Task Update_Changes_Name_And_Schedule()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش اول",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        var updated = await service.UpdateAsync(created.Value!.Id, new SaveReportRequest
        {
            Name = "گزارش ویرایش‌شده",
            Description = "توضیح جدید",
            Type = ReportType.SurveyAnalytics,
            Format = ReportFormat.Excel,
            Schedule = ReportSchedule.Weekly,
            SurveyId = surveyId,
            ActivateImmediately = true,
            RetentionCount = 5
        });

        updated.IsSuccess.Should().BeTrue();
        updated.Value!.Name.Should().Be("گزارش ویرایش‌شده");
        updated.Value.Format.Should().Be(ReportFormat.Excel);
        updated.Value.Schedule.Should().Be(ReportSchedule.Weekly);
        updated.Value.RetentionCount.Should().Be(5);
        updated.Value.Status.Should().Be(ReportStatus.Active);
    }

    [Fact]
    public async Task Update_Of_Archived_Definition_Is_Rejected()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        (await service.ArchiveAsync(created.Value!.Id)).IsSuccess.Should().BeTrue();

        var updated = await service.UpdateAsync(created.Value.Id, new SaveReportRequest
        {
            Name = "گزارش جدید",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        updated.IsFailure.Should().BeTrue();
        updated.Error.Code.Should().Be("report_archived");
    }

    [Fact]
    public async Task Activate_Schedules_Next_Run()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش روزانه",
            Type = ReportType.SurveyAnalytics,
            Schedule = ReportSchedule.Daily,
            SurveyId = surveyId,
            ActivateImmediately = false
        });

        created.Value!.NextRunAt.Should().BeNull();

        var activated = await service.ActivateAsync(created.Value.Id);

        activated.IsSuccess.Should().BeTrue();
        activated.Value!.Status.Should().Be(ReportStatus.Active);
        activated.Value.NextRunAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Archive_Is_Idempotent()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        await service.ArchiveAsync(created.Value!.Id);
        var second = await service.ArchiveAsync(created.Value.Id);

        second.IsSuccess.Should().BeTrue();
        (await env.ReportingDbContext.ReportDefinitions.SingleAsync()).Status
            .Should().Be(ReportStatus.Archived);
    }

    [Fact]
    public async Task Search_Filters_By_Status_And_Text()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش فروش",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        var daily = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش روزانه عملیات",
            Type = ReportType.SurveyAnalytics,
            Schedule = ReportSchedule.Daily,
            SurveyId = surveyId
        });

        await service.ArchiveAsync(daily.Value!.Id);

        var activeOnly = await service.SearchAsync(new ReportSearchRequest { PageSize = 50 });

        activeOnly.TotalCount.Should().Be(1);
        activeOnly.Items.Single().Name.Should().Be("گزارش فروش");

        var archived = await service.SearchAsync(new ReportSearchRequest
        {
            Status = ReportStatus.Archived,
            PageSize = 50
        });

        archived.TotalCount.Should().Be(1);
        archived.Items.Single().Name.Should().Be("گزارش روزانه عملیات");

        var byText = await service.SearchAsync(new ReportSearchRequest
        {
            SearchText = "فروش",
            PageSize = 50
        });

        byText.TotalCount.Should().Be(1);
    }

    // --- اجرای گزارش -------------------------------------------------------

    [Fact]
    public async Task Execute_Produces_Pdf_Artifact()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش NPS",
            Type = ReportType.SurveyAnalytics,
            Format = ReportFormat.Pdf,
            SurveyId = surveyId
        });

        var executed = await service.ExecuteAsync(created.Value!.Id);

        if (executed.IsFailure)
        {
            Assert.Fail($"execution failed: {executed.Error.Code} — {executed.Error.Message}");
        }
        executed.Value!.FileSizeBytes.Should().BeGreaterThan(0);
        executed.Value.HasArtifact.Should().BeTrue();

        var row = await env.ReportingDbContext.ReportExecutions.SingleAsync();
        row.Status.Should().Be(ReportExecutionStatus.Succeeded);
        row.FilePath.Should().NotBeNullOrEmpty();
        row.TriggeredByName.Should().Be("testuser");

        var definition = await env.ReportingDbContext.ReportDefinitions.SingleAsync();
        definition.LastExecutedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Execute_Produces_Excel_Artifact()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش اکسل",
            Type = ReportType.SurveyAnalytics,
            Format = ReportFormat.Excel,
            SurveyId = surveyId
        });

        var executed = await service.ExecuteAsync(created.Value!.Id);

        if (executed.IsFailure)
        {
            Assert.Fail($"execution failed: {executed.Error.Code} — {executed.Error.Message}");
        }

        executed.Value!.FileName.Should().EndWith(".xlsx");

        // خروجی واقعی یک OOXML معتبر است: امضای ZIP باید در سرآغاز فایل باشد.
        var artifact = await service.GetArtifactAsync(executed.Value.Id);
        artifact.IsSuccess.Should().BeTrue();

        await using var stream = artifact.Value!.Content;
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer);

        var bytes = buffer.ToArray();
        bytes.Should().NotBeEmpty();
        bytes[0].Should().Be(0x50); // 'P'
        bytes[1].Should().Be(0x4B); // 'K'
    }

    [Fact]
    public async Task Execute_Dashboard_Summary_Without_Survey_Works()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "خلاصه‌ی داشبورد",
            Type = ReportType.DashboardSummary,
            Format = ReportFormat.Pdf
        });

        var executed = await service.ExecuteAsync(created.Value!.Id);

        executed.IsSuccess.Should().BeTrue();
        executed.Value!.Status.Should().Be(ReportExecutionStatus.Succeeded);
    }

    [Fact]
    public async Task GetData_Returns_Sections_For_Display()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش نمایش",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        var data = await service.GetDataAsync(created.Value!.Id);

        data.IsSuccess.Should().BeTrue();
        data.Value!.Title.Should().Be("گزارش نمایش");
        data.Value.Sections.Should().NotBeEmpty();

        // بخش شاخص‌های کلیدی باید ستون‌ها و ردیف‌های هم‌اندازه داشته باشد.
        var metrics = data.Value.Sections.First(s => s.Columns.Count == 2);
        metrics.Columns.Should().HaveCount(2);
        metrics.Rows.Should().NotBeEmpty();
        metrics.Rows.Should().AllSatisfy(row => row.Should().HaveCount(2));
    }

    [Fact]
    public async Task GetData_Matches_Rendered_Artifact()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش هم‌خوان",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        var executed = await service.ExecuteAsync(created.Value!.Id);
        executed.IsSuccess.Should().BeTrue();

        var data = await service.GetDataAsync(created.Value!.Id);

        // داده‌ی روی صفحه باید همان تعداد ردیفی را داشته که در فایل نوشته شده.
        data.IsSuccess.Should().BeTrue();
        data.Value!.Sections.Sum(s => s.Rows.Count).Should().Be(executed.Value!.RowCount);
    }

    [Fact]
    public async Task GetData_Of_Report_Outside_Scope_Is_Denied()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (reportId, visibleUnitId) = await SeedReportOutsideScopeAsync(env);
        env.SetCurrentUser(visibleUnitId, DataScope.Department);

        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.GetDataAsync(reportId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("report_not_found");
    }

    [Fact]
    public async Task Execute_Of_Archived_Definition_Is_Rejected()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        await service.ArchiveAsync(created.Value!.Id);

        var executed = await service.ExecuteAsync(created.Value.Id);

        executed.IsFailure.Should().BeTrue();
        executed.Error.Code.Should().Be("report_archived");
    }

    [Fact]
    public async Task Execute_Benchmark_Comparison_Without_Benchmarks_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش بنچمارک",
            Type = ReportType.BenchmarkComparison,
            SurveyId = surveyId
        });

        // بنچمارکی تعریف نشده است؛ بخش بنچمارک خالی می‌ماند ولی اجرا موفق است.
        var executed = await service.ExecuteAsync(created.Value!.Id);

        executed.IsSuccess.Should().BeTrue();
        executed.Value!.Status.Should().Be(ReportExecutionStatus.Succeeded);
    }

    [Fact]
    public async Task GetArtifact_Opens_Stored_File()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش دانلود",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        var executed = await service.ExecuteAsync(created.Value!.Id);

        var artifact = await service.GetArtifactAsync(executed.Value!.Id);

        artifact.IsSuccess.Should().BeTrue();
        artifact.Value!.FileName.Should().Be(executed.Value.FileName);
        artifact.Value.ContentType.Should().Be("application/pdf");
        artifact.Value.SizeBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetArtifact_Of_Pending_Execution_Is_Rejected()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        // یک ردیف اجرای دستی در حالت Pending (هنوز رندر نشده).
        env.ReportingDbContext.ReportExecutions.Add(new ReportExecution
        {
            ReportDefinitionId = created.Value!.Id,
            ReportName = created.Value.Name,
            Status = ReportExecutionStatus.Pending
        });

        await env.ReportingDbContext.SaveChangesAsync();

        var executionId = (await env.ReportingDbContext.ReportExecutions.FirstAsync()).Id;

        var artifact = await service.GetArtifactAsync(executionId);

        artifact.IsFailure.Should().BeTrue();
        artifact.Error.Code.Should().Be("artifact_unavailable");
    }

    // --- نگه‌داری پنجره‌ی خروجی -------------------------------------------------

    [Fact]
    public async Task Retention_Soft_Deletes_Old_Executions()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش نگه‌داری",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId,
            RetentionCount = 2
        });

        // چهار اجرای موفق; فقط دو تای اخیر باید نگه داشته شوند.
        for (var i = 0; i < 4; i++)
        {
            var step = await service.ExecuteAsync(created.Value!.Id);

            if (step.IsFailure)
            {
                Assert.Fail($"execution {i + 1} failed: {step.Error.Code} — {step.Error.Message}");
            }
        }

        var visible = await env.ReportingDbContext.ReportExecutions
            .Where(e => !e.IsDeleted)
            .ToListAsync();

        visible.Should().HaveCount(2);
        visible.Should().OnlyContain(e => e.Status == ReportExecutionStatus.Succeeded);
    }

    // --- زمان‌بند ---------------------------------------------------------------

    [Fact]
    public async Task ProcessDueReports_Skips_Not_Due_Definitions()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش ماهانه",
            Type = ReportType.SurveyAnalytics,
            Schedule = ReportSchedule.Monthly,
            SurveyId = surveyId
        });

        // اجرای بعدی یک ماه بعد است، پس اکنون نباید اجرا شود.
        var processed = await service.ProcessDueReportsAsync(DateTime.UtcNow);

        processed.Should().Be(0);
        (await env.ReportingDbContext.ReportExecutions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task ProcessDueReports_Runs_Overdue_Definitions()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش روزانه",
            Type = ReportType.SurveyAnalytics,
            Schedule = ReportSchedule.Daily,
            SurveyId = surveyId
        });

        // شبیه‌سازی اینکه زمان اجرا رسیده است.
        var overdue = await env.ReportingDbContext.ReportDefinitions.SingleAsync();
        overdue.NextRunAt = DateTime.UtcNow.AddDays(-2);

        await env.ReportingDbContext.SaveChangesAsync();

        var processed = await service.ProcessDueReportsAsync(DateTime.UtcNow);

        processed.Should().Be(1);
        (await env.ReportingDbContext.ReportExecutions.CountAsync(e => !e.IsDeleted)).Should().Be(1);

        // پس از اجرا، زمان اجرای بعدی به آینده منتقل شده است.
        var after = await env.ReportingDbContext.ReportDefinitions.SingleAsync();
        after.NextRunAt.Should().BeAfter(DateTime.UtcNow);
        after.LastExecutedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessDueReports_Ignores_Draft_Definitions()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش پیش‌نویس",
            Type = ReportType.SurveyAnalytics,
            Schedule = ReportSchedule.Daily,
            SurveyId = surveyId,
            ActivateImmediately = false
        });

        // حتی اگر NextRunAt در گذشته باشد، تعاریف پیش‌نویس اجرا نمی‌شوند.
        var draft = await env.ReportingDbContext.ReportDefinitions.SingleAsync();
        draft.NextRunAt = DateTime.UtcNow.AddDays(-1);
        await env.ReportingDbContext.SaveChangesAsync();

        var processed = await service.ProcessDueReportsAsync(DateTime.UtcNow);

        processed.Should().Be(0);
    }

    // --- حریم خصوصی -----------------------------------------------------------

    [Fact]
    public async Task Execution_Audit_Entries_Are_Logged()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);

        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش ممیزی",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        await service.ExecuteAsync(created.Value!.Id);

        // شنونده‌ی ممیزی باید برای ایجاد و اجرای گزارش رخداد ثبت کرده باشد.
        var auditService = env.Services.GetRequiredService<ODCC.Application.Modules.Audit.Abstractions.IAuditService>();

        var entries = await auditService.SearchAsync(new AuditSearchRequest(
            EntityType: null, Action: null, UserId: null,
            FromUtc: null, ToUtc: null, Page: 1, PageSize: 100));

        entries.Should().Contain(e => e.Action == "create" && e.EntityType == "report_definition");
        entries.Should().Contain(e => e.Action == "export" && e.EntityType == "report_execution");
    }

    // --- مرز سازمانی (fail-closed) --------------------------------------------

    /// <summary>
    /// ساختن گزارشی متعلق به زیردرخت /hq/ops/it، یعنی خارج از دامنه‌ی
    /// /hq/fin/ap که در آزمون‌ها به کاربر محدود می‌شود. کاربر جاری در پایان
    /// دامنه‌ی Company روی همان واحد است.
    /// </summary>
    /// <returns>(شناسه‌ی گزارش، شناسه‌ی واحد /hq/fin/ap)</returns>
    private static async Task<(Guid reportId, Guid visibleUnitId)> SeedReportOutsideScopeAsync(
        TestEnvironment env)
    {
        var surveyId = await SeedSurveyAsync(env);
        var ids = await env.SeedOrgHierarchyAsync();

        env.SetCurrentUser(ids.otherDepartmentId, DataScope.Company);
        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش واحد دیگر",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId,
            OrgUnitId = ids.otherDepartmentId
        });

        created.IsSuccess.Should().BeTrue();
        created.Value!.OrgUnitPath.Should().Be("/hq/ops/it");

        return (created.Value.Id, ids.departmentId);
    }

    [Fact]
    public async Task Department_Scope_GetById_Denies_Report_Outside_Scope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (reportId, visibleUnitId) = await SeedReportOutsideScopeAsync(env);
        env.SetCurrentUser(visibleUnitId, DataScope.Department);

        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.GetByIdAsync(reportId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("report_not_found");
    }

    [Fact]
    public async Task Department_Scope_Search_Excludes_Out_Of_Scope_Reports()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, visibleUnitId) = await SeedReportOutsideScopeAsync(env);
        env.SetCurrentUser(visibleUnitId, DataScope.Department);

        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.SearchAsync(new ReportSearchRequest { PageSize = 50 });

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Department_Scope_SearchExecutions_Excludes_Out_Of_Scope_Reports()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (reportId, visibleUnitId) = await SeedReportOutsideScopeAsync(env);

        env.ReportingDbContext.ReportExecutions.Add(new ReportExecution
        {
            ReportDefinitionId = reportId,
            ReportName = "گزارش واحد دیگر",
            Status = ReportExecutionStatus.Succeeded
        });
        await env.ReportingDbContext.SaveChangesAsync();

        env.SetCurrentUser(visibleUnitId, DataScope.Department);

        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.SearchExecutionsAsync(new ExecutionSearchRequest { PageSize = 50 });

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Department_Scope_GetExecution_Denies_Outside_Scope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (reportId, visibleUnitId) = await SeedReportOutsideScopeAsync(env);

        env.ReportingDbContext.ReportExecutions.Add(new ReportExecution
        {
            ReportDefinitionId = reportId,
            ReportName = "گزارش واحد دیگر",
            Status = ReportExecutionStatus.Succeeded
        });
        await env.ReportingDbContext.SaveChangesAsync();

        var executionId = (await env.ReportingDbContext.ReportExecutions.FirstAsync()).Id;

        env.SetCurrentUser(visibleUnitId, DataScope.Department);

        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.GetExecutionAsync(executionId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("execution_not_found");
    }

    [Fact]
    public async Task Department_Scope_GetArtifact_Denies_Outside_Scope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (reportId, visibleUnitId) = await SeedReportOutsideScopeAsync(env);

        env.ReportingDbContext.ReportExecutions.Add(new ReportExecution
        {
            ReportDefinitionId = reportId,
            ReportName = "گزارش واحد دیگر",
            Status = ReportExecutionStatus.Succeeded,
            FilePath = "2026-01/report.pdf",
            FileName = "report.pdf"
        });
        await env.ReportingDbContext.SaveChangesAsync();

        var executionId = (await env.ReportingDbContext.ReportExecutions.FirstAsync()).Id;

        env.SetCurrentUser(visibleUnitId, DataScope.Department);

        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.GetArtifactAsync(executionId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("execution_not_found");
    }

    [Fact]
    public async Task Department_Scope_Execute_Denies_Outside_Scope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (reportId, visibleUnitId) = await SeedReportOutsideScopeAsync(env);
        env.SetCurrentUser(visibleUnitId, DataScope.Department);

        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.ExecuteAsync(reportId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("report_not_found");

        // هیچ ردیف اجرایی نباید ساخته شده باشد.
        (await env.ReportingDbContext.ReportExecutions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Department_Scope_Update_Denies_Outside_Scope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (reportId, visibleUnitId) = await SeedReportOutsideScopeAsync(env);
        env.SetCurrentUser(visibleUnitId, DataScope.Department);

        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.UpdateAsync(reportId, new SaveReportRequest
        {
            Name = "تغییر نام غیرمجاز",
            Type = ReportType.SurveyAnalytics
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("report_not_found");

        var row = await env.ReportingDbContext.ReportDefinitions.SingleAsync();
        row.Name.Should().Be("گزارش واحد دیگر");
    }

    [Fact]
    public async Task Department_Scope_Activate_And_Archive_Deny_Outside_Scope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (reportId, visibleUnitId) = await SeedReportOutsideScopeAsync(env);
        env.SetCurrentUser(visibleUnitId, DataScope.Department);

        var service = env.Services.GetRequiredService<IReportingService>();

        (await service.ActivateAsync(reportId)).Error.Code.Should().Be("report_not_found");
        (await service.ArchiveAsync(reportId)).Error.Code.Should().Be("report_not_found");

        var row = await env.ReportingDbContext.ReportDefinitions.SingleAsync();
        // وضعیت کاملاً دست‌نخورده باقی مانده است (اینجا Active است چون با
        // ActivateImmediately ساخته شده).
        row.Status.Should().Be(ReportStatus.Active);
    }

    [Fact]
    public async Task Create_With_OrgUnit_Outside_Scope_Is_Denied()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);
        var ids = await env.SeedOrgHierarchyAsync();

        env.SetCurrentUser(ids.departmentId, DataScope.Department);
        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش متخلف",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId,
            OrgUnitId = ids.otherDepartmentId
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");
        (await env.ReportingDbContext.ReportDefinitions.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Create_Without_OrgUnit_Falls_Back_To_Anchor()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);
        var ids = await env.SeedOrgHierarchyAsync();

        env.SetCurrentUser(ids.departmentId, DataScope.Department);
        var service = env.Services.GetRequiredService<IReportingService>();

        var result = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش دامنه",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.OrgUnitId.Should().Be(ids.departmentId);
        result.Value.OrgUnitPath.Should().Be("/hq/fin/ap");
    }

    [Fact]
    public async Task Update_With_OrgUnit_Outside_Scope_Is_Denied()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);
        var ids = await env.SeedOrgHierarchyAsync();

        env.SetCurrentUser(ids.departmentId, DataScope.Department);
        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش دامنه",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });
        created.IsSuccess.Should().BeTrue();

        var result = await service.UpdateAsync(created.Value!.Id, new SaveReportRequest
        {
            Name = "گزارش دامنه",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId,
            OrgUnitId = ids.otherDepartmentId
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("access_denied");

        var row = await env.ReportingDbContext.ReportDefinitions.SingleAsync();
        row.OrgUnitPath.Should().Be("/hq/fin/ap");
    }

    [Fact]
    public async Task Department_Scope_Allows_Report_Inside_Scope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var surveyId = await SeedSurveyAsync(env);
        var ids = await env.SeedOrgHierarchyAsync();

        env.SetCurrentUser(ids.departmentId, DataScope.Department);
        var service = env.Services.GetRequiredService<IReportingService>();

        var created = await service.CreateAsync(new SaveReportRequest
        {
            Name = "گزارش داخل دامنه",
            Type = ReportType.SurveyAnalytics,
            SurveyId = surveyId
        });

        var fetched = await service.GetByIdAsync(created.Value!.Id);

        fetched.IsSuccess.Should().BeTrue();
        fetched.Value!.Name.Should().Be("گزارش داخل دامنه");
    }
}
