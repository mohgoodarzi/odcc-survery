using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ODCC.Domain.Modules.Integration.Entities;
using ODCC.Domain.Modules.Integration.Enums;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Modules.Integration.Persistence;

/// <summary>
/// DbContext ماژول یکپارچه‌سازی.
///
/// جداول این ماژول مستقل از سایر ماژول‌هاست. این ماژول فقط متادیتای عمومی
/// رویدادها را نگه می‌دارد و هرگز به DbContext ماژول دیگر دسترسی مستقیم
/// ندارد — ارتباط فقط از طریق رویدادهای دامنه است.
/// </summary>
public class IntegrationDbContext(DbContextOptions<IntegrationDbContext> options) : DbContext(options)
{
    /// <summary>اندپوینت‌های یکپارچه‌سازی.</summary>
    public DbSet<IntegrationEndpoint> IntegrationEndpoints => Set<IntegrationEndpoint>();

    /// <summary>تحویل‌های وب‌هوک.</summary>
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<IntegrationEndpoint>(b =>
        {
            b.ConfigureBase("integration_endpoints", provider);

            b.Property(e => e.Name).HasMaxLength(300).IsRequired();
            b.Property(e => e.Code).HasMaxLength(128).IsRequired();
            b.Property(e => e.Description).HasMaxLength(2000);
            b.Property(e => e.Url).HasMaxLength(2048).IsRequired();
            b.Property(e => e.HttpMethod).HasMaxLength(16).IsRequired();
            b.Property(e => e.Type).HasConversion<int>();
            b.Property(e => e.AuthType).HasConversion<int>();

            // نام منطقی راز — مقدار واقعی هرگز اینجا نیست.
            b.Property(e => e.SecretRef).HasMaxLength(128);
            b.Property(e => e.AuthHeaderName).HasMaxLength(128);
            b.Property(e => e.CreatedByUserName).HasMaxLength(256);
            b.Property(e => e.LastDeliveryError).HasMaxLength(1000);

            // رویدادهای مشترک‌شده به‌صورت JSON در یک ستون متنی نگه‌داری می‌شوند.
            // ValueComparer مشخص می‌کند که مقایسه‌ی دو فهرست بر اساس محتواست
            // (نه ارجاع) تا ردیابی تغییرهای EF Core روی این مجموعه درست کار کند.
            b.Property(e => e.SubscribedEvents)
                .HasConversion(
                    v => string.Join(';', v),
                    v => string.IsNullOrWhiteSpace(v)
                        ? new List<string>()
                        : v.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList(),
                    ListValueComparer)
                .HasMaxLength(2000);

            // کد یکتا: فقط اندپوینت‌های غیر بایگانی‌شده.
            b.HasIndex(e => e.Code)
                .IsUnique()
                .HasFilter($"[{nameof(IntegrationEndpoint.IsDeleted)}] = 0");

            b.HasIndex(e => new { e.Type, e.IsActive });
            b.HasIndex(e => e.IsActive);
        });

        modelBuilder.Entity<WebhookDelivery>(b =>
        {
            b.ConfigureBase("webhook_deliveries", provider);

            b.Property(d => d.EndpointCode).HasMaxLength(128).IsRequired();
            b.Property(d => d.EventType).HasMaxLength(128).IsRequired();
            b.Property(d => d.EventId).HasMaxLength(128).IsRequired();
            b.Property(d => d.Status).HasConversion<int>();
            b.Property(d => d.LastError).HasMaxLength(1000);

            // payload: فقط متادیتای عمومی، حداکثر ۱ مگابایت.
            b.Property(d => d.PayloadJson).HasMaxLength(1024 * 1024).IsRequired();

            b.Property(d => d.LastAttemptAt).HasColumnType("datetime2");
            b.Property(d => d.NextAttemptAt).HasColumnType("datetime2");
            b.Property(d => d.DeliveredAt).HasColumnType("datetime2");

            // یکتایی رویداد به ازای هر اندپوینت (idempotency).
            b.HasIndex(d => new { d.EndpointId, d.EventId }).IsUnique();
            b.HasIndex(d => d.EndpointId);
            b.HasIndex(d => d.Status);
            b.HasIndex(d => new { d.Status, d.NextAttemptAt }); // زمان‌بند تلاش مجدد
            b.HasIndex(d => d.CreatedAt);
        });

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// مقایسه‌کننده‌ی مقداری برای فهرست رشته‌ای: بر اساس محتوا مقایسه می‌کند
    /// تا تغییر اعضای فهرست توسط ردیاب تغییر EF Core تشخیص داده شود.
    /// </summary>
    private static ValueComparer<List<string>> ListValueComparer { get; } = new(
        (a, b) => a != null && b != null && a.SequenceEqual(b),
        c => c.Aggregate(0, (hash, value) => hash ^ value.GetHashCode(StringComparison.Ordinal)),
        c => c.ToList());
}
