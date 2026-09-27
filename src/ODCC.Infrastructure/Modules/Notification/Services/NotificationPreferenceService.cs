using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Application.Modules.Notification.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Entities;
using ODCC.Domain.Modules.Notification.Enums;

namespace ODCC.Infrastructure.Modules.Notification.Services;

/// <summary>
/// ترجیحات تحویل کاربر جاری. هر کاربر فقط ترجیحات خودش را می‌بیند یا تغییر
/// می‌دهد. مدل داده «انصراف» است: ردیفی وجود ندارد یعنی مجاز؛ ردیف با
/// <c>IsEnabled == false</c> یعنی غیرفعال. dispatcher قبل از هر تحویلی این
/// ترجیح را بررسی می‌کند.
/// </summary>
public sealed class NotificationPreferenceService(
    INotificationPreferenceRepository preferenceRepository,
    ICurrentUserService currentUserService,
    INotificationUnitOfWork unitOfWork) : INotificationPreferenceService
{
    private readonly INotificationPreferenceRepository _preferenceRepository = preferenceRepository;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly INotificationUnitOfWork _unitOfWork = unitOfWork;

    /// <summary>
    /// ترجیحات کاربر جاری: به ازای هر کانال، ترجیب عمومی (تمام دسته‌ها) به‌همراه
    /// هر ترجیب اختصاصیِ دسته‌ای که تعریف شده باشد. ردیف‌های پیش‌فرض (در پایگاه
    /// داده نیستند) هم بازگردانده می‌شوند تا رابط کاربری بتواند همه‌ی کانال‌ها
    /// را نشان دهد.
    /// </summary>
    public async Task<IReadOnlyList<NotificationPreferenceDto>> ListAsync(CancellationToken ct = default)
    {
        var userId = _currentUserService.UserId;

        if (userId is null)
        {
            return [];
        }

        var existing = await _preferenceRepository.ListByUserAsync(userId.Value, ct);
        var dtos = new List<NotificationPreferenceDto>();

        // ترجیب عمومی هر کانال (تمام دسته‌ها): اگر تعریف نشده بود، پیش‌فرض روشن.
        foreach (var channel in Enum.GetValues<NotificationChannel>())
        {
            var general = existing.FirstOrDefault(p => p.Channel == channel && p.Category is null);

            dtos.Add(general is not null
                ? ToDto(general)
                : new NotificationPreferenceDto
                {
                    Channel = channel,
                    Category = null,
                    IsEnabled = true,
                    IsDefault = true
                });

            // ترجیب‌های اختصاصی دسته‌ای که کاربر تعریف کرده است.
            dtos.AddRange(existing
                .Where(p => p.Channel == channel && p.Category is not null)
                .Select(ToDto));
        }

        return dtos;
    }

    /// <inheritdoc/>
    public async Task<Result<NotificationPreferenceDto>> UpdateAsync(
        UpdateNotificationPreferenceRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUserService.UserId;

        if (userId is null)
        {
            return Result.Failure<NotificationPreferenceDto>(
                "not_authenticated", "کاربر احراز هویت نشده است.");
        }

        var existing = await _preferenceRepository.FindAsync(userId.Value, request.Channel, request.Category, ct);

        if (existing is null)
        {
            // Upsert: ردیفی نبود، پس ساخته می‌شود.
            var preference = new NotificationPreference
            {
                UserId = userId.Value,
                Channel = request.Channel,
                Category = request.Category,
                IsEnabled = request.IsEnabled
            };

            await _preferenceRepository.AddAsync(preference, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(ToDto(preference));
        }

        existing.IsEnabled = request.IsEnabled;

        _preferenceRepository.Update(existing);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(existing));
    }

    private static NotificationPreferenceDto ToDto(NotificationPreference p) => new()
    {
        Id = p.Id,
        Channel = p.Channel,
        Category = p.Category,
        IsEnabled = p.IsEnabled,
        IsDefault = false
    };
}
