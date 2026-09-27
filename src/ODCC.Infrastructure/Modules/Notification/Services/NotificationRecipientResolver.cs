using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Notification.Enums;
using ODCC.Domain.Modules.Organization.Enums;
using ODCC.Infrastructure.Modules.Identity.Entities;

namespace ODCC.Infrastructure.Modules.Notification.Services;

/// <summary>
/// حل‌کننده‌ی گیرنده‌ها: تبدیل «کاربران دارای یک مجوز» یا «کارمندان» به
/// <see cref="NotificationRecipient"/>.
///
/// <b>مرز ماژول‌ها:</b> این کلاس فقط از قراردادهای عمومی ماژول هویت
/// (<c>IUserService</c>، <c>UserManager</c> برای RoleClaims) و ماژول سازمان
/// (<c>IEmployeeRepository</c>) استفاده می‌کند. هرگز به DbContext یا موجودیت‌های
/// داخلی آن ماژول‌ها ارجاع نمی‌دهد.
/// </summary>
public sealed class NotificationRecipientResolver(
    IUserService userService,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IEmployeeRepository employeeRepository) : INotificationRecipientResolver
{
    private readonly IUserService _userService = userService;
    private readonly RoleManager<ApplicationRole> _roleManager = roleManager;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IEmployeeRepository _employeeRepository = employeeRepository;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationRecipient>> ResolveByPermissionAsync(
        string permission, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);

        // یافتن نقش‌هایی که این مجوز را به‌عنوان RoleClaim دارند. این مسیر
        // عمومی است (RoleManager) و DbContext ماژول هویت لمس نمی‌شود.
        var matchingRoles = new List<ApplicationRole>();

        foreach (var role in _roleManager.Roles)
        {
            var claims = await _roleManager.GetClaimsAsync(role);

            if (claims.Any(c => string.Equals(c.Type, Permissions.ClaimType, StringComparison.Ordinal)
                && string.Equals(c.Value, permission, StringComparison.OrdinalIgnoreCase)))
            {
                matchingRoles.Add(role);
            }
        }

        if (matchingRoles.Count == 0)
        {
            return [];
        }

        // کاربران فعال در نقش‌های هدف. GetUsersInRoleAsync به‌ازای هر نقش یک
        // پرس‌وجو می‌زند (نه به‌ازای هر کاربر)، که برای چند نقش کارآمد است.
        var recipients = new Dictionary<Guid, NotificationRecipient>();

        foreach (var role in matchingRoles)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name!);

            foreach (var user in usersInRole)
            {
                if (!user.IsActive || user.IsDeleted)
                {
                    continue;
                }

                // یک کاربر ممکن است چند نقش هدف داشته باشد؛ یکبار اضافه می‌شود.
                if (recipients.ContainsKey(user.Id))
                {
                    continue;
                }

                recipients[user.Id] = new NotificationRecipient
                {
                    UserId = user.Id,
                    Name = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName,
                    Email = user.Email,
                    Phone = user.PhoneNumber
                };
            }
        }

        return recipients.Values.ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<NotificationRecipient>> ResolveByEmployeesAsync(
        IReadOnlyCollection<Guid> employeeIds, CancellationToken ct = default)
    {
        if (employeeIds is null || employeeIds.Count == 0)
        {
            return [];
        }

        var employees = await _employeeRepository.GetByIdsAsync(employeeIds, ct);
        var recipients = new List<NotificationRecipient>(employees.Count);

        foreach (var employee in employees)
        {
            // ایمیل کاری سازمانی اولویت دارد؛ در غیر این صورت از ایمیل کاربر
            // سامانه استفاده می‌شود (در صورت اتصال کارمند به کاربر).
            var email = employee.WorkEmail;

            if (string.IsNullOrWhiteSpace(email) && employee.UserId is { } userId)
            {
                var userResult = await _userService.GetByIdAsync(userId, ct);
                email = userResult.IsSuccess ? userResult.Value?.Email : null;
            }

            var name = string.IsNullOrWhiteSpace(employee.FullName) ? null : employee.FullName;

            recipients.Add(new NotificationRecipient
            {
                UserId = employee.UserId,
                EmployeeId = employee.Id,
                Name = name,
                Email = email
            });
        }

        return recipients;
    }
}
