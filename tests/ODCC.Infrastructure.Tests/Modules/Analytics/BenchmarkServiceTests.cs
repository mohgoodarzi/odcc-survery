using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Modules.Analytics.Abstractions;
using ODCC.Application.Modules.Analytics.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Analytics.Enums;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Analytics;

/// <summary>
/// آزمون‌های مدیریت بنچمارک‌ها: ایجاد/به‌روزرسانی، حذف نرم، فیلتر جستجو بر
/// اساس فعال بودن، و حل بنچمارک‌های قابل‌اعمال (سراسری شرکت در برابر
/// بنچمارک‌های واحد سازمانی و اجداد آن).
///
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ
/// پایگاه‌داده‌ی واقعی را لمس نمی‌کنند.
/// </summary>
public class BenchmarkServiceTests
{
    /// <summary>ساخت سلسله‌مراتب سازمانی نمونه و بازگرداندن شناسه‌های واحدها.</summary>
    private static async Task<(Guid companyId, Guid divisionId, Guid departmentId, Guid otherDepartmentId, Guid orphanUnitId)>
        SeedOrgHierarchyAsync(TestEnvironment env)
    {
        var company = NewUnit("hq", "شرکت", ODCC.Domain.Modules.Organization.Enums.OrgUnitType.Company, null);
        var division = NewUnit("fin", "امور مالی", ODCC.Domain.Modules.Organization.Enums.OrgUnitType.Division, company);
        var department = NewUnit("ap", "حساب‌های پرداختنی", ODCC.Domain.Modules.Organization.Enums.OrgUnitType.Department, division);
        var otherDepartment = NewUnit("it", "فناوری اطلاعات", ODCC.Domain.Modules.Organization.Enums.OrgUnitType.Department, company);
        var orphan = NewUnit("orphan", "یاقتی", ODCC.Domain.Modules.Organization.Enums.OrgUnitType.Team, null);

        env.OrganizationDbContext.OrgUnits.AddRange(company, division, department, otherDepartment, orphan);
        await env.OrganizationDbContext.SaveChangesAsync();

        return (company.Id, division.Id, department.Id, otherDepartment.Id, orphan.Id);
    }

    private static ODCC.Domain.Modules.Organization.Entities.OrgUnit NewUnit(
        string code, string name, ODCC.Domain.Modules.Organization.Enums.OrgUnitType type,
        ODCC.Domain.Modules.Organization.Entities.OrgUnit? parent)
    {
        var unit = new ODCC.Domain.Modules.Organization.Entities.OrgUnit
        {
            Code = code,
            Name = name,
            Type = type,
            ParentId = parent?.Id
        };
        unit.SetPath(parent?.Path);
        return unit;
    }

    [Fact]
    public async Task Create_Persists_Company_Wide_Benchmark_Without_OrgUnit()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<IBenchmarkService>();

        var result = await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف NPS سالانه",
            Metric = MetricType.Nps,
            TargetValue = 40m,
            IsCompanyWide = true,
            Description = "هدف کل شرکت"
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("هدف NPS سالانه");
        result.Value.Metric.Should().Be(MetricType.Nps);
        result.Value.TargetValue.Should().Be(40m);
        result.Value.IsCompanyWide.Should().BeTrue();
        result.Value.IsActive.Should().BeTrue();
        result.Value.OrgUnitId.Should().BeNull();
        result.Value.OrgUnitPath.Should().BeNull();

        var row = await env.AnalyticsDbContext.Benchmarks.SingleAsync();
        row.Name.Should().Be("هدف NPS سالانه");
        row.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Create_Resolves_OrgUnit_Path_For_Department_Benchmark()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var units = await SeedOrgHierarchyAsync(env);

        var service = env.Services.GetRequiredService<IBenchmarkService>();

        var result = await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف CSAT دپارتمان",
            Metric = MetricType.Csat,
            TargetValue = 85m,
            IsCompanyWide = false,
            OrgUnitId = units.departmentId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsCompanyWide.Should().BeFalse();
        result.Value.OrgUnitId.Should().Be(units.departmentId);
        result.Value.OrgUnitPath.Should().Be("/hq/fin/ap");
    }

    [Fact]
    public async Task Update_Changes_Target_Value()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<IBenchmarkService>();

        var created = await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف NPS",
            Metric = MetricType.Nps,
            TargetValue = 30m,
            IsCompanyWide = true
        });

        var updated = await service.UpdateAsync(created.Value!.Id, new SaveBenchmarkRequest
        {
            Name = "هدف NPS بازنگری‌شده",
            Metric = MetricType.Nps,
            TargetValue = 50m,
            IsCompanyWide = true
        });

        updated.IsSuccess.Should().BeTrue();
        updated.Value!.Name.Should().Be("هدف NPS بازنگری‌شده");
        updated.Value.TargetValue.Should().Be(50m);

        // همچنان یک ردیف — جایگزینی، نه الحاق.
        var rows = await env.AnalyticsDbContext.Benchmarks.ToListAsync();
        rows.Should().ContainSingle();
        rows[0].TargetValue.Should().Be(50m);
    }

    [Fact]
    public async Task Update_Rejects_Unknown_Benchmark()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<IBenchmarkService>();

        var result = await service.UpdateAsync(Guid.NewGuid(), new SaveBenchmarkRequest
        {
            Name = "هدف",
            Metric = MetricType.Nps,
            TargetValue = 10m,
            IsCompanyWide = true
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("benchmark_not_found");
    }

    [Fact]
    public async Task Delete_Soft_Deactivates_Benchmark()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<IBenchmarkService>();

        var created = await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف موقت",
            Metric = MetricType.Ces,
            TargetValue = 60m,
            IsCompanyWide = true
        });

        var delete = await service.DeleteAsync(created.Value!.Id);
        delete.IsSuccess.Should().BeTrue();

        // حذف نرم: ردیف باقی می‌ماند ولی غیرفعال است.
        var row = await env.AnalyticsDbContext.Benchmarks.SingleAsync();
        row.IsActive.Should().BeFalse();

        var byId = await service.GetByIdAsync(created.Value.Id);
        byId.IsSuccess.Should().BeTrue();
        byId.Value!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_Rejects_Unknown_Benchmark()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<IBenchmarkService>();

        var result = await service.DeleteAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("benchmark_not_found");
    }

    [Fact]
    public async Task Search_Hides_Inactive_Benchmarks_By_Default()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<IBenchmarkService>();

        await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف فعال",
            Metric = MetricType.Nps,
            TargetValue = 40m,
            IsCompanyWide = true
        });

        var inactive = await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف غیرفعال",
            Metric = MetricType.Nps,
            TargetValue = 10m,
            IsCompanyWide = true
        });
        await service.DeleteAsync(inactive.Value!.Id);

        var activeOnly = await service.SearchAsync(searchText: null, includeInactive: false);
        activeOnly.TotalCount.Should().Be(1);
        activeOnly.Items[0].Name.Should().Be("هدف فعال");

        var all = await service.SearchAsync(searchText: null, includeInactive: true);
        all.TotalCount.Should().Be(2);
        all.Items.Should().Contain(b => b.Name == "هدف غیرفعال" && b.IsActive == false);
    }

    [Fact]
    public async Task Search_Filters_By_Search_Text()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<IBenchmarkService>();

        await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف NPS شرکت",
            Metric = MetricType.Nps,
            TargetValue = 40m,
            IsCompanyWide = true
        });

        await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف CSAT فروش",
            Metric = MetricType.Csat,
            TargetValue = 80m,
            IsCompanyWide = true
        });

        var hit = await service.SearchAsync(searchText: "NPS", includeInactive: true);
        hit.TotalCount.Should().Be(1);
        hit.Items[0].Name.Should().Be("هدف NPS شرکت");

        var none = await service.SearchAsync(searchText: "وجود ندارد", includeInactive: true);
        none.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetApplicable_Returns_Company_Wide_When_No_OrgUnitPath()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var units = await SeedOrgHierarchyAsync(env);

        var service = env.Services.GetRequiredService<IBenchmarkService>();
        var repository = env.Services.GetRequiredService<IBenchmarkRepository>();

        var companyWide = await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف سراسری",
            Metric = MetricType.Nps,
            TargetValue = 40m,
            IsCompanyWide = true
        });

        var departmentBenchmark = await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف دپارتمان",
            Metric = MetricType.Nps,
            TargetValue = 60m,
            IsCompanyWide = false,
            OrgUnitId = units.departmentId
        });

        // بدون مسیر سازمانی: فقط بنچمارک‌های سراسری شرکت قابل‌اعمال هستند.
        var applicable = await repository.GetApplicableAsync(MetricType.Nps, orgUnitPath: null);

        applicable.Should().ContainSingle(b => b.Id == companyWide.Value!.Id);
        applicable.Should().NotContain(b => b.Id == departmentBenchmark.Value!.Id);
    }

    [Fact]
    public async Task GetApplicable_Includes_Company_Wide_And_Ancestor_Unit_Benchmarks()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var units = await SeedOrgHierarchyAsync(env);

        var service = env.Services.GetRequiredService<IBenchmarkService>();
        var repository = env.Services.GetRequiredService<IBenchmarkRepository>();

        var companyWide = await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف سراسری",
            Metric = MetricType.Csat,
            TargetValue = 70m,
            IsCompanyWide = true
        });

        var divisionBenchmark = await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف دایرکتی",
            Metric = MetricType.Csat,
            TargetValue = 75m,
            IsCompanyWide = false,
            OrgUnitId = units.divisionId
        });

        var otherDepartmentBenchmark = await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف دپارتمان دیگر",
            Metric = MetricType.Csat,
            TargetValue = 80m,
            IsCompanyWide = false,
            OrgUnitId = units.otherDepartmentId
        });

        // مسیر دپارتمان «حساب‌های پرداختنی»: سراسری + دایرکتی (اجداد) قابل‌اعمال‌اند،
        // ولی هدف دپارتمانِ هم‌سطح یا زیردرختِ مجزا نیست.
        var applicable = await repository.GetApplicableAsync(MetricType.Csat, orgUnitPath: "/hq/fin/ap");

        applicable.Should().Contain(b => b.Id == companyWide.Value!.Id);
        applicable.Should().Contain(b => b.Id == divisionBenchmark.Value!.Id);
        applicable.Should().NotContain(b => b.Id == otherDepartmentBenchmark.Value!.Id);
    }

    [Fact]
    public async Task GetApplicable_Ignores_Inactive_Benchmarks()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<IBenchmarkService>();
        var repository = env.Services.GetRequiredService<IBenchmarkRepository>();

        var active = await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف فعال",
            Metric = MetricType.Ces,
            TargetValue = 50m,
            IsCompanyWide = true
        });

        var inactive = await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف غیرفعال",
            Metric = MetricType.Ces,
            TargetValue = 90m,
            IsCompanyWide = true
        });
        await service.DeleteAsync(inactive.Value!.Id);

        var applicable = await repository.GetApplicableAsync(MetricType.Ces, orgUnitPath: null);

        applicable.Should().ContainSingle(b => b.Id == active.Value!.Id);
        applicable.Should().NotContain(b => b.Id == inactive.Value!.Id);
    }

    [Fact]
    public async Task GetApplicable_Filters_By_Metric_Type()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<IBenchmarkService>();
        var repository = env.Services.GetRequiredService<IBenchmarkRepository>();

        await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف NPS",
            Metric = MetricType.Nps,
            TargetValue = 40m,
            IsCompanyWide = true
        });

        await service.CreateAsync(new SaveBenchmarkRequest
        {
            Name = "هدف CSAT",
            Metric = MetricType.Csat,
            TargetValue = 80m,
            IsCompanyWide = true
        });

        var npsOnly = await repository.GetApplicableAsync(MetricType.Nps, orgUnitPath: null);

        npsOnly.Should().ContainSingle();
        npsOnly[0].Metric.Should().Be(MetricType.Nps);
    }
}
