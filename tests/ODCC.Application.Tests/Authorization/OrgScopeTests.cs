using FluentAssertions;
using ODCC.Application.Authorization;
using ODCC.Domain.Common;
using Xunit;

namespace ODCC.Application.Tests.Authorization;

/// <summary>
/// آزمون‌های مرز دامنه‌ی سازمانی. این کلاس قلب امنیتی سیستم است:
/// اگر این آزمون‌ها پاس شوند، کاربر نمی‌تواند با دانستن یک شناسه
/// داده‌های خارج از دامنه‌ی خود را ببیند.
/// </summary>
public class OrgScopeTests
{
    private const string CompanyPath = "/hq";
    private const string DivisionPath = "/hq/fin";
    private const string DepartmentPath = "/hq/fin/ap";
    private const string TeamPath = "/hq/fin/ap/invoicing";
    private const string OtherDivisionPath = "/hq/ops";
    private const string OtherDepartmentPath = "/hq/ops/it";

    /// <summary>
    /// دامنه‌ی Company: دسترسی نامحدود اما فقط با صراحت.
    /// </summary>
    [Fact]
    public void Company_Scope_Is_Unrestricted_And_Can_Access_Everything()
    {
        var scope = new OrgScope
        {
            AnchorOrgUnitId = Guid.NewGuid(),
            AnchorPath = CompanyPath,
            Scope = DataScope.Company
        };

        scope.IsUnrestricted.Should().BeTrue();
        scope.HasVisibleOrgScope.Should().BeFalse();
        scope.VisiblePathPrefix.Should().BeNull();

        scope.CanAccess(CompanyPath).Should().BeTrue();
        scope.CanAccess(DivisionPath).Should().BeTrue();
        scope.CanAccess(DepartmentPath).Should().BeTrue();
        scope.CanAccess(TeamPath).Should().BeTrue();
        scope.CanAccess(OtherDivisionPath).Should().BeTrue();
    }

    /// <summary>
    /// دامنه‌ی Company حتی بدون لنگر هم نامحدود است (ادمین سراسری).
    /// </summary>
    [Fact]
    public void Company_Scope_Without_Anchor_Is_Still_Unrestricted()
    {
        var scope = new OrgScope { Scope = DataScope.Company };

        scope.IsUnrestricted.Should().BeTrue();
        scope.CanAccess(OtherDivisionPath).Should().BeTrue();
    }

    /// <summary>
    /// دامنه‌ی Division: فقط دایرکتی خودش و زیردرخت آن.
    /// </summary>
    [Fact]
    public void Division_Scope_Can_Access_Only_Self_Subtree()
    {
        var anchorId = Guid.NewGuid();
        var scope = new OrgScope
        {
            AnchorOrgUnitId = anchorId,
            AnchorPath = DivisionPath,
            Scope = DataScope.Division
        };

        scope.IsUnrestricted.Should().BeFalse();
        scope.HasVisibleOrgScope.Should().BeTrue();
        scope.VisiblePathPrefix.Should().Be(DivisionPath);

        scope.CanAccess(DivisionPath).Should().BeTrue();
        scope.CanAccess(DepartmentPath).Should().BeTrue();
        scope.CanAccess(TeamPath).Should().BeTrue();

        // واحدهای دیگر ممنوع.
        scope.CanAccess(OtherDivisionPath).Should().BeFalse();
        scope.CanAccess(OtherDepartmentPath).Should().BeFalse();
        scope.CanAccess(CompanyPath).Should().BeFalse();
    }

    /// <summary>
    /// دامنه‌ی Department: فقط دپارتمان خودش و زیردرخت آن.
    /// </summary>
    [Fact]
    public void Department_Scope_Can_Access_Only_Self_Subtree()
    {
        var scope = new OrgScope
        {
            AnchorOrgUnitId = Guid.NewGuid(),
            AnchorPath = DepartmentPath,
            Scope = DataScope.Department
        };

        scope.CanAccess(DepartmentPath).Should().BeTrue();
        scope.CanAccess(TeamPath).Should().BeTrue();

        // والد و خواهرها ممنوع.
        scope.CanAccess(DivisionPath).Should().BeFalse();
        scope.CanAccess(OtherDepartmentPath).Should().BeFalse();
    }

    /// <summary>
    /// دامنه‌ی Team: فقط تیم خودش.
    /// </summary>
    [Fact]
    public void Team_Scope_Can_Access_Only_Self()
    {
        var scope = new OrgScope
        {
            AnchorOrgUnitId = Guid.NewGuid(),
            AnchorPath = TeamPath,
            Scope = DataScope.Team
        };

        scope.CanAccess(TeamPath).Should().BeTrue();

        // همه‌ی اجداد و خواهرها ممنوع.
        scope.CanAccess(DepartmentPath).Should().BeFalse();
        scope.CanAccess(DivisionPath).Should().BeFalse();
        scope.CanAccess(CompanyPath).Should().BeFalse();
    }

    /// <summary>
    /// مهم‌ترین آزمون: دسترسی متقابل بین دو دایرکتی مجاز نیست.
    /// </summary>
    [Fact]
    public void Cross_Division_Access_Is_Denied()
    {
        var scope = new OrgScope
        {
            AnchorOrgUnitId = Guid.NewGuid(),
            AnchorPath = DivisionPath,
            Scope = DataScope.Division
        };

        scope.CanAccess(OtherDivisionPath).Should().BeFalse();
        scope.CanAccess(OtherDepartmentPath).Should().BeFalse();

        // با شناسه‌ی واحد هم نباید بشود دور زد.
        var otherUnitId = Guid.NewGuid();
        scope.CanAccess(otherUnitId, OtherDepartmentPath).Should().BeFalse();
    }

    /// <summary>
    /// مرزها با «/» بررسی می‌شوند تا «/hq/fin» با «/hq/finance» اشتباه گرفته نشود.
    /// این یک حمله‌ی رایج پیشوندی است.
    /// </summary>
    [Fact]
    public void Sibling_Path_With_Common_Prefix_Is_Denied()
    {
        // "/hq/fin" و "/hq/finance" هم‌پیشوند اما خواهر هستند.
        var scope = new OrgScope
        {
            AnchorOrgUnitId = Guid.NewGuid(),
            AnchorPath = "/hq/fin",
            Scope = DataScope.Department
        };

        scope.CanAccess("/hq/finance").Should().BeFalse();
        scope.CanAccess("/hq/fin").Should().BeTrue();
        scope.CanAccess("/hq/finance/team").Should().BeFalse();
    }

    /// <summary>
    /// fail-closed: دامنه‌ی Own هیچ داده‌ی سازمانی نمی‌بیند.
    /// این تضمین می‌کند که «نداشتن دامنه» هرگز به «دسترسی نامحدود» تبدیل نشود.
    /// </summary>
    [Fact]
    public void Own_Scope_Sees_No_Org_Data()
    {
        var scope = new OrgScope
        {
            AnchorOrgUnitId = Guid.NewGuid(),
            AnchorPath = DepartmentPath,
            Scope = DataScope.Own
        };

        scope.IsUnrestricted.Should().BeFalse();
        scope.HasVisibleOrgScope.Should().BeFalse();
        scope.VisiblePathPrefix.Should().BeNull();

        scope.CanAccess(DepartmentPath).Should().BeFalse();
        scope.CanAccess(TeamPath).Should().BeFalse();
        scope.CanAccess(CompanyPath).Should().BeFalse();
    }

    /// <summary>
    /// fail-closed: دامنه‌ی Default (Empty) هیچ داده‌ای نمی‌بیند.
    /// </summary>
    [Fact]
    public void Empty_Scope_Sees_Nothing()
    {
        var scope = OrgScope.Empty;

        scope.IsUnrestricted.Should().BeFalse();
        scope.HasVisibleOrgScope.Should().BeFalse();
        scope.CanAccess(DepartmentPath).Should().BeFalse();
        scope.CanAccess((string?)null).Should().BeFalse();
    }

    /// <summary>
    /// fail-closed: لنگر نامعتبر (مسیر خالی) حتی با دامنه‌ی غیر Own هم نمی‌بیند.
    /// </summary>
    [Fact]
    public void Invalid_Anchor_Path_Sees_Nothing()
    {
        var scope = new OrgScope
        {
            AnchorOrgUnitId = Guid.NewGuid(),
            AnchorPath = string.Empty,
            Scope = DataScope.Department
        };

        scope.IsUnrestricted.Should().BeFalse();
        scope.HasVisibleOrgScope.Should().BeFalse();
        scope.CanAccess(DepartmentPath).Should().BeFalse();
    }

    /// <summary>
    /// fail-closed: لنگر null با دامنه‌ی غیر Company نمی‌بیند.
    /// </summary>
    [Fact]
    public void Null_Anchor_With_Limited_Scope_Sees_Nothing()
    {
        var scope = new OrgScope
        {
            AnchorOrgUnitId = null,
            AnchorPath = null,
            Scope = DataScope.Department
        };

        scope.HasVisibleOrgScope.Should().BeFalse();
        scope.CanAccess(DepartmentPath).Should().BeFalse();
        scope.VisiblePathPrefix.Should().BeNull();
    }

    /// <summary>
    /// منبع بدون مسیر سازمانی: رد می‌شود (مگر دامنه‌ی Company).
    /// </summary>
    [Fact]
    public void Resource_Without_Path_Is_Denied_For_Limited_Scopes()
    {
        var scope = new OrgScope
        {
            AnchorOrgUnitId = Guid.NewGuid(),
            AnchorPath = DivisionPath,
            Scope = DataScope.Division
        };

        scope.CanAccess((string?)null).Should().BeFalse();
        scope.CanAccess(string.Empty).Should().BeFalse();
        scope.CanAccess("   ").Should().BeFalse();
    }

    /// <summary>
    /// بررسی با شناسه‌ی واحد: وقتی مسیر موجود نیست، فقط خود واحد لنگر پذیرفته می‌شود.
    /// </summary>
    [Fact]
    public void Access_By_Id_Without_Path_Only_Allows_Anchor_Itself()
    {
        var anchorId = Guid.NewGuid();
        var scope = new OrgScope
        {
            AnchorOrgUnitId = anchorId,
            AnchorPath = DivisionPath,
            Scope = DataScope.Division
        };

        scope.CanAccess(anchorId, resourceOrgPath: null).Should().BeTrue();
        scope.CanAccess(Guid.NewGuid(), resourceOrgPath: null).Should().BeFalse();
        scope.CanAccess((Guid?)null, resourceOrgPath: null).Should().BeFalse();
    }

    /// <summary>
    /// بررسی با شناسه‌ی واحد: مسیر دقیق‌ترین بررسی را انجام می‌دهد.
    /// </summary>
    [Fact]
    public void Access_By_Id_Uses_Path_When_Available()
    {
        var anchorId = Guid.NewGuid();
        var scope = new OrgScope
        {
            AnchorOrgUnitId = anchorId,
            AnchorPath = DivisionPath,
            Scope = DataScope.Division
        };

        // شناسه‌ی لنگر ولی مسیر خارج از دامنه → رد (شناسه قابل اعتماد نیست).
        scope.CanAccess(anchorId, OtherDepartmentPath).Should().BeFalse();

        // شناسه‌ی دیگر ولی مسیر داخل دامنه → مجاز.
        scope.CanAccess(Guid.NewGuid(), DepartmentPath).Should().BeTrue();
    }

    /// <summary>
    /// VisiblePathPrefix همیشه مسیر لنگر است (نه چیز دیگر) و فقط برای دامنه‌های قابل‌مشاهده.
    /// </summary>
    [Fact]
    public void VisiblePathPrefix_Is_Anchor_Path_When_Visible()
    {
        var scope = new OrgScope
        {
            AnchorOrgUnitId = Guid.NewGuid(),
            AnchorPath = TeamPath,
            Scope = DataScope.Team
        };

        scope.VisiblePathPrefix.Should().Be(TeamPath);
    }

    /// <summary>
    /// مجموعه کامل سناریوها: هر دامنه در برابر هر مسیر منبع.
    /// </summary>
    [Theory]
    [InlineData(DataScope.Own, "/hq/fin/ap", "/hq/fin/ap/team1", false)]
    [InlineData(DataScope.Team, "/hq/fin/ap/team1", "/hq/fin/ap/team1", true)]
    [InlineData(DataScope.Team, "/hq/fin/ap/team1", "/hq/fin/ap", false)]
    [InlineData(DataScope.Department, "/hq/fin/ap", "/hq/fin/ap/team1", true)]
    [InlineData(DataScope.Department, "/hq/fin/ap", "/hq/fin", false)]
    [InlineData(DataScope.Division, "/hq/fin", "/hq/fin/ap", true)]
    [InlineData(DataScope.Division, "/hq/fin", "/hq/ops", false)]
    [InlineData(DataScope.Company, "/hq", "/hq/ops/it", true)]
    public void Scope_Matrix(DataScope scopeType, string anchor, string resource, bool expected)
    {
        var scope = new OrgScope
        {
            AnchorOrgUnitId = Guid.NewGuid(),
            AnchorPath = anchor,
            Scope = scopeType
        };

        scope.CanAccess(resource).Should().Be(expected);
    }
}
