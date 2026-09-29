using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ODCC.Domain.Modules.SystemConfiguration.Entities;
using ODCC.Domain.Modules.SystemConfiguration.Enums;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Modules.SystemConfiguration.Persistence;

/// <summary>
/// DbContext ماژول پیکربندی سامانه.
///
/// جداول این ماژول مستقل است. این ماژول فقط کلید/مقدار را نگه می‌دارد و هرگز
/// به موجودیت‌های ماژول دیگر ارجاع مستقیم ندارد (ارتباط فقط با شناسه‌ی کاربر
/// به‌صورت snapshot است).
/// </summary>
public class SystemConfigurationDbContext(DbContextOptions<SystemConfigurationDbContext> options) : DbContext(options)
{
    private static readonly JsonSerializerOptions ListJsonOptions = new(JsonSerializerDefaults.Web);
    /// <summary>تنظیمات سامانه.</summary>
    public DbSet<Setting> Settings => Set<Setting>();

    /// <summary>پرچم‌های ویژگی.</summary>
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();

    /// <summary>سیاست‌های سیستمی.</summary>
    public DbSet<SystemPolicy> SystemPolicies => Set<SystemPolicy>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<Setting>(b =>
        {
            b.ConfigureBase("system_settings", provider);

            b.Property(s => s.Key).HasMaxLength(256).IsRequired();
            b.Property(s => s.Name).HasMaxLength(300).IsRequired();
            b.Property(s => s.Description).HasMaxLength(2000);
            b.Property(s => s.Value).HasMaxLength(4000).IsRequired();
            b.Property(s => s.DefaultValue).HasMaxLength(4000);
            b.Property(s => s.Group).HasMaxLength(128);
            b.Property(s => s.ValueType).HasConversion<int>();
            b.Property(s => s.Scope).HasConversion<int>();
            b.Property(s => s.LastModifiedByUserName).HasMaxLength(256);

            // کلید یکتا به ازای دامنه. چون OrgUnitId قابل‌تهی است، SQL Server
            // مقادیر NULL را متمایز می‌داند و یکتایی برای دامنه‌ی سیستم (که
            // OrgUnitId آن NULL است) اعمال نمی‌شود. به‌جای یک اندیس ساده، از
            // دو اندیس یکتای فیلترشده استفاده می‌کنیم تا یکتایی واقعی تضمین شود:
            // دامنه‌ی سیستم (OrgUnitId IS NULL) روی Key، و دامنه‌ی سازمانی روی
            // (OrgUnitId, Key).
            b.HasIndex(s => new { s.Key })
                .IsUnique()
                .HasFilter($"[{nameof(Setting.OrgUnitId)}] IS NULL AND [{nameof(Setting.Scope)}] = {(int)ConfigurationScope.System}");

            b.HasIndex(s => new { s.OrgUnitId, s.Key })
                .IsUnique()
                .HasFilter($"[{nameof(Setting.OrgUnitId)}] IS NOT NULL AND [{nameof(Setting.Scope)}] = {(int)ConfigurationScope.Organization}");

            // اندیس ساده برای جستجوی سریع بر اساس کلید. نام پایگاه‌داده‌ی مجزا
            // ضروری است: در غیر این صورت EF Core این اندیس را با اندیس یکتای
            // فیلترشده‌ی بالا (که نام پیش‌فرض یکسانی دارد) ادغام می‌کند و اندیس
            // ساده هرگز ساخته نمی‌شود — مسیر داغ کلید به‌صورت table scan اجرا می‌شود.
            b.HasIndex(s => s.Key).HasDatabaseName("IX_system_settings_Key_Lookup");
            b.HasIndex(s => s.Group);
        });

        modelBuilder.Entity<FeatureFlag>(b =>
        {
            b.ConfigureBase("system_feature_flags", provider);

            b.Property(f => f.Key).HasMaxLength(128).IsRequired();
            b.Property(f => f.Name).HasMaxLength(300).IsRequired();
            b.Property(f => f.Description).HasMaxLength(2000);
            b.Property(f => f.State).HasConversion<int>();
            b.Property(f => f.Scope).HasConversion<int>();
            b.Property(f => f.LastModifiedByUserName).HasMaxLength(256);

            // فهرست‌های مجاز به‌صورت JSON فشرده ذخیره می‌شوند تا هم در SQL Server
            // و هم در SQLite به‌درستی کار کنند (نباید به navigateable collection
            // تبدیل شوند). ValueComparer بر اساس محتوا مقایسه می‌کند تا ردیابی
            // تغییر EF Core روی این فهرست‌ها درست کار کند.
            b.Property(f => f.AllowedUserIds)
                .HasConversion(list => ListToJsonConverter(list), json => JsonToListConverter(json), StringListValueComparer)
                .HasMaxLength(4000);

            b.Property(f => f.AllowedRoles)
                .HasConversion(list => ListToJsonConverter(list), json => JsonToListConverter(json), StringListValueComparer)
                .HasMaxLength(4000);

            b.Property(f => f.ExpiresAt).HasColumnType("datetime2");

            // مانند تنظیمات: یکتایی واقعی با اندیس‌های فیلترشده‌ی جداگانه برای
            // دامنه‌ی سیستم و دامنه‌ی سازمانی (OrgUnitId قابل‌تهی است).
            b.HasIndex(f => new { f.Key })
                .IsUnique()
                .HasFilter($"[{nameof(FeatureFlag.OrgUnitId)}] IS NULL AND [{nameof(FeatureFlag.Scope)}] = {(int)ConfigurationScope.System}");

            b.HasIndex(f => new { f.OrgUnitId, f.Key })
                .IsUnique()
                .HasFilter($"[{nameof(FeatureFlag.OrgUnitId)}] IS NOT NULL AND [{nameof(FeatureFlag.Scope)}] = {(int)ConfigurationScope.Organization}");

            // اندیس ساده‌ی جستجو (نام مجزا برای جلوگیری از ادغام با اندیس یکتای فیلترشده).
            b.HasIndex(f => f.Key).HasDatabaseName("IX_system_feature_flags_Key_Lookup");
            b.HasIndex(f => f.State);
        });

        modelBuilder.Entity<SystemPolicy>(b =>
        {
            b.ConfigureBase("system_policies", provider);

            b.Property(p => p.Key).HasMaxLength(256).IsRequired();
            b.Property(p => p.Name).HasMaxLength(300).IsRequired();
            b.Property(p => p.Description).HasMaxLength(2000);
            b.Property(p => p.Value).HasMaxLength(2000).IsRequired();
            b.Property(p => p.DefaultValue).HasMaxLength(2000);
            b.Property(p => p.Type).HasConversion<int>();
            b.Property(p => p.LastModifiedByUserName).HasMaxLength(256);

            b.HasIndex(p => new { p.Type, p.Key }).IsUnique();
            b.HasIndex(p => p.Type);
            b.HasIndex(p => p.IsEnabled);
        });

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>تبدیل فهرست رشته‌ای به JSON برای ذخیره در ستون.</summary>
    private static string ListToJsonConverter(List<string> list) =>
        list.Count == 0 ? "[]" : JsonSerializer.Serialize(list, ListJsonOptions);

    /// <summary>تبدیل JSON ذخیره‌شده به فهرست رشته‌ای.</summary>
    private static List<string> JsonToListConverter(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, ListJsonOptions) ?? [];
        }
        catch (JsonException)
        {
            // داده‌ی تاریخی نامعتبر — فهرست خالی امن‌ترین گزینه است.
            return [];
        }
    }

    /// <summary>
    /// مقایسه‌کننده‌ی مقداری برای فهرست رشته‌ای: بر اساس محتوا مقایسه می‌کند
    /// تا تغییر اعضای فهرست توسط ردیاب تغییر EF Core تشخیص داده شود.
    /// </summary>
    private static ValueComparer<List<string>> StringListValueComparer { get; } = new(
        (a, b) => a != null && b != null && a.SequenceEqual(b),
        c => c.Aggregate(0, (hash, value) => hash ^ value.GetHashCode(StringComparison.Ordinal)),
        c => c.ToList());
}
