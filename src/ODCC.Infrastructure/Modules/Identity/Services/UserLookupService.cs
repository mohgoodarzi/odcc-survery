using Microsoft.AspNetCore.Identity;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Infrastructure.Modules.Identity.Entities;

namespace ODCC.Infrastructure.Modules.Identity.Services;

/// <summary>
/// پیاده‌سازی جستجوی اطلاعات پایه‌ی کاربران برای سایر ماژول‌ها.
/// </summary>
public sealed class UserLookupService(UserManager<ApplicationUser> userManager) : IUserLookupService
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    public async Task<string?> GetDisplayNameAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user?.DisplayName;
    }

    public async Task<string?> GetUserNameAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user?.UserName;
    }
}
