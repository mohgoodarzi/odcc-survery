using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Application.Languages;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Entities;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.Questionnaire.Entities;
using QuestionnaireEntity = ODCC.Domain.Modules.Questionnaire.Entities.Questionnaire;
using ODCC.Domain.Modules.Questionnaire.Enums;
using ODCC.Domain.Modules.Questionnaire.Events;
using ODCC.Infrastructure.Modules.Questionnaire.Persistence;

namespace ODCC.Infrastructure.Modules.Questionnaire.Services;

/// <summary>
/// پیاده‌سازی سرویس پرسشنامه‌ها.
///
/// **یکپارچگی تجمعی:** کل ساختار پرسشنامه (بخش‌ها، آیتم‌ها و قوانین انشعاب) در یک
/// تراکنش ذخیره می‌شود تا وضعیت پرسشنامه هرگز نیمه‌کاره باقی نماند.
///
/// **چسبیدن به نسخه:** هر آیتم به یک نسخه‌ی مشخص از سؤال کتابخانه ارجاع می‌دهد.
/// آیتم جدید همواره به آخرین نسخه‌ی سؤال چسبیده می‌شود؛ ویرایش آیتم موجود نسخه‌ی
/// چسبیده را حفظ می‌کند تا ساختار پاسخ‌گویی در طول عمر نظرسنجی ثابت بماند.
///
/// **مرز ماژول‌ها:** این سرویس برای خواندن سؤال‌های کتابخانه فقط از قرارداد
/// <see cref="IQuestionRepository"/> استفاده می‌کند، هرگز از DbContext آن ماژول.
/// </summary>
public sealed class QuestionnaireService(
    IQuestionnaireRepository questionnaireRepository,
    IQuestionRepository questionRepository,
    ICurrentUserService currentUserService,
    IQuestionnaireUnitOfWork unitOfWork,
    QuestionnaireDbContext dbContext) : IQuestionnaireService
{
    private readonly IQuestionnaireRepository _questionnaireRepository = questionnaireRepository;
    private readonly IQuestionRepository _questionRepository = questionRepository;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IQuestionnaireUnitOfWork _unitOfWork = unitOfWork;
    private readonly QuestionnaireDbContext _dbContext = dbContext;

    private static readonly JsonSerializerOptions SnapshotSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<PagedResult<QuestionnaireSummaryDto>> SearchAsync(QuestionnaireSearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _questionnaireRepository.CountAsync(request, ct);

        var questionnaires = await _questionnaireRepository.SearchAsync(request, ct);

        var dtos = questionnaires.Select(q => new QuestionnaireSummaryDto
        {
            Id = q.Id,
            Code = q.Code,
            Status = q.Status,
            Version = q.Version,
            Title = q.Localizations.Pick(Language.Fa)?.Title
                ?? q.Localizations.FirstOrDefault()?.Title
                ?? q.Code,
            SectionCount = q.Sections.Count,
            ItemCount = q.Sections.Sum(s => s.Items.Count),
            CreatedAt = q.CreatedAt,
            UpdatedAt = q.UpdatedAt
        }).ToList();

        return new PagedResult<QuestionnaireSummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 100)
        };
    }

    public async Task<Result<QuestionnaireDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var questionnaire = await _questionnaireRepository.GetByIdAsync(id, ct);
        if (questionnaire is null)
        {
            return Result.Failure<QuestionnaireDto>("questionnaire_not_found", "پرسشنامه یافت نشد.");
        }

        var questions = await LoadQuestionsAsync(questionnaire, ct);

        return Result.Success(ToDto(questionnaire, questions));
    }

    public async Task<Result<QuestionnaireDto>> CreateAsync(SaveQuestionnaireRequest request, CancellationToken ct = default)
    {
        var existing = await _questionnaireRepository.FindByCodeAsync(request.Code, ct);
        if (existing is not null)
        {
            return Result.Failure<QuestionnaireDto>("questionnaire_code_taken", "این کد پرسشنامه قبلاً استفاده شده است.");
        }

        var questionnaire = new QuestionnaireEntity
        {
            Code = request.Code,
            Status = QuestionnaireStatus.Draft,
            Version = 1
        };

        ApplyLocalizations(questionnaire, request.Localizations);

        var questions = await _questionRepository.GetByIdsAsync(request.Sections.SelectMany(s => s.Items).Select(i => i.QuestionId).ToList(), ct);

        var questionLookup = questions.ToDictionary(q => q.Id);

        var sectionResult = ApplySections(questionnaire, request.Sections, questionLookup);
        if (sectionResult.IsFailure)
        {
            return Result.Failure<QuestionnaireDto>(sectionResult.Error);
        }

        questionnaire.RaiseDomainEvent(new QuestionnaireCreatedEvent(questionnaire.Id, questionnaire.Code, _currentUserService.UserId));

        await _questionnaireRepository.AddAsync(questionnaire, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(questionnaire, questionLookup));
    }

    public async Task<Result<QuestionnaireDto>> UpdateAsync(Guid id, SaveQuestionnaireRequest request, CancellationToken ct = default)
    {
        var questionnaire = await _questionnaireRepository.GetByIdAsync(id, ct);
        if (questionnaire is null)
        {
            return Result.Failure<QuestionnaireDto>("questionnaire_not_found", "پرسشنامه یافت نشد.");
        }

        // پرسشنامه‌ی منتشرشده قابل ویرایش نیست (داده‌ی پاسخ‌گویی ثابت می‌ماند).
        if (questionnaire.Status == QuestionnaireStatus.Active)
        {
            return Result.Failure<QuestionnaireDto>("questionnaire_is_active", "پرسشنامه‌ی فعال را نمی‌توان ویرایش کرد. ابتدا یک نسخه‌ی جدید بسازید یا آن را بایگانی کنید.");
        }

        if (!string.Equals(questionnaire.Code, request.Code, StringComparison.Ordinal))
        {
            var codeOwner = await _questionnaireRepository.FindByCodeAsync(request.Code, ct);
            if (codeOwner is not null && codeOwner.Id != id)
            {
                return Result.Failure<QuestionnaireDto>("questionnaire_code_taken", "این کد پرسشنامه قبلاً استفاده شده است.");
            }
        }

        questionnaire.Code = request.Code;

        ApplyLocalizations(questionnaire, request.Localizations);

        var questions = await _questionRepository.GetByIdsAsync(request.Sections.SelectMany(s => s.Items).Select(i => i.QuestionId).ToList(), ct);

        var questionLookup = questions.ToDictionary(q => q.Id);

        var sectionResult = ApplySections(questionnaire, request.Sections, questionLookup);
        if (sectionResult.IsFailure)
        {
            return Result.Failure<QuestionnaireDto>(sectionResult.Error);
        }

        questionnaire.ReorderSections();
        foreach (var section in questionnaire.Sections)
        {
            section.ReorderItems();
        }

        questionnaire.RaiseDomainEvent(new QuestionnaireUpdatedEvent(questionnaire.Id, questionnaire.Code, _currentUserService.UserId));

        _questionnaireRepository.Update(questionnaire);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(questionnaire, questionLookup));
    }

    public async Task<Result<QuestionnaireDto>> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var questionnaire = await _questionnaireRepository.GetByIdAsync(id, ct);
        if (questionnaire is null)
        {
            return Result.Failure<QuestionnaireDto>("questionnaire_not_found", "پرسشنامه یافت نشد.");
        }

        if (questionnaire.Status == QuestionnaireStatus.Archived)
        {
            return Result.Failure<QuestionnaireDto>("questionnaire_archived", "پرسشنامه‌ی بایگانی‌شده را نمی‌توان منتشر کرد.");
        }

        if (!questionnaire.IsPublishable)
        {
            return Result.Failure<QuestionnaireDto>("questionnaire_not_publishable", "پرسشنامه برای انتشار باید حداقل یک بخش با یک سؤال و یک عنوان داشته باشد.");
        }

        questionnaire.Publish();
        questionnaire.RaiseDomainEvent(new QuestionnairePublishedEvent(questionnaire.Id, questionnaire.Code, questionnaire.Version, _currentUserService.UserId));

        _questionnaireRepository.Update(questionnaire);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(questionnaire, await LoadQuestionsAsync(questionnaire, ct)));
    }

    public async Task<Result<QuestionnaireDto>> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var questionnaire = await _questionnaireRepository.GetByIdAsync(id, ct);
        if (questionnaire is null)
        {
            return Result.Failure<QuestionnaireDto>("questionnaire_not_found", "پرسشنامه یافت نشد.");
        }

        questionnaire.Archive();
        questionnaire.RaiseDomainEvent(new QuestionnaireArchivedEvent(questionnaire.Id, questionnaire.Code, _currentUserService.UserId));

        _questionnaireRepository.Update(questionnaire);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(questionnaire, await LoadQuestionsAsync(questionnaire, ct)));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var questionnaire = await _questionnaireRepository.GetByIdAsync(id, ct);
        if (questionnaire is null)
        {
            return Result.Failure("questionnaire_not_found", "پرسشنامه یافت نشد.");
        }

        // فقط پیش‌نویس‌ها حذف می‌شوند تا تاریخچه‌ی پاسخ‌ها در نظرسنجی‌های منتشرشده حفظ شود.
        if (questionnaire.Status != QuestionnaireStatus.Draft)
        {
            return Result.Failure("questionnaire_not_deletable", "فقط پرسشنامه‌های پیش‌نویس قابل حذف هستند.");
        }

        _questionnaireRepository.Remove(questionnaire);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // --- کمک‌کننده‌ها ----------------------------------------------------------

    /// <summary>بارگذاری سؤال‌های ارجاع‌شده در آیتم‌های پرسشنامه (یک پرس‌وجو).</summary>
    private async Task<Dictionary<Guid, Question>> LoadQuestionsAsync(QuestionnaireEntity questionnaire, CancellationToken ct)
    {
        var questionIds = questionnaire.AllItems.Select(i => i.QuestionId).Distinct().ToList();
        if (questionIds.Count == 0)
        {
            return [];
        }

        var questions = await _questionRepository.GetByIdsAsync(questionIds, ct);
        return questions.ToDictionary(q => q.Id);
    }

    private static void ApplyLocalizations(QuestionnaireEntity questionnaire, IReadOnlyList<QuestionnaireLocalizationDto> localizations)
    {
        foreach (var loc in localizations)
        {
            questionnaire.SetLocalization(loc.Language, loc.Title.Trim(), string.IsNullOrWhiteSpace(loc.Description) ? null : loc.Description.Trim());
        }

        var requested = localizations.Select(l => l.Language).ToHashSet();
        questionnaire.Localizations.RemoveAll(l => !requested.Contains(l.Language));
    }

    /// <summary>
    /// بازسازی بخش‌ها و آیتم‌های پرسشنامه بر اساس درخواست.
    /// بخش/آیتم با شناسه‌ی مطابق به‌روزرسانی می‌شود، در غیر این صورت ساخته می‌شود
    /// و آن‌هایی که در درخواست نیستند حذف می‌شوند.
    /// </summary>
    private static Result ApplySections(
        QuestionnaireEntity questionnaire,
        IReadOnlyList<SaveSectionRequest> sections,
        IReadOnlyDictionary<Guid, Domain.Modules.QuestionBank.Entities.Question> questions)
    {
        var submittedSectionIds = new HashSet<Guid>();

        for (var order = 0; order < sections.Count; order++)
        {
            var sectionRequest = sections[order];

            var section = sectionRequest.Id is { } existingId
                ? questionnaire.Sections.FirstOrDefault(s => s.Id == existingId)
                : null;

            var isNew = section is null;
            if (isNew)
            {
                section = new Section
                {
                    Id = sectionRequest.Id ?? Guid.CreateVersion7(),
                    QuestionnaireId = questionnaire.Id
                };

                questionnaire.Sections.Add(section);
            }

            var target = section!;

            target.IsOptional = sectionRequest.IsOptional;
            target.DisplayOrder = order;

            foreach (var loc in sectionRequest.Localizations)
            {
                target.SetLocalization(loc.Language, loc.Title.Trim());
            }

            var requestedLocLanguages = sectionRequest.Localizations.Select(l => l.Language).ToHashSet();
            target.Localizations.RemoveAll(l => !requestedLocLanguages.Contains(l.Language));

            var itemResult = ApplyItems(target, sectionRequest.Items, questions);
            if (itemResult.IsFailure)
            {
                return itemResult;
            }

            submittedSectionIds.Add(target.Id);
        }

        questionnaire.Sections.RemoveAll(s => !submittedSectionIds.Contains(s.Id));

        return Result.Success();
    }

    private static Result ApplyItems(
        Section section,
        IReadOnlyList<SaveItemRequest> items,
        IReadOnlyDictionary<Guid, Domain.Modules.QuestionBank.Entities.Question> questions)
    {
        var submittedItemIds = new HashSet<Guid>();

        for (var order = 0; order < items.Count; order++)
        {
            var itemRequest = items[order];

            if (!questions.TryGetValue(itemRequest.QuestionId, out var question))
            {
                return Result.Failure("question_not_found", "یکی از سؤال‌های ارجاع‌شده در کتابخانه وجود ندارد.");
            }

            var item = itemRequest.Id is { } existingId
                ? section.Items.FirstOrDefault(i => i.Id == existingId)
                : null;

            var isNew = item is null;
            if (isNew)
            {
                // آیتم جدید همواره به آخرین نسخه‌ی سؤال چسبیده می‌شود.
                item = new QuestionnaireItem
                {
                    Id = itemRequest.Id ?? Guid.CreateVersion7(),
                    SectionId = section.Id,
                    QuestionId = question.Id,
                    QuestionVersionNumber = question.CurrentVersionNumber,
                    QuestionCode = question.Code,
                    QuestionType = question.Type
                };
            }
            else if (item!.QuestionId != question.Id)
            {
                // تغییر سؤالِ آیتم موجود: دوباره به آخرین نسخه‌ی سؤال جدید چسبیده می‌شود.
                item.QuestionId = question.Id;
                item.QuestionVersionNumber = question.CurrentVersionNumber;
                item.QuestionCode = question.Code;
                item.QuestionType = question.Type;
                item.BranchingRules.Clear();
            }

            item!.IsRequired = itemRequest.IsRequired;
            item.TitleOverride = string.IsNullOrWhiteSpace(itemRequest.TitleOverride) ? null : itemRequest.TitleOverride.Trim();
            item.DisplayOrder = order;

            var ruleResult = ApplyBranchingRules(item!, itemRequest.BranchingRules, question, items);
            if (ruleResult.IsFailure)
            {
                return ruleResult;
            }

            if (isNew)
            {
                section.Items.Add(item!);
            }

            submittedItemIds.Add(item!.Id);
        }

        section.Items.RemoveAll(i => !submittedItemIds.Contains(i.Id));

        return Result.Success();
    }

    private static Result ApplyBranchingRules(
        QuestionnaireItem item,
        IReadOnlyList<SaveBranchingRuleRequest> rules,
        Domain.Modules.QuestionBank.Entities.Question question,
        IReadOnlyList<SaveItemRequest> allItems)
    {
        // همه‌ی شناسه‌ی آیتم‌های این درخواست (برای مقصد‌های آیتم جدید).
        var validTargetIds = allItems
            .Select(i => i.Id)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToHashSet();

        // گزینه‌های مجاز برای سؤال گزینه‌ای (از نسخه‌ی چسبیده).
        var allowedOptionCodes = GetOptionCodesForVersion(question, item.QuestionVersionNumber);

        var submittedRuleIds = new HashSet<Guid>();

        foreach (var ruleRequest in rules)
        {
            // مقصد باید یکی از آیتم‌های همین پرسشنامه (در این درخواست) باشد و
            // انشعال به خود آیتم بی‌معناست.
            if (ruleRequest.TargetItemId == Guid.Empty
                || !validTargetIds.Contains(ruleRequest.TargetItemId)
                || ruleRequest.TargetItemId == item.Id)
            {
                return Result.Failure("branching_target_invalid", "مقصد قانون انشعاب باید یک آیتم دیگر از همین پرسشنامه باشد.");
            }

            if (!BranchingConditionRules.IsApplicable(ruleRequest.Condition, question.Type))
            {
                return Result.Failure("branching_condition_invalid", "این شرط انشعاب برای نوع این سؤال مجاز نیست.");
            }

            // برای سؤال‌های گزینه‌ای، مقدار مورد انتظار باید کد یکی از گزینه‌ها باشد.
            if (allowedOptionCodes is not null && !allowedOptionCodes.Contains(ruleRequest.ExpectedValue.Trim()))
            {
                return Result.Failure("branching_value_invalid", "مقدار مورد انتظار باید کد یکی از گزینه‌های سؤال باشد.");
            }

            var rule = ruleRequest.Id is { } existingId
                ? item.BranchingRules.FirstOrDefault(r => r.Id == existingId)
                : null;

            var isNew = rule is null;
            if (isNew)
            {
                rule = new BranchingRule
                {
                    Id = ruleRequest.Id ?? Guid.CreateVersion7(),
                    ItemId = item.Id
                };
            }

            rule!.TargetItemId = ruleRequest.TargetItemId;
            rule.Condition = ruleRequest.Condition;
            rule.ExpectedValue = ruleRequest.ExpectedValue.Trim();

            if (isNew)
            {
                item.BranchingRules.Add(rule!);
            }

            submittedRuleIds.Add(rule!.Id);
        }

        item.BranchingRules.RemoveAll(r => !submittedRuleIds.Contains(r.Id));

        return Result.Success();
    }

    /// <summary>
    /// کدهای گزینه‌های یک سؤال در نسخه‌ی مشخص. <c>null</c> برای سؤال‌های غیرگزینه‌ای
    /// (که هر مقدار متنی/عددی مجاز است). کدها از تصویر لحظه‌ای نسخه استخراج می‌شوند.
    /// </summary>
    private static HashSet<string>? GetOptionCodesForVersion(Domain.Modules.QuestionBank.Entities.Question question, int versionNumber)
    {
        if (!question.Type.HasOptions())
        {
            return null;
        }

        var version = question.Versions.FirstOrDefault(v => v.VersionNumber == versionNumber);
        if (version is null)
        {
            // نسخه پیدا نشد: به‌جای رد کردن، محدودیت گزینه‌ها اعمال نمی‌شود
            // (فراخوان باید قبل از آن وجود نسخه را تضمین کند).
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(version.Snapshot);

            if (!document.RootElement.TryGetProperty("optionTexts", out var optionsElement))
            {
                return null;
            }

            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var option in optionsElement.EnumerateArray())
            {
                if (option.TryGetProperty("code", out var codeElement))
                {
                    codes.Add(codeElement.GetString() ?? string.Empty);
                }
            }

            return codes;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static QuestionnaireDto ToDto(QuestionnaireEntity questionnaire, Dictionary<Guid, Domain.Modules.QuestionBank.Entities.Question> questions)
    {
        var picked = questionnaire.Localizations.Pick(Language.Fa);

        return new QuestionnaireDto
        {
            Id = questionnaire.Id,
            Code = questionnaire.Code,
            Status = questionnaire.Status,
            Version = questionnaire.Version,
            Title = picked?.Title ?? questionnaire.Localizations.FirstOrDefault()?.Title ?? questionnaire.Code,
            Description = picked?.Description,
            Localizations = questionnaire.Localizations.Select(l => new QuestionnaireLocalizationDto
            {
                Language = l.Language,
                Title = l.Title,
                Description = l.Description
            }).ToList(),
            Sections = questionnaire.Sections
                .OrderBy(s => s.DisplayOrder)
                .Select(section => new SectionDto
                {
                    Id = section.Id,
                    DisplayOrder = section.DisplayOrder,
                    IsOptional = section.IsOptional,
                    Title = section.Localizations.Pick(Language.Fa)?.Title
                        ?? section.Localizations.FirstOrDefault()?.Title
                        ?? string.Empty,
                    Localizations = section.Localizations.Select(l => new SectionLocalizationDto
                    {
                        Language = l.Language,
                        Title = l.Title
                    }).ToList(),
                    Items = section.Items
                        .OrderBy(i => i.DisplayOrder)
                        .Select(item => new QuestionnaireItemDto
                        {
                            Id = item.Id,
                            SectionId = item.SectionId,
                            QuestionId = item.QuestionId,
                            QuestionVersionNumber = item.QuestionVersionNumber,
                            QuestionCode = item.QuestionCode,
                            QuestionType = item.QuestionType,
                            DisplayOrder = item.DisplayOrder,
                            IsRequired = item.IsRequired,
                            TitleOverride = item.TitleOverride,
                            QuestionText = questions.TryGetValue(item.QuestionId, out var question)
                                ? (question.Localizations.Pick(Language.Fa)?.Text
                                   ?? question.Localizations.FirstOrDefault()?.Text
                                   ?? item.QuestionCode)
                                : item.QuestionCode,
                            BranchingRules = item.BranchingRules.Select(rule => new BranchingRuleDto
                            {
                                Id = rule.Id,
                                TargetItemId = rule.TargetItemId,
                                Condition = rule.Condition,
                                ExpectedValue = rule.ExpectedValue
                            }).ToList()
                        })
                        .ToList()
                })
                .ToList(),
            SectionCount = questionnaire.Sections.Count,
            ItemCount = questionnaire.Sections.Sum(s => s.Items.Count),
            IsPublishable = questionnaire.IsPublishable,
            CreatedAt = questionnaire.CreatedAt,
            UpdatedAt = questionnaire.UpdatedAt
        };
    }
}
