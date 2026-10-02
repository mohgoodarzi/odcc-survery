using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Questionnaire.Enums;
using ODCC.Domain.Modules.Survey.Enums;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Questionnaire;

/// <summary>
/// آزمون جریان کامل مدیریت پرسشنامه تا نظرسنجی — همان مسیری که مدیر در
/// رابط کاربری طی می‌کند:
///
/// ۱) مدیر سؤال‌ها را در کتابخانه می‌سازد (با گزینه و نوع).
/// ۲) پرسشنامه می‌سازد و سؤال‌ها را به آن اضافه می‌کند.
/// ۳) پرسشنامه را ذخیره و سپس منتشر می‌کند.
/// ۴) نظرسنجی می‌سازد و پرسشنامه را انتخاب می‌کند.
/// ۵) نظرسنجی پرسشنامه‌ی درست را بارگذاری می‌کند (شناسه، نسخه و کد).
///
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ
/// پایگاه‌داده‌ی واقعی را لمس نمی‌کنند.
/// </summary>
public class QuestionnaireSurveyFlowTests
{
    /// <summary>ساخت چند سؤال با انواع و گزینه‌های مختلف در کتابخانه‌ی سؤالات.</summary>
    private static async Task<List<QuestionDto>> SeedQuestionsAsync(TestEnvironment env)
    {
        var service = env.Services.GetRequiredService<IQuestionService>();
        var questions = new List<QuestionDto>();

        // سؤال گزینه‌ای.
        var choice = await service.CreateAsync(new SaveQuestionRequest
        {
            Code = "QB-CHOICE-01",
            Type = QuestionType.SingleChoice,
            Localizations =
            [
                new QuestionLocalizationDto { Language = Language.Fa, Text = "میزان رضایت" },
                new QuestionLocalizationDto { Language = Language.En, Text = "Satisfaction level" }
            ],
            Options =
            [
                new SaveQuestionOptionRequest
                {
                    Code = "LOW", DisplayOrder = 0,
                    Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "کم" } ]
                },
                new SaveQuestionOptionRequest
                {
                    Code = "HIGH", DisplayOrder = 1,
                    Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "زیاد" } ]
                }
            ],
            Tags = ["رضایت"]
        });
        choice.IsSuccess.Should().BeTrue("سؤال گزینه‌ای باید ساخته شود");
        questions.Add(choice.Value!);

        // سؤال امتیازدهی.
        var rating = await service.CreateAsync(new SaveQuestionRequest
        {
            Code = "QB-RATING-01",
            Type = QuestionType.Rating,
            ScaleMax = 10,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "امتیاز کلی" } ]
        });
        rating.IsSuccess.Should().BeTrue("سؤال امتیازدهی باید ساخته شود");
        questions.Add(rating.Value!);

        // سؤال متنی.
        var text = await service.CreateAsync(new SaveQuestionRequest
        {
            Code = "QB-TEXT-01",
            Type = QuestionType.LongText,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "توضیحات باز" } ]
        });
        text.IsSuccess.Should().BeTrue("سؤال متنی باید ساخته شود");
        questions.Add(text.Value!);

        return questions;
    }

    [Fact]
    public async Task Full_Flow_Admin_Creates_Questionnaire_Adds_Questions_Then_Survey_Uses_It()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var questionService = env.Services.GetRequiredService<IQuestionService>();
        var questionnaireService = env.Services.GetRequiredService<IQuestionnaireService>();
        var surveyService = env.Services.GetRequiredService<ISurveyService>();

        // --- ۱) مدیر سؤال‌ها را در کتابخانه می‌سازد ----------------------------
        var questions = await SeedQuestionsAsync(env);

        // --- ۲) مدیر پرسشنامه می‌سازد و سؤال‌ها را اضافه می‌کند ----------------
        var createRequest = new SaveQuestionnaireRequest
        {
            Code = "QS-FLOW-01",
            Localizations =
            [
                new QuestionnaireLocalizationDto { Language = Language.Fa, Title = "پرسشنامه‌ی رضایت سازمانی" },
                new QuestionnaireLocalizationDto { Language = Language.En, Title = "Organizational satisfaction" }
            ],
            Sections =
            [
                new SaveSectionRequest
                {
                    IsOptional = false,
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش اصلی" } ],
                    Items =
                    [
                        new SaveItemRequest
                        {
                            QuestionId = questions[0].Id,
                            IsRequired = true,
                            TitleOverride = "رضایت کلی شما"
                        },
                        new SaveItemRequest { QuestionId = questions[1].Id, IsRequired = true }
                    ]
                }
            ]
        };

        var created = await questionnaireService.CreateAsync(createRequest);

        // --- ۳) ذخیره‌سازی ساختار بررسی می‌شود --------------------------------
        created.IsSuccess.Should().BeTrue();
        created.Value!.Status.Should().Be(QuestionnaireStatus.Draft);
        created.Value.SectionCount.Should().Be(1);
        created.Value.ItemCount.Should().Be(2);
        created.Value.IsPublishable.Should().BeTrue("یک بخش با دو سؤال و عنوان دارد");

        // سؤال‌ها باید با نوع و گزینه‌های نسخه‌ی چسبیده ثبت شده باشند.
        var firstItem = created.Value.Sections[0].Items[0];
        firstItem.QuestionId.Should().Be(questions[0].Id);
        firstItem.QuestionType.Should().Be(QuestionType.SingleChoice);
        firstItem.QuestionCode.Should().Be("QB-CHOICE-01");
        firstItem.QuestionVersionNumber.Should().Be(questions[0].CurrentVersionNumber);
        firstItem.TitleOverride.Should().Be("رضایت کلی شما");

        // --- ۳-ب) افزودن سؤال دیگر از طریق ویرایش (یک بخش جدید) --------------
        // شناسه‌های بخش/آیتم‌های موجود ارسال می‌شوند تا سرویس آن‌ها را
        // در محل به‌روزرسانی کند (نه بازسازی) — دقیقاً مثل رابط کاربری.
        var existingSection = created.Value.Sections[0];

        var updateRequest = new SaveQuestionnaireRequest
        {
            Code = "QS-FLOW-01",
            Localizations = createRequest.Localizations,
            Sections =
            [
                new SaveSectionRequest
                {
                    Id = existingSection.Id,
                    IsOptional = existingSection.IsOptional,
                    Localizations = existingSection.Localizations
                        .Select(l => new SectionLocalizationDto { Language = l.Language, Title = l.Title })
                        .ToList(),
                    Items = existingSection.Items
                        .Select(i => new SaveItemRequest
                        {
                            Id = i.Id,
                            QuestionId = i.QuestionId,
                            IsRequired = i.IsRequired,
                            TitleOverride = i.TitleOverride,
                            BranchingRules = i.BranchingRules
                                .Select(r => new SaveBranchingRuleRequest
                                {
                                    Id = r.Id,
                                    TargetItemId = r.TargetItemId,
                                    Condition = r.Condition,
                                    ExpectedValue = r.ExpectedValue
                                })
                                .ToList()
                        })
                        .ToList()
                },
                new SaveSectionRequest
                {
                    IsOptional = true,
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش نظرات" } ],
                    Items = [ new SaveItemRequest { QuestionId = questions[2].Id, IsRequired = false } ]
                }
            ]
        };

        var updated = await questionnaireService.UpdateAsync(created.Value.Id, updateRequest);

        updated.IsSuccess.Should().BeTrue();
        updated.Value!.SectionCount.Should().Be(2);
        updated.Value.ItemCount.Should().Be(3);
        updated.Value.Sections[0].Items.Select(i => i.Id)
            .Should().BeEquivalentTo(existingSection.Items.Select(i => i.Id),
                "آیتم‌های موجود باید هنگام ویرایش حفظ شوند");

        // --- ۴) انتشار پرسشنامه (فعال‌سازی برای استفاده در نظرسنجی) ----------
        var published = await questionnaireService.PublishAsync(created.Value.Id);

        published.IsSuccess.Should().BeTrue();
        published.Value!.Status.Should().Be(QuestionnaireStatus.Active);
        published.Value.Version.Should().Be(2);

        // فقط پرسشنامه‌ی فعال در جستجوی نظرسنجی‌ها دیده می‌شود.
        var activeSearch = await questionnaireService.SearchAsync(
            new QuestionnaireSearchRequest { Status = QuestionnaireStatus.Active });
        activeSearch.Items.Should().ContainSingle().Which.Id.Should().Be(created.Value.Id);

        // --- ۵) مدیر نظرسنجی می‌سازد و پرسشنامه را انتخاب می‌کند --------------
        var surveyCreate = new SaveSurveyRequest
        {
            Code = "SV-FLOW-01",
            QuestionnaireId = created.Value.Id,
            EstimatedMinutes = 7,
            Localizations =
            [
                new SurveyLocalizationDto { Language = Language.Fa, Title = "نظرسنجی رضایت سازمانی" },
                new SurveyLocalizationDto { Language = Language.En, Title = "Satisfaction survey" }
            ]
        };

        var survey = await surveyService.CreateAsync(surveyCreate);

        // --- ۶) نظرسنجی پرسشنامه‌ی درست را بارگذاری می‌کند --------------------
        survey.IsSuccess.Should().BeTrue();
        survey.Value!.QuestionnaireId.Should().Be(created.Value.Id);
        survey.Value.QuestionnaireCode.Should().Be("QS-FLOW-01");
        survey.Value.QuestionnaireVersion.Should().Be(published.Value.Version,
            "نسخه‌ی پرسشنامه در زمان ایجاد نظرسنجیه ثبت می‌شود");

        // بارگذاری مجدد نظرسنجی هم باید همان پرسشنامه را نشان دهد.
        var reloaded = await surveyService.GetByIdAsync(survey.Value.Id);
        reloaded.IsSuccess.Should().BeTrue();
        reloaded.Value!.QuestionnaireId.Should().Be(created.Value.Id);
        reloaded.Value.QuestionnaireCode.Should().Be("QS-FLOW-01");
        reloaded.Value.QuestionnaireVersion.Should().Be(published.Value.Version);

        // ساختار پرسشنامه‌ی پیوندخورده باید کامل و قابل خواندن باشد.
        var linked = await questionnaireService.GetByIdAsync(reloaded.Value.QuestionnaireId);
        linked.IsSuccess.Should().BeTrue();
        linked.Value!.Sections.Should().HaveCount(2);
        linked.Value.ItemCount.Should().Be(3);
        linked.Value.Sections[0].Items.Should().HaveCount(2);
        linked.Value.Sections[0].Items[0].QuestionType.Should().Be(QuestionType.SingleChoice);

        // داده‌ها باید در پایگاه‌داده هم ذخیره شده باشند.
        var surveyEntity = await env.SurveyDbContext.Surveys
            .SingleAsync(s => s.Id == survey.Value.Id);
        surveyEntity.QuestionnaireId.Should().Be(created.Value.Id);
        surveyEntity.QuestionnaireCode.Should().Be("QS-FLOW-01");
    }

    [Fact]
    public async Task Archived_Questionnaire_Is_Not_Selectable_For_New_Surveys()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var questionnaireService = env.Services.GetRequiredService<IQuestionnaireService>();
        var surveyService = env.Services.GetRequiredService<ISurveyService>();
        var questions = await SeedQuestionsAsync(env);

        var created = await questionnaireService.CreateAsync(new SaveQuestionnaireRequest
        {
            Code = "QS-ARCH-FLOW",
            Localizations = [ new QuestionnaireLocalizationDto { Language = Language.Fa, Title = "پرسشنامه" } ],
            Sections =
            [
                new SaveSectionRequest
                {
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش" } ],
                    Items = [ new SaveItemRequest { QuestionId = questions[0].Id } ]
                }
            ]
        });
        created.IsSuccess.Should().BeTrue();
        (await questionnaireService.PublishAsync(created.Value!.Id)).IsSuccess.Should().BeTrue();

        // ایجاد نظرسنجی با پرسشنامه‌ی فعال باید کار کند.
        var activeSurvey = await surveyService.CreateAsync(new SaveSurveyRequest
        {
            Code = "SV-ACTIVE",
            QuestionnaireId = created.Value.Id,
            Localizations = [ new SurveyLocalizationDto { Language = Language.Fa, Title = "نظرسنجی" } ]
        });
        activeSurvey.IsSuccess.Should().BeTrue();

        // بایگانی (غیرفعال‌سازی) پرسشنامه: نظرسنجی‌های جدید نباید بتوانند انتخابش کنند.
        var archived = await questionnaireService.ArchiveAsync(created.Value.Id);
        archived.IsSuccess.Should().BeTrue();
        archived.Value!.Status.Should().Be(QuestionnaireStatus.Archived);

        var blocked = await surveyService.CreateAsync(new SaveSurveyRequest
        {
            Code = "SV-BLOCKED",
            QuestionnaireId = created.Value.Id,
            Localizations = [ new SurveyLocalizationDto { Language = Language.Fa, Title = "نظرسنجی" } ]
        });

        blocked.IsFailure.Should().BeTrue();
        blocked.Error.Code.Should().Be("questionnaire_not_active");

        // نظرسنجی از قبل موجود همچنان به همان پرسشنامه وصل است.
        var existing = await surveyService.GetByIdAsync(activeSurvey.Value!.Id);
        existing.IsSuccess.Should().BeTrue();
        existing.Value!.QuestionnaireId.Should().Be(created.Value.Id);
        existing.Value.Status.Should().Be(SurveyStatus.Draft);
    }
}
