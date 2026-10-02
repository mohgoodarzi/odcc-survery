using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Domain.Modules.Organization.Enums;
using ODCC.Infrastructure.Modules.Organization.Services;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Organization;

/// <summary>
/// آزمون‌های سرویس واحدهای سازمانی.
///
/// این آزمون‌ها ریشه‌ی باگ «عدم نمایش واحدها در صفحه‌ی مدیریت» را نگه‌داری
/// می‌کنند: مسیر «درخت» (صفحه‌ی مدیریت) و مسیر «لیست» (مثلاً dropdown انتخاب
/// واحد در فرم کارمند) باید دقیقاً همان مجموعه‌ی واحدها را با همان قوانین
/// فیلترگذاری ببینند. در غیر این صورت واحدی در dropdown ظاهر می‌شود که در
/// صفحه‌ی مدیریت غایب است (یا برعکس).
/// </summary>
public class OrgUnitServiceTests
{
    private static readonly string[] ModulePermissions =
    [
        Permissions.Organization.UnitsView,
        Permissions.Organization.UnitsManage
    ];

    private static readonly string[] SeedCodes = ["hq", "fin", "ops"];
    private static readonly string[] ExtraCodes = ["LEGACY", "ACTIVE"];
    private static readonly string[] DivisionCodes = ["fin", "ops"];

    /// <summary>ساخت محیط با کاربر مدیر کل (دسترسی سراسری) و ساختار نمونه.</summary>
    private static async Task<(TestEnvironment env, OrgUnitService service)> CreateUnrestrictedAsync()
    {
        var env = await TestEnvironment.CreateAsync();
        await env.SeedOrgHierarchyAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ModulePermissions);
        var service = (OrgUnitService)env.Services.GetRequiredService<IOrgUnitService>();
        return (env, service);
    }

    /// <summary>ساخت یک واحد جدید (فعال یا غیرفعال) زیر ریشه‌ی شرکت.</summary>
    private static async Task<OrgUnit> AddUnitAsync(TestEnvironment env, string code, bool isActive)
    {
        var company = await env.OrganizationDbContext.OrgUnits.FirstAsync(u => u.ParentId == null);
        var unit = new OrgUnit
        {
            Code = code,
            Name = $"واحد {code}",
            Type = OrgUnitType.Department,
            ParentId = company.Id,
            IsActive = isActive
        };
        unit.SetPath(company.Path);
        env.OrganizationDbContext.OrgUnits.Add(unit);
        await env.OrganizationDbContext.SaveChangesAsync();
        return unit;
    }

    /// <summary>جمع‌آوری همه‌ی گره‌های درخت به‌صورت مسطح.</summary>
    private static List<string> FlattenCodes(IReadOnlyList<OrgUnitTreeDto> roots)
    {
        var codes = new List<string>();

        void Walk(IEnumerable<OrgUnitTreeDto> nodes)
        {
            foreach (var node in nodes)
            {
                codes.Add(node.Code);
                Walk(node.Children);
            }
        }

        Walk(roots);
        return codes;
    }

    [Fact]
    public async Task GetTreeAsync_includes_inactive_units_so_management_page_can_show_them()
    {
        var (env, service) = await CreateUnrestrictedAsync();
        await AddUnitAsync(env, "LEGACY", isActive: false);
        await AddUnitAsync(env, "ACTIVE", isActive: true);

        var result = await service.GetTreeAsync();

        result.IsSuccess.Should().BeTrue();
        FlattenCodes(result.Value!).Should().Contain(SeedCodes);
        FlattenCodes(result.Value!).Should().Contain(ExtraCodes);
    }

    [Fact]
    public async Task GetTreeAsync_and_ListAsync_expose_the_same_units()
    {
        var (env, service) = await CreateUnrestrictedAsync();
        await AddUnitAsync(env, "LEGACY", isActive: false);

        var tree = await service.GetTreeAsync();
        var list = await service.ListAsync(activeOnly: false);

        tree.IsSuccess.Should().BeTrue();
        list.IsSuccess.Should().BeTrue();

        // منبع داده‌ی صفحه‌ی مدیریت و dropdown باید یکسان باشند.
        FlattenCodes(tree.Value!).OrderBy(c => c)
            .Should().BeEquivalentTo(list.Value!.Select(u => u.Code).OrderBy(c => c));
    }

    [Fact]
    public async Task ListAsync_active_only_hides_inactive_units_but_management_tree_does_not()
    {
        var (env, service) = await CreateUnrestrictedAsync();
        await AddUnitAsync(env, "LEGACY", isActive: false);

        var activeOnly = await service.ListAsync(activeOnly: true);
        var all = await service.ListAsync(activeOnly: false);
        var tree = await service.GetTreeAsync();

        activeOnly.Value!.Select(u => u.Code).Should().NotContain("LEGACY");
        all.Value!.Select(u => u.Code).Should().Contain("LEGACY");
        FlattenCodes(tree.Value!).Should().Contain("LEGACY");
    }

    [Fact]
    public async Task GetTreeAsync_builds_parent_child_hierarchy()
    {
        var (env, service) = await CreateUnrestrictedAsync();

        var result = await service.GetTreeAsync();

        result.IsSuccess.Should().BeTrue();
        var roots = result.Value!;
        roots.Should().HaveCount(1);
        roots[0].Code.Should().Be("hq");
        roots[0].Children.Select(c => c.Code).Should().BeEquivalentTo(DivisionCodes);
    }
}
