using NotificationEntity = ODCC.Domain.Modules.Notification.Entities.Notification;
using Microsoft.EntityFrameworkCore;
using ODCC.Domain.Modules.Notification.Entities;
using ODCC.Domain.Modules.Notification.Enums;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Modules.Notification.Persistence;

/// <summary>
/// DbContext ماژول اعلان‌ها.
///
/// جداول این ماژول مستقل از سایر ماژول‌هاست. خواندن کاربران/کارمندان فقط از
/// طریق قراردادهای عمومی ماژول هویت/سازمان انجام می‌شود، نه از DbContext آن‌ها.
/// </summary>
public class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    /// <summary>اعلان‌ها (یک نمونه به ازای هر گیرنده+کانال).</summary>
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    /// <summary>قالب‌های پیام.</summary>
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();

    /// <summary>ترجیحات تحویل کاربران.</summary>
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<NotificationEntity>(b =>
        {
            b.ConfigureBase("notifications", provider);

            b.Property(n => n.RecipientName).HasMaxLength(256);
            b.Property(n => n.RecipientEmail).HasMaxLength(320);
            b.Property(n => n.RecipientPhone).HasMaxLength(32);
            b.Property(n => n.Channel).HasConversion<int>();
            b.Property(n => n.Category).HasConversion<int>();
            b.Property(n => n.Status).HasConversion<int>();
            b.Property(n => n.TemplateCode).HasMaxLength(128).IsRequired();
            b.Property(n => n.Subject).HasMaxLength(500).IsRequired();
            b.Property(n => n.Language).HasConversion<int>();
            b.Property(n => n.SourceType).HasMaxLength(64);
            b.Property(n => n.Url).HasMaxLength(512);
            b.Property(n => n.ProviderMessageId).HasMaxLength(256);

            // nvarchar(max) فقط روی SQL Server معتبر است؛ SQLite به TEXT نامحدود ذخیره می‌کند.
            if (!string.Equals(provider, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
            {
                b.Property(n => n.Body).HasColumnType("nvarchar(max)");
                b.Property(n => n.LastError).HasColumnType("nvarchar(max)");
            }

            // ایندس‌های پرس‌وجوهای پرتکرار:
            b.HasIndex(n => new { n.RecipientUserId, n.Channel });
            b.HasIndex(n => n.Status);
            b.HasIndex(n => new { n.Status, n.NextTryAt }); // زمان‌بند تحویل
            b.HasIndex(n => new { n.SourceType, n.SourceId }); // پیوند به کمپین
        });

        modelBuilder.Entity<NotificationTemplate>(b =>
        {
            b.ConfigureBase("notification_templates", provider);

            b.Property(t => t.Code).HasMaxLength(128).IsRequired();
            b.Property(t => t.Name).HasMaxLength(200).IsRequired();
            b.Property(t => t.Channel).HasConversion<int>();
            b.Property(t => t.Category).HasConversion<int>();

            // کد قالب یکتا است (حتی در میان بایگانی‌شده‌ها، چون کد مبنای فراخوانی است).
            b.HasIndex(t => t.Code).IsUnique();

            b.HasMany(t => t.Localizations)
                .WithOne()
                .HasForeignKey(l => l.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);

            // nvarchar(max) فقط روی SQL Server معتبر است.
            if (!string.Equals(provider, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
            {
                modelBuilder.Entity<NotificationTemplateLocalization>()
                    .Property(l => l.Body)
                    .HasColumnType("nvarchar(max)");
            }
        });

        modelBuilder.Entity<NotificationTemplateLocalization>(b =>
        {
            LocalizationConfiguration.ConfigureLocalization(b, "notification_template_localizations", nameof(NotificationTemplateLocalization.TemplateId));
            b.Property(l => l.Subject).HasMaxLength(500).IsRequired();
        });

        modelBuilder.Entity<NotificationPreference>(b =>
        {
            b.ConfigureBase("notification_preferences", provider);

            b.Property(p => p.Channel).HasConversion<int>();
            b.Property(p => p.Category).HasConversion<int>();

            // یکتایی ترجیب به ازای (کاربر، کانال، دسته). چون دسته می‌تواند
            // <c>null</c> باشد (ترجیب عمومی) و SQL Server مقادیر NULL را در اندیس
            // یکتا مجاز می‌داند، از دو اندیس فیلترشده استفاده می‌شود تا هم
            // ترجیب عمومی و هم ترجیب هر دسته یکتا باشند. این روی SQLite و
            // SQL Server هر دو پشتیبانی می‌شود.
            b.HasIndex(p => new { p.UserId, p.Channel })
                .IsUnique()
                .HasFilter($"[{nameof(NotificationPreference.Category)}] IS NULL");

            b.HasIndex(p => new { p.UserId, p.Channel, p.Category })
                .IsUnique()
                .HasFilter($"[{nameof(NotificationPreference.Category)}] IS NOT NULL");

            b.HasIndex(p => p.UserId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
