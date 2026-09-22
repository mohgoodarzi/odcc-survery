using FluentAssertions;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Infrastructure.Services;
using Xunit;

namespace ODCC.Infrastructure.Tests.Services;

/// <summary>
/// آزمون‌های OrgScopeProvider. برای جلوگیری از وابستگی به پایگاه‌داده،
/// از یک مخزن جعلی (Fake) استفاده می‌شود که مسیرها را در حافظه نگه می‌دارد.
/// </summary>
public class OrgScopeProviderTests
{
    private static readonly Guid AnchorId = Guid.NewGuid();
    private const string AnchorPath = "/hq/fin/ap";

    private sealed class FakeOrgUnitRepository : IOrgUnitRepository
    {
        private readonly Dictionary<Guid, string> _paths = new();

        public void Add(Guid id, string path) => _paths[id] = path;

        public Task<string?> GetPathAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(_paths.TryGetValue(id, out var path) ? path : null);

        // اعضای استفاده‌نشده در این آزمون‌ها — پیاده‌سازی‌های حداقلی.
        public Task<OrgUnit?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OrgUnit>> ListAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(OrgUnit entity, CancellationToken ct = default) => throw new NotSupportedException();
        public void Remove(OrgUnit entity) => throw new NotSupportedException();
        public void Update(OrgUnit entity) => throw new NotSupportedException();
        public Task<OrgUnit?> FindByCodeAsync(string code, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<OrgUnit?> FindRootAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<OrgUnit>> GetDescendantsAsync(Guid rootId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountChildrenAsync(Guid parentId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<int> CountEmployeesAsync(Guid orgUnitId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string? UserName { get; set; } = "test";
        public string? DisplayName { get; set; }
        public bool IsAuthenticated { get; set; } = true;
        public IReadOnlyCollection<string> Roles { get; set; } = [];
        public IReadOnlyCollection<string> Permissions { get; set; } = [];
        public Guid? OrgUnitId { get; set; } = AnchorId;
        public DataScope DataScope { get; set; } = DataScope.Department;
        public string? ClientIpAddress { get; set; }
        public bool IsInRole(params string[] roles) => throw new NotSupportedException();
        public bool HasPermission(string permission) => throw new NotSupportedException();
        public bool HasPermission(Permissions.PermissionKey permission) => throw new NotSupportedException();
    }

    private static (OrgScopeProvider provider, FakeCurrentUserService current, FakeOrgUnitRepository repo) Create()
    {
        var current = new FakeCurrentUserService();
        var repo = new FakeOrgUnitRepository();
        repo.Add(AnchorId, AnchorPath);
        return (new OrgScopeProvider(current, repo), current, repo);
    }

    [Fact]
    public async Task Unauthenticated_User_Gets_Empty_Scope()
    {
        var (provider, current, _) = Create();
        current.IsAuthenticated = false;

        var scope = await provider.GetCurrentScopeAsync();

        scope.Should().Be(OrgScope.Empty);
        scope.HasVisibleOrgScope.Should().BeFalse();
    }

    [Fact]
    public async Task Company_Scope_Is_Unrestricted_Without_Db_Lookup()
    {
        var (provider, current, repo) = Create();
        current.DataScope = DataScope.Company;
        // حتی اگر لنگر در مخزن نباشد، دامنه‌ی Company باید کار کند.
        current.OrgUnitId = Guid.NewGuid();

        var scope = await provider.GetCurrentScopeAsync();

        scope.IsUnrestricted.Should().BeTrue();
    }

    [Fact]
    public async Task Own_Scope_Gets_Empty_Scope()
    {
        var (provider, current, _) = Create();
        current.DataScope = DataScope.Own;

        var scope = await provider.GetCurrentScopeAsync();

        scope.HasVisibleOrgScope.Should().BeFalse();
        scope.IsUnrestricted.Should().BeFalse();
    }

    [Fact]
    public async Task Department_Scope_Resolves_Anchor_Path()
    {
        var (provider, _, _) = Create();

        var scope = await provider.GetCurrentScopeAsync();

        scope.HasVisibleOrgScope.Should().BeTrue();
        scope.AnchorPath.Should().Be(AnchorPath);
        scope.VisiblePathPrefix.Should().Be(AnchorPath);
    }

    /// <summary>
    /// fail-closed: اگر لنگر در مخزن وجود نداشته باشد (مثلاً حذف شده)،
    /// دامنه‌ی خالی برمی‌گردد تا هرگز دسترسی بیش از حد داده نشود.
    /// </summary>
    [Fact]
    public async Task Invalid_Anchor_Gets_Empty_Scope()
    {
        var (provider, current, repo) = Create();
        var missingId = Guid.NewGuid();
        current.OrgUnitId = missingId;
        current.DataScope = DataScope.Department;
        repo.Add(missingId, string.Empty); // مسیر خالی → نامعتبر

        var scope = await provider.GetCurrentScopeAsync();

        scope.HasVisibleOrgScope.Should().BeFalse();
    }

    [Fact]
    public async Task Missing_Anchor_Gets_Empty_Scope()
    {
        var (provider, current, _) = Create();
        current.OrgUnitId = null;
        current.DataScope = DataScope.Department;

        var scope = await provider.GetCurrentScopeAsync();

        scope.HasVisibleOrgScope.Should().BeFalse();
    }

    /// <summary>
    /// Team/Division/Department همگی از طریق مسیر مادی کار می‌کنند —
    /// تفاوت فقط در خود مقدار DataScope است که در OrgScopeTests پوشش داده شده.
    /// </summary>
    [Theory]
    [InlineData(DataScope.Team)]
    [InlineData(DataScope.Department)]
    [InlineData(DataScope.Division)]
    public async Task Limited_Scopes_Resolve_Anchor_Path(DataScope scopeType)
    {
        var (provider, current, _) = Create();
        current.DataScope = scopeType;

        var scope = await provider.GetCurrentScopeAsync();

        scope.AnchorPath.Should().Be(AnchorPath);
        scope.Scope.Should().Be(scopeType);
        scope.HasVisibleOrgScope.Should().BeTrue();
    }
}
