using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Languages;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.QuestionBank.Entities;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Domain.Modules.QuestionBank.Events;
using ODCC.Infrastructure.Modules.QuestionBank.Persistence;

namespace ODCC.Infrastructure.Modules.QuestionBank.Services;

/// <summary>
/// پیاده‌سازی سرویس کتابخانه‌ی سؤالات.
///
/// **نسخه‌برداری:** در زمان ایجاد سؤال، نسخه‌ی ۱ با یک تصویر لحظه‌ای JSON از
/// محتوای سؤال ثبت می‌شود. در زمان ویرایش، اگر محتوا (نوع، طیف، متن‌ها یا
/// گزینه‌ها) نسبت به آخرین تصویر ذخیره‌شده تغییر کرده باشد، نسخه‌ی جدیدی ثبت
/// می‌شود. پرسشنامه‌ها به نسخه‌ی مشخصی ارجاع می‌دهند تا ساختار پاسخ‌گویی در
/// طول عمر نظرسنجی ثابت بماند.
/// </summary>
public sealed class QuestionService(
    IQuestionRepository questionRepository,
    ICurrentUserService currentUserService,
    IQuestionBankUnitOfWork unitOfWork,
    QuestionBankDbContext dbContext) : IQuestionService
{
    private readonly IQuestionRepository _questionRepository = questionRepository;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IQuestionBankUnitOfWork _unitOfWork = unitOfWork;
    private readonly QuestionBankDbContext _dbContext = dbContext;

    private static readonly JsonSerializerOptions SnapshotSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<PagedResult<QuestionDto>> SearchAsync(QuestionSearchRequest request, CancellationToken ct = default)
    {
        var language = Language.Fa;

        var query = _dbContext.Questions
            .Include(q => q.Localizations)
            .Include(q => q.Options).ThenInclude(o => o.Localizations)
            .Include(q => q.Tags)
            .AsNoTracking();

        if (!request.IncludeArchived)
        {
            query = query.Where(q => !q.IsArchived);
        }

        if (request.Type is { } type)
        {
            query = query.Where(q => q.Type == type);
        }

        if (!string.IsNullOrWhiteSpace(request.Tag))
        {
            var tag = request.Tag.Trim();
            query = query.Where(q => q.Tags.Any(t => t.Name == tag));
        }

        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var text = request.SearchText.Trim();
            query = query.Where(q =>
                q.Code.Contains(text) ||
                q.Localizations.Any(l => l.Text.Contains(text)));
        }

        var totalCount = await query.CountAsync(ct);

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var questions = await query
            .OrderByDescending(q => q.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var dtos = questions.Select(q => ToDto(q, language)).ToList();

        return new PagedResult<QuestionDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Result<QuestionDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var question = await _questionRepository.GetByIdAsync(id, ct);
        if (question is null)
        {
            return Result.Failure<QuestionDto>("question_not_found", "سؤال یافت نشد.");
        }

        return Result.Success(ToDto(question, Language.Fa));
    }

    public async Task<Result<QuestionDto>> CreateAsync(SaveQuestionRequest request, CancellationToken ct = default)
    {
        var existing = await _questionRepository.FindByCodeAsync(request.Code, ct);
        if (existing is not null)
        {
            return Result.Failure<QuestionDto>("question_code_taken", "این کد سؤال قبلاً استفاده شده است.");
        }

        if (request.Type.HasOptions() && request.Options.Count < 2)
        {
            return Result.Failure<QuestionDto>("question_needs_options", "سؤال گزینه‌ای باید حداقل دو گزینه داشته باشد.");
        }

        if (!request.Type.HasOptions() && request.Options.Count > 0)
        {
            return Result.Failure<QuestionDto>("question_no_options_allowed", "فقط سؤال‌های گزینه‌ای می‌توانند گزینه داشته باشند.");
        }

        var question = new Question
        {
            Code = request.Code,
            Type = request.Type,
            ScaleMax = request.ScaleMax,
            IsArchived = request.IsArchived,
            CurrentVersionNumber = 1
        };

        ApplyLocalizations(question, request.Localizations);
        ApplyOptions(question, request.Options, isNew: true);
        ApplyTags(question, request.Tags);

        var snapshot = BuildSnapshot(question);
        question.Versions.Add(new QuestionVersion
        {
            QuestionId = question.Id,
            VersionNumber = 1,
            Snapshot = snapshot,
            ChangeSummary = "ایجاد سؤال",
            CreatedByUserId = _currentUserService.UserId
        });

        question.RaiseDomainEvent(new QuestionCreatedEvent(question.Id, question.Code, question.Type, _currentUserService.UserId));

        await _questionRepository.AddAsync(question, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(question, Language.Fa));
    }

    public async Task<Result<QuestionDto>> UpdateAsync(Guid id, SaveQuestionRequest request, CancellationToken ct = default)
    {
        var question = await _questionRepository.GetByIdAsync(id, ct);
        if (question is null)
        {
            return Result.Failure<QuestionDto>("question_not_found", "سؤال یافت نشد.");
        }

        if (!string.Equals(question.Code, request.Code, StringComparison.Ordinal))
        {
            var codeOwner = await _questionRepository.FindByCodeAsync(request.Code, ct);
            if (codeOwner is not null && codeOwner.Id != id)
            {
                return Result.Failure<QuestionDto>("question_code_taken", "این کد سؤال قبلاً استفاده شده است.");
            }
        }

        if (request.Type.HasOptions() && request.Options.Count < 2)
        {
            return Result.Failure<QuestionDto>("question_needs_options", "سؤال گزینه‌ای باید حداقل دو گزینه داشته باشد.");
        }

        if (!request.Type.HasOptions() && request.Options.Count > 0)
        {
            return Result.Failure<QuestionDto>("question_no_options_allowed", "فقط سؤال‌های گزینه‌ای می‌توانند گزینه داشته باشند.");
        }

        question.Code = request.Code;
        question.Type = request.Type;
        question.ScaleMax = request.ScaleMax;
        question.IsArchived = request.IsArchived;

        ApplyLocalizations(question, request.Localizations);
        ApplyOptions(question, request.Options, isNew: false);
        ApplyTags(question, request.Tags);

        // فقط در صورت تغییر محتوای سؤال نسخه‌ی جدید ثبت می‌شود.
        var newSnapshot = BuildSnapshot(question);
        var latestSnapshot = question.Versions
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefault()?.Snapshot;

        if (latestSnapshot is null || !string.Equals(latestSnapshot, newSnapshot, StringComparison.Ordinal))
        {
            question.CurrentVersionNumber = (question.Versions.MaxBy(v => v.VersionNumber)?.VersionNumber ?? 0) + 1;

            question.Versions.Add(new QuestionVersion
            {
                QuestionId = question.Id,
                VersionNumber = question.CurrentVersionNumber,
                Snapshot = newSnapshot,
                ChangeSummary = $"به‌روزرسانی محتوای سؤال به نسخه‌ی {question.CurrentVersionNumber}",
                CreatedByUserId = _currentUserService.UserId
            });
        }

        question.RaiseDomainEvent(new QuestionUpdatedEvent(question.Id, question.Code, question.CurrentVersionNumber, _currentUserService.UserId));

        _questionRepository.Update(question);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(question, Language.Fa));
    }

    public async Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var question = await _questionRepository.GetByIdAsync(id, ct);
        if (question is null)
        {
            return Result.Failure("question_not_found", "سؤال یافت نشد.");
        }

        question.Archive();
        question.RaiseDomainEvent(new QuestionArchivedEvent(question.Id, question.Code, _currentUserService.UserId));

        _questionRepository.Update(question);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<QuestionVersionDto>>> GetVersionsAsync(Guid id, CancellationToken ct = default)
    {
        var exists = await _questionRepository.GetByIdAsync(id, ct) is not null;
        if (!exists)
        {
            return Result.Failure<IReadOnlyList<QuestionVersionDto>>("question_not_found", "سؤال یافت نشد.");
        }

        var versions = await _questionRepository.GetVersionsAsync(id, ct);
        var dtos = versions
            .Select(v => new QuestionVersionDto
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                ChangeSummary = v.ChangeSummary,
                CreatedByUserId = v.CreatedByUserId,
                CreatedAt = v.CreatedAt
            })
            .ToList();

        return Result.Success<IReadOnlyList<QuestionVersionDto>>(dtos);
    }

    // --- کمک‌کننده‌ها ----------------------------------------------------------

    private static void ApplyLocalizations(Question question, IReadOnlyList<QuestionLocalizationDto> localizations)
    {
        foreach (var loc in localizations)
        {
            question.SetLocalization(loc.Language, loc.Text.Trim(), string.IsNullOrWhiteSpace(loc.Description) ? null : loc.Description.Trim());
        }

        // حذف ترجمه‌هایی که دیگر در درخواست نیستند.
        var requested = localizations.Select(l => l.Language).ToHashSet();
        question.Localizations.RemoveAll(l => !requested.Contains(l.Language));
    }

    private static void ApplyOptions(Question question, IReadOnlyList<SaveQuestionOptionRequest> options, bool isNew)
    {
        if (!question.Type.HasOptions())
        {
            question.Options.Clear();
            return;
        }

        var submittedIds = new HashSet<Guid>();

        foreach (var optionRequest in options.OrderBy(o => o.DisplayOrder))
        {
            var option = optionRequest.Id is { } existingId && !isNew
                ? question.Options.FirstOrDefault(o => o.Id == existingId)
                : null;

            if (option is null)
            {
                option = new QuestionOption
                {
                    Id = optionRequest.Id ?? Guid.CreateVersion7(),
                    QuestionId = question.Id,
                    Code = optionRequest.Code,
                    DisplayOrder = optionRequest.DisplayOrder
                };
                question.Options.Add(option);
            }
            else
            {
                option.Code = optionRequest.Code;
                option.DisplayOrder = optionRequest.DisplayOrder;
            }

            submittedIds.Add(option.Id);

            foreach (var loc in optionRequest.Localizations)
            {
                option.SetLocalization(loc.Language, loc.Text.Trim());
            }

            option.Localizations.RemoveAll(l => !optionRequest.Localizations.Any(x => x.Language == l.Language));
        }

        // حذف گزینه‌هایی که دیگر در درخواست نیستند.
        question.Options.RemoveAll(o => !submittedIds.Contains(o.Id));
        question.Options.Sort((a, b) => a.DisplayOrder.CompareTo(b.DisplayOrder));
    }

    private static void ApplyTags(Question question, IReadOnlyList<string> tags)
    {
        var requested = tags
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        question.Tags.RemoveAll(t => !requested.Contains(t.Name, StringComparer.OrdinalIgnoreCase));

        foreach (var name in requested)
        {
            question.AddTag(name);
        }
    }

    /// <summary>
    /// تصویر لحظه‌ای از محتوای سؤال: نوع، طیف، متن‌ها و گزینه‌ها.
    /// این تصویر مبنای مقایسه‌ی تغییرات و بازتولید پرسشنامه در زمان پاسخ‌گویی است.
    /// </summary>
    private static string BuildSnapshot(Question question) =>
        JsonSerializer.Serialize(new QuestionSnapshot
        {
            Type = question.Type,
            ScaleMax = question.ScaleMax,
            Texts = question.Localizations.ToDictionary(l => l.Language.ToString(), l => l.Text),
            OptionTexts = question.Options
                .OrderBy(o => o.DisplayOrder)
                .Select(o => new QuestionOptionSnapshot
                {
                    Code = o.Code,
                    Texts = o.Localizations.ToDictionary(l => l.Language.ToString(), l => l.Text)
                })
                .ToList()
        }, SnapshotSerializerOptions);

    private static QuestionDto ToDto(Question question, Language language)
    {
        var picked = question.Localizations.Pick(language);

        return new QuestionDto
        {
            Id = question.Id,
            Code = question.Code,
            Type = question.Type,
            ScaleMax = question.ScaleMax,
            IsArchived = question.IsArchived,
            CurrentVersionNumber = question.CurrentVersionNumber,
            Text = picked?.Text ?? question.Localizations.FirstOrDefault()?.Text ?? string.Empty,
            Description = picked?.Description,
            Tags = question.Tags.Select(t => t.Name).Order().ToList(),
            Options = question.Options
                .OrderBy(o => o.DisplayOrder)
                .Select(o => new QuestionOptionDto
                {
                    Id = o.Id,
                    Code = o.Code,
                    DisplayOrder = o.DisplayOrder,
                    Text = o.Localizations.Pick(language)?.Text ?? o.Localizations.FirstOrDefault()?.Text ?? string.Empty,
                    Localizations = o.Localizations.Select(ol => new QuestionOptionLocalizationDto
                    {
                        Language = ol.Language,
                        Text = ol.Text
                    }).ToList()
                })
                .ToList(),
            Localizations = question.Localizations.Select(l => new QuestionLocalizationDto
            {
                Language = l.Language,
                Text = l.Text,
                Description = l.Description
            }).ToList(),
            CreatedAt = question.CreatedAt,
            UpdatedAt = question.UpdatedAt
        };
    }

    private sealed record QuestionSnapshot
    {
        public QuestionType Type { get; init; }
        public int ScaleMax { get; init; }
        public Dictionary<string, string> Texts { get; init; } = [];
        public List<QuestionOptionSnapshot> OptionTexts { get; init; } = [];
    }

    private sealed record QuestionOptionSnapshot
    {
        public string Code { get; init; } = string.Empty;
        public Dictionary<string, string> Texts { get; init; } = [];
    }
}
