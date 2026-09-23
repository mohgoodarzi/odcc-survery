using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Infrastructure.Modules.QuestionBank.Persistence;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.QuestionBank;

/// <summary>
/// آزمون‌های سرویس کتابخانه‌ی سؤالات: ایجاد، ویرایش، جستجو،
/// بایگانی و نسخه‌برداری.
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ
/// پایگاه‌داده‌ی واقعی را لمس نمی‌کنند.
/// </summary>
public class QuestionServiceTests
{
    private static SaveQuestionRequest CreateChoiceRequest(string code = "QB-ENG-001") => new()
    {
        Code = code,
        Type = QuestionType.SingleChoice,
        Localizations =
        [
            new QuestionLocalizationDto { Language = Language.Fa, Text = "میزان رضایت شما چقدر است؟" },
            new QuestionLocalizationDto { Language = Language.En, Text = "How satisfied are you?" }
        ],
        Options =
        [
            new SaveQuestionOptionRequest
            {
                Code = "A",
                DisplayOrder = 0,
                Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "کم" } ]
            },
            new SaveQuestionOptionRequest
            {
                Code = "B",
                DisplayOrder = 1,
                Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "زیاد" } ]
            }
        ],
        Tags = ["رضایت‌سنجی", "محیط کار"]
    };

    [Fact]
    public async Task Create_Creates_Question_With_First_Version_And_Options()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        var result = await service.CreateAsync(CreateChoiceRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("QB-ENG-001");
        result.Value.Type.Should().Be(QuestionType.SingleChoice);
        result.Value.CurrentVersionNumber.Should().Be(1);
        result.Value.Text.Should().Be("میزان رضایت شما چقدر است؟");
        result.Value.Options.Should().HaveCount(2);
        result.Value.Tags.Should().BeEquivalentTo(["رضایت‌سنجی", "محیط کار"]);

        // نسخه‌ی ۱ باید با تصویر لحظه‌ای ثبت شده باشد.
        var versions = await service.GetVersionsAsync(result.Value.Id);
        versions.IsSuccess.Should().BeTrue();
        versions.Value.Should().ContainSingle().Which.VersionNumber.Should().Be(1);

        var entity = await env.QuestionBankDbContext.Questions
            .Include(q => q.Options).ThenInclude(o => o.Localizations)
            .Include(q => q.Tags)
            .SingleAsync(q => q.Id == result.Value.Id);

        entity.Options.Should().HaveCount(2);
        entity.Options.Select(o => o.Code).Should().BeEquivalentTo(["A", "B"]);
        entity.Options.SelectMany(o => o.Localizations).Should().HaveCount(2);
        entity.Tags.Should().HaveCount(2);
        entity.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task Create_Rejects_Duplicate_Code()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        await service.CreateAsync(CreateChoiceRequest());

        var second = await service.CreateAsync(CreateChoiceRequest());

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("question_code_taken");
    }

    [Fact]
    public async Task Create_Rejects_Choice_Question_Without_Two_Options()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        var request = CreateChoiceRequest();
        request = request with { Options = [request.Options[0]] };

        var result = await service.CreateAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("question_needs_options");
    }

    [Fact]
    public async Task Create_Rejects_Options_On_Non_Choice_Question()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        var request = CreateChoiceRequest() with { Type = QuestionType.LongText };

        var result = await service.CreateAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("question_no_options_allowed");
    }

    [Fact]
    public async Task GetById_Returns_Question_With_Persian_Text()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        var created = (await service.CreateAsync(CreateChoiceRequest())).Value!;

        var result = await service.GetByIdAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Text.Should().Be("میزان رضایت شما چقدر است؟");
        result.Value.Localizations.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetById_Returns_Failure_For_Missing_Question()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("question_not_found");
    }

    [Fact]
    public async Task Update_Changing_Content_Creates_New_Version()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        var created = (await service.CreateAsync(CreateChoiceRequest())).Value!;
        var update = CreateChoiceRequest() with
        {
            Localizations =
            [
                new QuestionLocalizationDto { Language = Language.Fa, Text = "میزان رضایت شما از محیط کار چقدر است؟" },
                new QuestionLocalizationDto { Language = Language.En, Text = "How satisfied are you?" }
            ]
        };

        var result = await service.UpdateAsync(created.Id, update);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentVersionNumber.Should().Be(2, "متن سؤال تغییر کرده است");
        result.Value.Text.Should().Contain("محیط کار");

        var versions = await service.GetVersionsAsync(created.Id);
        versions.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Update_Without_Content_Change_Does_Not_Create_Version()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        var created = (await service.CreateAsync(CreateChoiceRequest())).Value!;

        // تغییر فقط برچسب‌ها — محتوای سؤال (نوع، طیف، متن‌ها، گزینه‌ها) ثابت می‌ماند.
        var update = CreateChoiceRequest() with { Tags = ["حوزه‌ی جدید"] };

        var result = await service.UpdateAsync(created.Id, update);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentVersionNumber.Should().Be(1, "محتوای سؤال تغییر نکرده است");

        var versions = await service.GetVersionsAsync(created.Id);
        versions.Value.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_Replaces_And_Removes_Options_And_Tags()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        var created = (await service.CreateAsync(CreateChoiceRequest())).Value!;

        var firstOptionId = created.Options[0].Id;
        var removedOptionId = created.Options[1].Id;

        var update = CreateChoiceRequest() with
        {
            Options =
            [
                // گزینه‌ی موجود به‌روزرسانی می‌شود.
                new SaveQuestionOptionRequest
                {
                    Id = firstOptionId,
                    Code = "A",
                    DisplayOrder = 0,
                    Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "اصلاً راضی نیستم" } ]
                },
                // گزینه‌ی جدید اضافه می‌شود.
                new SaveQuestionOptionRequest
                {
                    Code = "C",
                    DisplayOrder = 1,
                    Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "متوسط" } ]
                }
            ],
            Tags = ["رضایت‌سنجی"]
        };

        var result = await service.UpdateAsync(created.Id, update);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Options.Should().HaveCount(2);
        result.Value.Options.Select(o => o.Code).Should().BeEquivalentTo(["A", "C"]);
        result.Value.Options.First(o => o.Code == "A").Text.Should().Be("اصلاً راضی نیستم");
        result.Value.Options.Any(o => o.Id == removedOptionId).Should().BeFalse("گزینه‌ی حذف‌شده نباید بازگردانده شود");
        result.Value.Tags.Should().ContainSingle().Which.Should().Be("رضایت‌سنجی");

        var entity = await env.QuestionBankDbContext.Questions
            .Include(q => q.Options).ThenInclude(o => o.Localizations)
            .Include(q => q.Tags)
            .SingleAsync(q => q.Id == created.Id);

        entity.Options.Should().HaveCount(2);
        entity.Tags.Should().ContainSingle();
    }

    [Fact]
    public async Task Update_Returns_Failure_For_Missing_Question()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        var result = await service.UpdateAsync(Guid.NewGuid(), CreateChoiceRequest());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("question_not_found");
    }

    [Fact]
    public async Task Archive_Marks_Question_As_Archived()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        var created = (await service.CreateAsync(CreateChoiceRequest())).Value!;

        var archiveResult = await service.ArchiveAsync(created.Id);
        archiveResult.IsSuccess.Should().BeTrue();

        var entity = await env.QuestionBankDbContext.Questions.SingleAsync(q => q.Id == created.Id);
        entity.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task Search_Filters_By_Archived_Type_And_Tag()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        await service.CreateAsync(CreateChoiceRequest("QB-001"));
        await service.CreateAsync(CreateChoiceRequest("QB-002") with { Tags = ["حوزه‌ی متفاوت"] });

        var rating = CreateChoiceRequest("QB-003") with
        {
            Type = QuestionType.Rating,
            Options = []
        };
        await service.CreateAsync(rating);
        await service.ArchiveAsync((await service.CreateAsync(CreateChoiceRequest("QB-004"))).Value!.Id);

        // به‌طور پیش‌فرض، سؤال‌های بایگانی‌شده مخفی هستند.
        var defaultResult = await service.SearchAsync(new QuestionSearchRequest());
        defaultResult.TotalCount.Should().Be(3);
        defaultResult.Items.Should().NotContain(q => q.Code == "QB-004");

        // شامل بایگانی‌شده‌ها.
        var archivedResult = await service.SearchAsync(new QuestionSearchRequest { IncludeArchived = true });
        archivedResult.TotalCount.Should().Be(4);

        // فیلتر بر اساس نوع.
        var typeResult = await service.SearchAsync(new QuestionSearchRequest { Type = QuestionType.Rating });
        typeResult.TotalCount.Should().Be(1);
        typeResult.Items.Should().ContainSingle().Which.Code.Should().Be("QB-003");

        // فیلتر بر اساس برچسب.
        var tagResult = await service.SearchAsync(new QuestionSearchRequest { Tag = "حوزه‌ی متفاوت" });
        tagResult.TotalCount.Should().Be(1);
        tagResult.Items.Should().ContainSingle().Which.Code.Should().Be("QB-002");

        // جستجوی متنی روی کد.
        var textResult = await service.SearchAsync(new QuestionSearchRequest { SearchText = "QB-002" });
        textResult.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Search_Paginates_Results()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        for (var i = 0; i < 5; i++)
        {
            await service.CreateAsync(CreateChoiceRequest($"QB-PAGE-{i:D2}"));
        }

        var result = await service.SearchAsync(new QuestionSearchRequest { Page = 1, PageSize = 2 });

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Create_Writes_Audit_Entry_Via_Domain_Event()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionService>();

        await service.CreateAsync(CreateChoiceRequest());

        // رویداد دامنه باید از طریق مرز مجاز ماژول‌ها ممیزی ثبت کرده باشد.
        var auditService = env.Services.GetRequiredService<IAuditService>();
        var entries = await auditService.SearchAsync(new AuditSearchRequest("question", null, null, null, null));

        entries.Should().ContainSingle().Which.Action.Should().Be("create");
    }
}
