using Microsoft.EntityFrameworkCore;
using ODCC.Domain.Modules.Analytics.Entities;
using ODCC.Domain.Modules.Response.Enums;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Modules.Analytics.Persistence;

/// <summary>
/// DbContext اختصاصی ماژول تحلیلات.
///
/// در معماری «مونولیت ماژولار» هر ماژول DbContext خود را دارد که فقط جداول
/// همان ماژول را می‌شناسد و مهاجرت‌های مستقل خود را تولید می‌کند. این جداول
/// یک <b>مدل خواندنی</b> هستند که توسط سرویس تحلیلات پر می‌شوند و سایر ماژول‌ها
/// (گزارش‌گیری، اعلان‌ها، برنامه‌ی اقدام در فازهای آینده) فقط از طریق قراردادهای
/// لایه‌ی کاربرد (<c>IAnalyticsService</c>) آن را می‌خوانند.
/// </summary>
public class AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : DbContext(options)
{
    /// <summary>عکس‌العمل‌های محاسبه‌شده‌ی تحلیلات (به ازای نظرسنجی × بُعد بخش‌بندی).</summary>
    public DbSet<SurveyMetric> SurveyMetrics => Set<SurveyMetric>();

    /// <summary>بنچمارک‌های مقایسه.</summary>
    public DbSet<Benchmark> Benchmarks => Set<Benchmark>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<SurveyMetric>(b =>
        {
            b.ConfigureBase("survey_metrics", provider);
            b.Property(m => m.SurveyCode).HasMaxLength(64).IsRequired();
            b.Property(m => m.SurveyTitle).HasMaxLength(256).IsRequired();
            b.Property(m => m.SegmentType).HasConversion<int>();
            b.Property(m => m.Source).HasConversion<int>();
            b.Property(m => m.OrgUnitPath).HasMaxLength(512);
            b.Property(m => m.CampaignCode).HasMaxLength(64);
            b.Property(m => m.WindowStart).HasColumnType("datetime2");
            b.Property(m => m.WindowEnd).HasColumnType("datetime2");
            b.Property(m => m.ComputedAt).HasColumnType("datetime2");
            b.Property(m => m.CompletionRate).HasColumnType("decimal(18,3)");
            b.Property(m => m.NpsScore).HasColumnType("decimal(18,3)");
            b.Property(m => m.CsatScore).HasColumnType("decimal(18,3)");
            b.Property(m => m.CesScore).HasColumnType("decimal(18,3)");
            b.Property(m => m.AverageRating).HasColumnType("decimal(18,3)");
            b.Property(m => m.ResponseRate).HasColumnType("decimal(18,3)");

            // nvarchar(max) فقط در SQL Server معتبر است؛ در SQLite از TEXT استفاده می‌شود.
            if (!string.Equals(provider, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
                b.Property(m => m.QuestionMetrics).HasColumnType("nvarchar(max)");

            // هر نظرسنجی در هر بُعد فقط یک عکس‌العمل دارد (جایگزینی در هر محاسبه).
            b.HasIndex(m => new { m.SurveyId, m.SegmentType, m.OrgUnitId, m.CampaignId, m.Source })
                .IsUnique();
            b.HasIndex(m => m.SurveyId);
            b.HasIndex(m => m.SegmentType);
        });

        modelBuilder.Entity<Benchmark>(b =>
        {
            b.ConfigureBase("benchmarks", provider);
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.Metric).HasConversion<int>();
            b.Property(x => x.TargetValue).HasColumnType("decimal(18,3)");
            b.Property(x => x.OrgUnitPath).HasMaxLength(512);
            b.Property(x => x.Description).HasMaxLength(1000);

            b.HasIndex(x => new { x.Metric, x.IsCompanyWide, x.OrgUnitId });
            b.HasIndex(x => x.IsActive);
        });

        base.OnModelCreating(modelBuilder);
    }
}
