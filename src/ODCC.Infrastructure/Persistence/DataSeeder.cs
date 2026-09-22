using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Domain.Modules.Organization.Enums;
using ODCC.Domain.Modules.Organization.Events;
using ODCC.Infrastructure.Modules.Identity.Entities;

namespace ODCC.Infrastructure.Persistence;

/// <summary>
/// پیاده‌سازی داده‌ی اولیه (bootstrap).
///
/// سه چیز ایجاد می‌شود:
/// ۱. واحد سازمانی ریشه (شرکت) — تا ساختار درختان سازمانی لنگر داشته باشد.
/// ۲. نقش مدیر کل با <b>تمام</b> مجوزهای تعریف‌شده (سیستمی و غیرقابل حذف).
/// ۳. کاربر مدیر کل متصل به نقش و واحد ریشه، با دامنه‌ی <see cref="DataScope.Company"/>.
///
/// <b>خودتوانی:</b> هر گام پیش از اجرا بررسی می‌کند که آیا داده وجود دارد یا نه.
/// اجرای مجدد هیچ تغییری ایجاد نمی‌کند. به همین دلیل تغییر رمز یا نام نمایشی
/// کاربر مدیر کل از طریق این کلاس انجام نمی‌شود — مدیریت آن پس از اولین ورود
/// بر عهده‌ی خود کاربر یا مدیر سامانه است.
/// </summary>
public sealed class DataSeeder(
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IOrgUnitRepository orgUnitRepository,
    IOrganizationUnitOfWork unitOfWork,
    IOptions<SeedOptions> options,
    ILogger<DataSeeder> logger) : IDataSeeder
{
    private readonly SeedOptions _options = options.Value;

    private static readonly Action<ILogger, string, Exception?> SeedFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(2, "SeedFailed"),
        "ایجاد داده‌ی اولیه برای {Step} ناموفق بود.");

    private static readonly Action<ILogger, string, Exception?> SeedSkipped = LoggerMessage.Define<string>(
        LogLevel.Information,
        new EventId(3, "SeedSkipped"),
        "از ایجاد داده‌ی اولیه صرف‌نظر شد: {Reason}");

    /// <summary>
    /// ایجاد داده‌ی اولیه. هر گام مستقل از گام‌های دیگر است؛ شکست یک گام
    /// مانع اجرای گام‌های بعدی نمی‌شود (و هرگز program را از کار نمی‌اندازد).
    /// </summary>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var rootOrgUnitId = await SeedRootOrgUnitAsync(ct);
        await SeedAdminRoleAsync(ct);
        await SeedAdminUserAsync(rootOrgUnitId, ct);
    }

    /// <summary>
    /// ایجاد واحد سازمانی ریشه در صورت نبودن. اگر هر واحد ریشه‌ای (بدون والد)
    /// وجود داشته باشد، از ایجاد یکی جدید صرف‌نظر می‌شود تا ساختار موجود
    /// دوباره‌نویسی نشود و کاربر مدیر کل به همان ریشه متصل می‌شود.
    /// </summary>
    private async Task<Guid?> SeedRootOrgUnitAsync(CancellationToken ct)
    {
        try
        {
            // اولین ریشه موجود (حتی با کدی متفاوت) به‌عنوان لنگر پذیرفته می‌شود.
            var existingRoot = await orgUnitRepository.FindRootAsync(ct);
            if (existingRoot is not null)
            {
                return existingRoot.Id;
            }

            var unit = new OrgUnit
            {
                Code = _options.RootOrgUnitCode,
                Name = _options.RootOrgUnitName,
                Type = OrgUnitType.Company,
                ParentId = null,
                IsActive = true
            };

            unit.SetPath(parentPath: null);
            unit.RaiseDomainEvent(new OrgUnitCreatedEvent(unit.Id, unit.Code, unit.Path, actorUserId: null));

            await orgUnitRepository.AddAsync(unit, ct);
            await unitOfWork.SaveChangesAsync(ct);

            return unit.Id;
        }
        catch (Exception ex)
        {
            // شکست ایجاد واحد ریشه نباید کل bootstrap را متوقف کند.
            SeedFailed(logger, nameof(SeedRootOrgUnitAsync), ex);
            return null;
        }
    }

    /// <summary>
    /// ایجاد نقش مدیر کل با تمام مجوزهای تعریف‌شده در صورت نبودن.
    /// نقش به‌صورت سیستمی علامت‌گذاری می‌شود تا قابل حذف یا تغییر نام نباشد.
    /// </summary>
    private async Task SeedAdminRoleAsync(CancellationToken ct)
    {
        try
        {
            var existing = await roleManager.FindByNameAsync(_options.AdminRoleName);
            if (existing is not null)
            {
                return;
            }

            var role = new ApplicationRole
            {
                Name = _options.AdminRoleName,
                DisplayName = _options.AdminRoleDisplayName,
                Description = "نقش سیستمی با تمام مجوزهای سامانه (به‌صورت خودکار ایجاد شده).",
                IsSystem = true
            };

            var createResult = await roleManager.CreateAsync(role);
            if (!createResult.Succeeded)
            {
                SeedFailed(logger, nameof(SeedAdminRoleAsync),
                    new InvalidOperationException(string.Join(", ", createResult.Errors.Select(e => e.Description))));
                return;
            }

            // انتصاب تمام مجوزهای تعریف‌شده به نقش مدیر کل.
            foreach (var permission in Permissions.All)
            {
                await roleManager.AddClaimAsync(role, new Claim(Permissions.ClaimType, permission));
            }
        }
        catch (Exception ex)
        {
            SeedFailed(logger, nameof(SeedAdminRoleAsync), ex);
        }
    }

    /// <summary>
    /// ایجاد کاربر مدیر کل در صورت نبودن.
    /// رمز عبور باید از پیکربندی تامین شده باشد؛ در غیر این صورت، ایجاد کاربر
    /// به‌صورت ایمن رد می‌شود (با هشدار) تا برنامه بوت شود.
    /// </summary>
    private async Task SeedAdminUserAsync(Guid? rootOrgUnitId, CancellationToken ct)
    {
        try
        {
            var existing = await userManager.FindByNameAsync(_options.AdminUserName);
            if (existing is not null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_options.AdminPassword))
            {
                SeedSkipped(logger, "رمز عبور مدیر کل در پیکربندی (Seed:AdminPassword) تنظیم نشده است.", null);
                return;
            }

            var user = new ApplicationUser
            {
                UserName = _options.AdminUserName,
                Email = _options.AdminEmail,
                EmailConfirmed = true,
                FirstName = _options.AdminFirstName,
                LastName = _options.AdminLastName,
                IsActive = true,
                // کاربر مدیر کل به کل شرکت دسترسی دارد.
                DataScope = DataScope.Company,
                OrgUnitId = rootOrgUnitId
            };

            // Identity رمز را هش می‌کند؛ هرگز متن‌وضوح ذخیره نمی‌شود.
            var createResult = await userManager.CreateAsync(user, _options.AdminPassword);
            if (!createResult.Succeeded)
            {
                SeedFailed(logger, nameof(SeedAdminUserAsync),
                    new InvalidOperationException(string.Join(", ", createResult.Errors.Select(e => e.Description))));
                return;
            }

            var addToRoleResult = await userManager.AddToRoleAsync(user, _options.AdminRoleName);
            if (!addToRoleResult.Succeeded)
            {
                SeedFailed(logger, nameof(SeedAdminUserAsync),
                    new InvalidOperationException(string.Join(", ", addToRoleResult.Errors.Select(e => e.Description))));
            }
        }
        catch (Exception ex)
        {
            SeedFailed(logger, nameof(SeedAdminUserAsync), ex);
        }
    }
}
