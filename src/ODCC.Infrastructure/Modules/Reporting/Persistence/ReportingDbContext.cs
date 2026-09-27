using Microsoft.EntityFrameworkCore;
using ODCC.Domain.Modules.Reporting.Entities;
using ODCC.Domain.Modules.Reporting.Enums;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Modules.Reporting.Persistence;

/// <summary>
/// DbContext اختصاصی ماژول گزارش‌گیری.
///
/// در معماری «مونولیت ماژولار» هر ماژول DbContext خود را دارد که فقط جداول
/// همان ماژول را می‌شناسد و مهاجرت‌های مستقل خود را تولید می‌کند. سایر ماژول‌ها
/// (مثل اعلان‌ها یا برنامه‌ی اقدام در فازهای آینده) فقط از طریق قراردادهای
/// لایه‌ی کاربرد (<c>IReportingService</c>) با این ماژول صحبت می‌کنند.
/// </summary>
public class ReportingDbContext(DbContextOptions<ReportingDbContext> options) : DbContext(options)
{
    /// <summary>تعاریف گزارش (محتوا، دامنه، زمان‌بندی).</summary>
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();

    /// <summary>اجراهای گزارش (وضعیت، زمان‌ها، خروجی).</summary>
    public DbSet<ReportExecution> ReportExecutions => Set<ReportExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<ReportDefinition>(b =>
        {
            b.ConfigureBase("report_definitions", provider);
            b.Property(d => d.Name).HasMaxLength(200).IsRequired();
            b.Property(d => d.Description).HasMaxLength(1000);
            b.Property(d => d.Type).HasConversion<int>();
            b.Property(d => d.Format).HasConversion<int>();
            b.Property(d => d.Schedule).HasConversion<int>();
            b.Property(d => d.Status).HasConversion<int>();
            b.Property(d => d.SurveyCode).HasMaxLength(64);
            b.Property(d => d.SurveyTitle).HasMaxLength(256);
            b.Property(d => d.OrgUnitPath).HasMaxLength(512);
            b.Property(d => d.OwnerUserName).HasMaxLength(256);
            b.Property(d => d.From).HasColumnType("datetime2");
            b.Property(d => d.To).HasColumnType("datetime2");
            b.Property(d => d.LastExecutedAt).HasColumnType("datetime2");
            b.Property(d => d.NextRunAt).HasColumnType("datetime2");

            // فهرست برای زمان‌بند: پیدا کردن تعاریف فعالِ رسیده.
            b.HasIndex(d => new { d.Status, d.NextRunAt });
            b.HasIndex(d => d.Status);
            b.HasIndex(d => d.SurveyId);
        });

        modelBuilder.Entity<ReportExecution>(b =>
        {
            b.ConfigureBase("report_executions", provider);
            b.Property(e => e.ReportName).HasMaxLength(200).IsRequired();
            b.Property(e => e.ReportType).HasConversion<int>();
            b.Property(e => e.Format).HasConversion<int>();
            b.Property(e => e.Status).HasConversion<int>();
            b.Property(e => e.QueuedAt).HasColumnType("datetime2");
            b.Property(e => e.StartedAt).HasColumnType("datetime2");
            b.Property(e => e.CompletedAt).HasColumnType("datetime2");
            b.Property(e => e.TriggeredByName).HasMaxLength(256);
            b.Property(e => e.FileName).HasMaxLength(300);
            b.Property(e => e.FilePath).HasMaxLength(512);

            // برای پنجره‌ی نگه‌داری: حذف نرمِ قدیمی‌ترین اجراها.
            b.HasIndex(e => new { e.ReportDefinitionId, e.QueuedAt });
            b.HasIndex(e => e.Status);
        });

        base.OnModelCreating(modelBuilder);
    }
}
