using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using ODCC.Application.Authorization;

namespace ODCC.Api.Authorization;

/// <summary>
/// تأمین‌کننده‌ی Policy پویا.
///
/// به‌جای ثبت تک‌تک مجوزها به‌عنوان Policy در زمان راه‌اندازی (که با افزودن مجوزهای جدید
/// به سرعت ناپایدار می‌شود)، این کلاس به ازای هر نام مجوز، یک Policy شامل
/// <see cref="PermissionRequirement"/> می‌سازد. بنابراین <c>[HasPermission(Permissions.Identity.UsersView)]</c>
/// بدون هیچ پیکربندی اضافه‌ای کار می‌کند.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    /// <summary>
    /// Policy پیش‌فرض: تمام اندپوینت‌ها در صورت نبودن <c>[AllowAnonymous]</c>
    /// نیازمند کاربر احراز هویت‌شده هستند. این رفتار «fail-closed» است:
    /// یک اندپوینت جدید که فراموش شود مجوز بگیرد، به‌جای دسترسی عمومی شدن،
    /// دسترسی‌اش رد می‌شود.
    /// </summary>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        Task.FromResult(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

    /// <summary>
    /// Policy بازگشتی: زمانی که هیچ Policy دیگری منطبق نباشد، باز هم نیازمند
    /// کاربر احراز هویت‌شده است. بدون این، اندپوینت‌های بدون ویژگی ناشناس می‌مانند.
    /// </summary>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build());

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (Permissions.All.Contains(policyName, StringComparer.Ordinal))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        // نام Policy یک Policy معمولی است (نه مجوز)، مثل Policyهای سنتی.
        return _fallback.GetPolicyAsync(policyName);
    }
}
