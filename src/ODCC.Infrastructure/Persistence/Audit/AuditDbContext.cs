using Microsoft.EntityFrameworkCore;
using ODCC.Domain.Modules.Audit.Entities;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Persistence.Audit;

/// <summary>
/// DbContext اختصاصی ماژول ممیزی.
///
/// در معماری «مونولیت ماژولار» هر ماژول DbContext خود را دارد که فقط جداول
/// همان ماژول را می‌شناسد و مهاجرت‌های مستقل خود را تولید می‌کند. تمام ماژول‌ها
/// یک رشته‌ی اتصال و یک پایگاه داده‌ی فیزیکی را به اشتراک می‌گذارند، اما
/// مرز طرح (Schema ownership) بین آن‌ها حفظ می‌شود. سایر ماژول‌ها هرگز از این
/// DbContext مستقیماً استفاده نمی‌کنند؛ تنها از طریق قرارداد IAuditService.
/// </summary>
public class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<AuditEntry>(b =>
        {
            b.ConfigureBase("audit_entries", provider);
            b.Property(e => e.UserName).HasMaxLength(256);
            b.Property(e => e.Action).HasMaxLength(64).IsRequired();
            b.Property(e => e.EntityType).HasMaxLength(128).IsRequired();
            b.Property(e => e.Severity).HasMaxLength(16);
            b.Property(e => e.ClientIp).HasMaxLength(64);
            b.Property(e => e.Description).HasMaxLength(1024);

            // nvarchar(max) فقط در SQL Server معتبر است؛ SQLite طول نامحدود را
            // با نوع TEXT و بدون تعیین حداکثر طول نمایش می‌دهد.
            if (!string.Equals(provider, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
            {
                b.Property(e => e.Changes).HasColumnType("nvarchar(max)");
            }

            b.HasIndex(e => e.EntityType);
            b.HasIndex(e => e.UserId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
