using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ODCC.Domain.Modules.Identity.Entities;
using ODCC.Infrastructure.Modules.Identity.Entities;

namespace ODCC.Infrastructure.Modules.Identity.Persistence;

/// <summary>
/// DbContext اختصاصی ماژول هویت.
///
/// جداول ASP.NET Core Identity (Users, Roles, UserRoles, RoleClaims, ...) را به‌همراه
/// جدول توکن‌های تازه‌سازی مدیریت می‌کند. نام جداول snake_case است.
/// سایر ماژول‌ها هرگز از این DbContext استفاده مستقیم نمی‌کنند؛ فقط از طریق
/// قراردادهای ماژول هویت (IUserService, IRoleService, IAuthService).
/// </summary>
public class IdentityDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // جداول Identity را به snake_case تغییر نام می‌دهیم.
        foreach (var entity in builder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (!string.IsNullOrEmpty(tableName))
            {
                entity.SetTableName(ToSnakeCase(tableName));
            }
        }

        builder.Entity<ApplicationUser>(b =>
        {
            b.ToTable("asp_net_users");
            b.Property(u => u.FirstName).HasMaxLength(128).IsRequired();
            b.Property(u => u.LastName).HasMaxLength(128).IsRequired();
            b.Property(u => u.NationalCode).HasMaxLength(10);
            b.Property(u => u.AvatarUrl).HasMaxLength(512);
            b.Property(u => u.DataScope).HasConversion<int>();
            b.Property(u => u.CreatedAt).HasColumnType("datetime2");
            b.Property(u => u.UpdatedAt).HasColumnType("datetime2");
            b.Property(u => u.DeactivatedAt).HasColumnType("datetime2");
            b.HasIndex(u => u.OrgUnitId);
            b.HasQueryFilter(u => !u.IsDeleted); // حذف نرم
        });

        builder.Entity<ApplicationRole>(b =>
        {
            b.ToTable("asp_net_roles");
            b.Property(r => r.DisplayName).HasMaxLength(128);
            b.Property(r => r.Description).HasMaxLength(512);
            b.HasQueryFilter(r => !r.IsDeleted); // حذف نرم
        });

        builder.Entity<RefreshToken>(b =>
        {
            b.ToTable("refresh_tokens");
            b.HasKey(t => t.Id);
            b.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            b.Property(t => t.ReplacedByTokenHash).HasMaxLength(128);
            b.Property(t => t.DeviceInfo).HasMaxLength(512);
            b.Property(t => t.CreatedByIp).HasMaxLength(64);
            b.Property(t => t.RevokedByIp).HasMaxLength(64);
            b.Property(t => t.ExpiresAt).HasColumnType("datetime2");
            b.Property(t => t.RevokedAt).HasColumnType("datetime2");
            b.HasIndex(t => t.TokenHash).IsUnique();
            b.HasIndex(t => t.UserId);
            b.HasIndex(t => t.FamilyId);
            b.HasQueryFilter(t => t.RevokedAt == null);
        });
    }

    /// <summary>تبدیل PascalCase به snake_case برای نام جداول Identity.</summary>
    private static string ToSnakeCase(string name)
    {
        return string.Concat(name.Select((c, i) =>
            i > 0 && char.IsUpper(c) ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));
    }
}
