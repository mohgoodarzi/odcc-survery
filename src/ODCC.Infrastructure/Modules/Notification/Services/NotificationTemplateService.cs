using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Application.Modules.Notification.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Entities;

namespace ODCC.Infrastructure.Modules.Notification.Services;

/// <summary>
/// مدیریت قالب‌های اعلان (پنل مدیریت). قالب‌ها در پایگاه داده نگه‌داری می‌شوند
/// تا مدیران بتوانند متن‌ها را بدون تغییر کد ویرایش کنند. کد قالب مبنای
/// جستجو و یکتایی است (حتی در میان بایگانی‌شده‌ها) چون فراخوانان از کد استفاده
/// می‌کنند.
/// </summary>
public sealed class NotificationTemplateService(
    INotificationTemplateRepository templateRepository,
    INotificationUnitOfWork unitOfWork) : INotificationTemplateService
{
    private readonly INotificationTemplateRepository _templateRepository = templateRepository;
    private readonly INotificationUnitOfWork _unitOfWork = unitOfWork;

    /// <inheritdoc/>
    public async Task<PagedResult<NotificationTemplateDto>> SearchAsync(
        NotificationTemplateSearchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var totalCount = await _templateRepository.CountAsync(request, ct);
        var templates = await _templateRepository.SearchAsync(request, ct);

        return new PagedResult<NotificationTemplateDto>
        {
            Items = templates.Select(ToDto).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 100)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationTemplateDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(id, ct);

        if (template is null)
        {
            return Result.Failure<NotificationTemplateDto>("template_not_found", "قالب یافت نشد.");
        }

        return Result.Success(ToDto(template));
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationTemplateDto>> CreateAsync(
        SaveNotificationTemplateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _templateRepository.ExistsByCodeAsync(request.Code, excludingId: null, ct))
        {
            return Result.Failure<NotificationTemplateDto>(
                "template_code_exists", "قالبی با این کد از قبل وجود دارد.");
        }

        var template = new NotificationTemplate
        {
            Code = request.Code,
            Name = request.Name,
            Channel = request.Channel,
            Category = request.Category,
            IsActive = request.IsActive
        };

        foreach (var localization in request.Localizations)
        {
            template.SetLocalization(localization.Language, localization.Subject, localization.Body);
        }

        await _templateRepository.AddAsync(template, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(template));
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationTemplateDto>> UpdateAsync(
        Guid id, SaveNotificationTemplateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var template = await _templateRepository.GetByIdAsync(id, ct);

        if (template is null)
        {
            return Result.Failure<NotificationTemplateDto>("template_not_found", "قالب یافت نشد.");
        }

        if (!template.IsActive)
        {
            return Result.Failure<NotificationTemplateDto>(
                "template_archived", "قالب بایگانی‌شده قابل ویرایش نیست.");
        }

        if (await _templateRepository.ExistsByCodeAsync(request.Code, excludingId: id, ct))
        {
            return Result.Failure<NotificationTemplateDto>(
                "template_code_exists", "قالب دیگری با این کد وجود دارد.");
        }

        template.Code = request.Code;
        template.Name = request.Name;
        template.Channel = request.Channel;
        template.Category = request.Category;

        // جایگزینی کامل ترجمه‌ها: حذف همه و درج دوباره ساده‌تر و امن‌تر از
        // ادغام دستی است چون EF Core تغییرات مجموعه‌ی فرزند را ردیابی می‌کند.
        template.Localizations.Clear();

        foreach (var localization in request.Localizations)
        {
            template.SetLocalization(localization.Language, localization.Subject, localization.Body);
        }

        _templateRepository.Update(template);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(template));
    }

    /// <inheritdoc/>
    public async Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templateRepository.GetByIdAsync(id, ct);

        if (template is null)
        {
            return Result.Failure("template_not_found", "قالب یافت نشد.");
        }

        // بایگانی نرم است: قالب غیرفعال می‌شود تا رندر از آن صرف‌نظر کند و به
        // متن پیش‌فرض توکار بیفتد. تاریخچه و وابستگی‌های قدیمی حفظ می‌شوند.
        template.IsActive = false;

        _templateRepository.Update(template);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    private static NotificationTemplateDto ToDto(NotificationTemplate t) => new()
    {
        Id = t.Id,
        Code = t.Code,
        Name = t.Name,
        Channel = t.Channel,
        Category = t.Category,
        IsActive = t.IsActive,
        Localizations = t.Localizations
            .Select(l => new NotificationTemplateLocalizationDto
            {
                Language = l.Language,
                Subject = l.Subject,
                Body = l.Body
            })
            .ToList(),
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
