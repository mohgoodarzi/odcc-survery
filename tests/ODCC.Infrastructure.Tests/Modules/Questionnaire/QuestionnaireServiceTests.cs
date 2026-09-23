using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Questionnaire.Enums;
using ODCC.Infrastructure.Modules.Questionnaire.Persistence;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Questionnaire;

/// <summary>
/// آزمون‌های سرویس پرسشنامه‌ها: ایجاد، ویرایش، انتشار، بایگانی، حذف،
/// انشعاب و چسبیدن به نسخه‌ی سؤال.
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ
/// پایگاه‌داده‌ی واقعی را لمس نمی‌کنند.
/// </summary>
public class QuestionnaireServiceTests
{
    /// <summary>ساخت یک درخواست پرسشنامه‌ی نمونه با یک بخش و دو آیتم.</summary>
    private static SaveQuestionnaireRequest CreateRequest(
        string code,
        Guid firstQuestionId,
        Guid? secondQuestionId = null,
        Guid? firstSectionId = null,
        Guid? firstItemId = null,
        Guid? secondItemId = null,
        IReadOnlyList<SaveBranchingRuleRequest>? firstItemRules = null)
    {
        var items = new List<SaveItemRequest>
        {
            new() { Id = firstItemId, QuestionId = firstQuestionId, IsRequired = true, BranchingRules = firstItemRules ?? [] }
        };

        if (secondQuestionId is { } q2)
        {
            items.Add(new() { Id = secondItemId, QuestionId = q2, IsRequired = false });
        }

        return new SaveQuestionnaireRequest
        {
            Code = code,
            Localizations =
            [
                new QuestionnaireLocalizationDto { Language = Language.Fa, Title = "پرسشنامه‌ی محیط کار" },
                new QuestionnaireLocalizationDto { Language = Language.En, Title = "Workplace questionnaire" }
            ],
            Sections =
            [
                new SaveSectionRequest
                {
                    Id = firstSectionId,
                    IsOptional = false,
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش اول" } ],
                    Items = items
                }
            ]
        };
    }

    /// <summary>ایجاد چند سؤال گزینه‌ای در کتابخانه‌ی سؤالات.</summary>
    private static async Task<List<QuestionDto>> SeedQuestionsAsync(TestEnvironment env, int count)
    {
        var service = env.Services.GetRequiredService<IQuestionService>();
        var questions = new List<QuestionDto>();

        for (var i = 0; i < count; i++)
        {
            var request = new SaveQuestionRequest
            {
                Code = $"QB-Q-{i:D2}",
                Type = QuestionType.SingleChoice,
                Localizations =
                [
                    new QuestionLocalizationDto { Language = Language.Fa, Text = $"سؤال {i + 1}" },
                    new QuestionLocalizationDto { Language = Language.En, Text = $"Question {i + 1}" }
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
                ],
                Tags = ["محیط کار"]
            };

            var result = await service.CreateAsync(request);
            result.IsSuccess.Should().BeTrue("سؤال نمونه باید ساخته شود");
            questions.Add(result.Value!);
        }

        return questions;
    }

    [Fact]
    public async Task Create_Creates_Questionnaire_With_Sections_And_Items()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 2);

        var result = await service.CreateAsync(CreateRequest("QS-001", questions[0].Id, questions[1].Id));

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("QS-001");
        result.Value.Status.Should().Be(QuestionnaireStatus.Draft);
        result.Value.Version.Should().Be(1);
        result.Value.SectionCount.Should().Be(1);
        result.Value.ItemCount.Should().Be(2);
        result.Value.Title.Should().Be("پرسشنامه‌ی محیط کار");

        // آیتم جدید باید به آخرین نسخه‌ی سؤال چسبیده شود.
        result.Value.Sections.Should().ContainSingle().Which.Items[0]
            .QuestionVersionNumber.Should().Be(questions[0].CurrentVersionNumber);

        var entity = await env.QuestionnaireDbContext.Questionnaires
            .Include(q => q.Sections).ThenInclude(s => s.Items)
            .SingleAsync(q => q.Id == result.Value.Id);

        entity.Sections.Should().ContainSingle();
        entity.Sections[0].Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_Rejects_Duplicate_Code()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 1);

        await service.CreateAsync(CreateRequest("QS-DUP", questions[0].Id));

        var second = await service.CreateAsync(CreateRequest("QS-DUP", questions[0].Id));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("questionnaire_code_taken");
    }

    [Fact]
    public async Task Create_Rejects_Item_Referencing_Unknown_Question()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();

        var request = CreateRequest("QS-UNKNOWN", firstQuestionId: Guid.NewGuid());

        var result = await service.CreateAsync(request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("question_not_found");
    }

    [Fact]
    public async Task Update_Adds_New_Section_And_Item_To_Existing_Questionnaire()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 2);

        var created = (await service.CreateAsync(CreateRequest("QS-UPD", questions[0].Id))).Value!;
        var existingSectionId = created.Sections[0].Id;
        var existingItemId = created.Sections[0].Items[0].Id;

        // بخش دوم با آیتم جدید اضافه می‌شود (بخش اول نگه داشته می‌شود).
        var update = CreateRequest("QS-UPD", questions[0].Id, questions[1].Id, existingSectionId, existingItemId);
        update = update with
        {
            Sections =
            [
                update.Sections[0],
                new SaveSectionRequest
                {
                    Id = null,
                    IsOptional = true,
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش دوم" } ],
                    Items =
                    [
                        new SaveItemRequest { Id = null, QuestionId = questions[1].Id, IsRequired = true }
                    ]
                }
            ]
        };

        var result = await service.UpdateAsync(created.Id, update);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Sections.Should().HaveCount(2);
        result.Value.ItemCount.Should().Be(3);
        result.Value.Sections.Should().Contain(s => s.Title == "بخش دوم");

        var entity = await env.QuestionnaireDbContext.Questionnaires
            .Include(q => q.Sections).ThenInclude(s => s.Items)
            .SingleAsync(q => q.Id == created.Id);

        entity.Sections.Should().HaveCount(2);
        entity.Sections.SelectMany(s => s.Items).Should().HaveCount(3);
    }

    [Fact]
    public async Task Update_Preserves_Version_Pin_When_Question_Evolved()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var questionService = env.Services.GetRequiredService<IQuestionService>();
        var questionnaireService = env.Services.GetRequiredService<IQuestionnaireService>();

        var question = (await questionService.CreateAsync(new SaveQuestionRequest
        {
            Code = "QB-PIN-001",
            Type = QuestionType.SingleChoice,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "متن اولیه" } ],
            Options =
            [
                new SaveQuestionOptionRequest { Code = "A", DisplayOrder = 0,
                    Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "کم" } ] },
                new SaveQuestionOptionRequest { Code = "B", DisplayOrder = 1,
                    Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "زیاد" } ] }
            ]
        })).Value!;

        var pinnedVersion = question.CurrentVersionNumber;

        var created = (await questionnaireService.CreateAsync(
            CreateRequest("QS-PIN", question.Id))).Value!;
        var sectionId = created.Sections[0].Id;
        var itemId = created.Sections[0].Items[0].Id;

        // سؤال طیف جدیدی می‌گیرد (نسخه‌ی ۲ ساخته می‌شود).
        var evolved = await questionService.UpdateAsync(question.Id, new SaveQuestionRequest
        {
            Code = question.Code,
            Type = QuestionType.SingleChoice,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "متن تغییر یافته" } ],
            Options =
            [
                new SaveQuestionOptionRequest { Code = "A", DisplayOrder = 0,
                    Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "کم" } ] },
                new SaveQuestionOptionRequest { Code = "B", DisplayOrder = 1,
                    Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "زیاد" } ] }
            ]
        });
        evolved.Value!.CurrentVersionNumber.Should().Be(pinnedVersion + 1);

        // ویرایش آیتم موجود (بدون تغییر سؤال) باید نسخه‌ی چسبیده را حفظ کند.
        var update = CreateRequest("QS-PIN", question.Id, firstSectionId: sectionId, firstItemId: itemId);

        var result = await questionnaireService.UpdateAsync(created.Id, update);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Sections[0].Items[0].QuestionVersionNumber.Should().Be(pinnedVersion,
            "آیتم موجود باید به همان نسخه‌ای که در زمان ساخت چسبیده بود باقی بماند");
    }

    [Fact]
    public async Task Update_Rejects_Active_Questionnaire()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 1);

        var created = (await service.CreateAsync(CreateRequest("QS-ACTIVE", questions[0].Id))).Value!;
        (await service.PublishAsync(created.Id)).IsSuccess.Should().BeTrue();

        var result = await service.UpdateAsync(created.Id, CreateRequest("QS-ACTIVE", questions[0].Id));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("questionnaire_is_active");
    }

    [Fact]
    public async Task Publish_Sets_Active_And_Increments_Version()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 1);

        var created = (await service.CreateAsync(CreateRequest("QS-PUB", questions[0].Id))).Value!;

        var result = await service.PublishAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(QuestionnaireStatus.Active);
        result.Value.Version.Should().Be(2);
    }

    [Fact]
    public async Task Publish_Rejects_Questionnaire_Without_Items()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 1);

        var request = CreateRequest("QS-EMPTY", questions[0].Id);
        request = request with
        {
            Sections =
            [
                new SaveSectionRequest
                {
                    Id = Guid.CreateVersion7(),
                    IsOptional = false,
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش خالی" } ],
                    Items = []
                }
            ]
        };

        var created = (await service.CreateAsync(request)).Value!;

        var result = await service.PublishAsync(created.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("questionnaire_not_publishable");
    }

    [Fact]
    public async Task Archive_Sets_Status_Archived()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 1);

        var created = (await service.CreateAsync(CreateRequest("QS-ARCH", questions[0].Id))).Value!;

        var result = await service.ArchiveAsync(created.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(QuestionnaireStatus.Archived);
    }

    [Fact]
    public async Task Delete_Removes_Draft_But_Rejects_Published()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 2);

        var draft = (await service.CreateAsync(CreateRequest("QS-DEL-1", questions[0].Id))).Value!;
        var published = (await service.CreateAsync(CreateRequest("QS-DEL-2", questions[1].Id))).Value!;
        (await service.PublishAsync(published.Id)).IsSuccess.Should().BeTrue();

        var deleteDraft = await service.DeleteAsync(draft.Id);
        deleteDraft.IsSuccess.Should().BeTrue();

        var deletePublished = await service.DeleteAsync(published.Id);
        deletePublished.IsFailure.Should().BeTrue();
        deletePublished.Error.Code.Should().Be("questionnaire_not_deletable");
    }

    [Fact]
    public async Task Update_Rejects_Branching_Rule_Targeting_Self()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 1);

        var itemId = Guid.CreateVersion7();
        var request = CreateRequest(
            "QS-BR-SELF",
            questions[0].Id,
            firstItemId: itemId,
            firstItemRules:
            [
                new SaveBranchingRuleRequest
                {
                    TargetItemId = itemId, // انشعاب به خود آیتم بی‌معناست.
                    Condition = BranchingCondition.Equals,
                    ExpectedValue = "A"
                }
            ]);

        var created = await service.CreateAsync(request);

        created.IsFailure.Should().BeTrue();
        created.Error.Code.Should().Be("branching_target_invalid");
    }

    [Fact]
    public async Task Update_Rejects_Branching_Rule_With_Invalid_Option_Value()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 2);

        var firstItemId = Guid.CreateVersion7();
        var secondItemId = Guid.CreateVersion7();
        var request = CreateRequest(
            "QS-BR-VAL",
            questions[0].Id,
            questions[1].Id,
            firstItemId: firstItemId,
            secondItemId: secondItemId,
            firstItemRules:
            [
                new SaveBranchingRuleRequest
                {
                    TargetItemId = secondItemId,
                    Condition = BranchingCondition.Equals,
                    ExpectedValue = "Z" // کد گزینه‌ی مجاز فقط A یا B است.
                }
            ]);

        var created = await service.CreateAsync(request);

        created.IsFailure.Should().BeTrue();
        created.Error.Code.Should().Be("branching_value_invalid");
    }

    [Fact]
    public async Task Update_Accepts_Valid_Branching_Rule()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 2);

        var firstItemId = Guid.CreateVersion7();
        var secondItemId = Guid.CreateVersion7();
        var request = CreateRequest(
            "QS-BR-OK",
            questions[0].Id,
            questions[1].Id,
            firstItemId: firstItemId,
            secondItemId: secondItemId,
            firstItemRules:
            [
                new SaveBranchingRuleRequest
                {
                    TargetItemId = secondItemId,
                    Condition = BranchingCondition.Equals,
                    ExpectedValue = "A"
                }
            ]);

        var result = await service.CreateAsync(request);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Sections[0].Items.First(i => i.Id == firstItemId).BranchingRules
            .Should().ContainSingle()
            .Which.TargetItemId.Should().Be(secondItemId);
    }

    [Fact]
    public async Task Search_Filters_By_Status()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 2);

        var first = (await service.CreateAsync(CreateRequest("QS-S-1", questions[0].Id))).Value!;
        await service.CreateAsync(CreateRequest("QS-S-2", questions[1].Id));
        (await service.PublishAsync(first.Id)).IsSuccess.Should().BeTrue();

        var activeResult = await service.SearchAsync(new QuestionnaireSearchRequest { Status = QuestionnaireStatus.Active });
        activeResult.TotalCount.Should().Be(1);
        activeResult.Items.Should().ContainSingle().Which.Code.Should().Be("QS-S-1");

        var draftResult = await service.SearchAsync(new QuestionnaireSearchRequest { Status = QuestionnaireStatus.Draft });
        draftResult.TotalCount.Should().Be(1);
        draftResult.Items.Should().ContainSingle().Which.Code.Should().Be("QS-S-2");
    }

    [Fact]
    public async Task Search_Paginates_Results()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 1);

        for (var i = 0; i < 5; i++)
        {
            await service.CreateAsync(CreateRequest($"QS-PAGE-{i:D2}", questions[0].Id));
        }

        var result = await service.SearchAsync(new QuestionnaireSearchRequest { Page = 1, PageSize = 2 });

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Create_Writes_Audit_Entry_Via_Domain_Event()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var service = env.Services.GetRequiredService<IQuestionnaireService>();
        var questions = await SeedQuestionsAsync(env, 1);

        await service.CreateAsync(CreateRequest("QS-AUDIT", questions[0].Id));

        // رویداد دامنه باید از طریق مرز مجاز ماژول‌ها ممیزی ثبت کرده باشد.
        var auditService = env.Services.GetRequiredService<IAuditService>();
        var entries = await auditService.SearchAsync(new AuditSearchRequest("questionnaire", null, null, null, null));

        entries.Should().ContainSingle().Which.Action.Should().Be("create");
    }
}
