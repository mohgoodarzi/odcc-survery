using Microsoft.EntityFrameworkCore;
using ODCC.Domain.Modules.ActionManagement.Entities;
using ODCC.Domain.Modules.ActionManagement.Enums;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Modules.ActionManagement.Persistence;

/// <summary>
/// DbContext ماژول مدیریت اقدامات.
///
/// جداول این ماژول مستقل از سایر ماژول‌هاست. خواندن نظرسنجی‌ها/کاربران/واحدهای
/// سازمانی فقط از طریق قراردادهای عمومی آن ماژول‌ها انجام می‌شود، نه از
/// DbContext آن‌ها.
/// </summary>
public class ActionManagementDbContext(DbContextOptions<ActionManagementDbContext> options) : DbContext(options)
{
    /// <summary>برنامه‌های اقدام.</summary>
    public DbSet<ActionPlan> ActionPlans => Set<ActionPlan>();

    /// <summary>آیتم‌های اقدام.</summary>
    public DbSet<ActionItem> ActionItems => Set<ActionItem>();

    /// <summary>دیدگاه‌های آیتم‌ها.</summary>
    public DbSet<ActionComment> ActionComments => Set<ActionComment>();

    /// <summary>متادیتای پیوست‌های آیتم‌ها.</summary>
    public DbSet<ActionEvidence> ActionEvidence => Set<ActionEvidence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<ActionPlan>(b =>
        {
            b.ConfigureBase("action_plans", provider);

            b.Property(p => p.Title).HasMaxLength(300).IsRequired();
            b.Property(p => p.Description).HasMaxLength(2000);
            b.Property(p => p.Source).HasConversion<int>();
            b.Property(p => p.Status).HasConversion<int>();
            b.Property(p => p.Priority).HasConversion<int>();
            b.Property(p => p.TriggerMetricType).HasConversion<int>();

            // کلید یکتای منبع خودکار: تضمین می‌کند محاسبه‌ی مجدد تحلیلات
            // برنامه‌ی مضاعف نسازد. فیلترشده تا چندین کلید null مجاز باشند
            // (برنامه‌های دستی کلید منبع ندارند).
            b.Property(p => p.SourceKey).HasMaxLength(256);
            b.HasIndex(p => p.SourceKey)
                .IsUnique()
                .HasFilter($"[{nameof(ActionPlan.SourceKey)}] IS NOT NULL");

            b.Property(p => p.SurveyCode).HasMaxLength(128);
            b.Property(p => p.SurveyTitle).HasMaxLength(500);
            b.Property(p => p.OrgUnitPath).HasMaxLength(512);
            b.Property(p => p.OwnerUserName).HasMaxLength(256);
            b.Property(p => p.CreatedByUserName).HasMaxLength(256);

            b.Property(p => p.DueDate).HasColumnType("datetime2");
            b.Property(p => p.CompletedAt).HasColumnType("datetime2");
            b.Property(p => p.OutcomeMeasuredAt).HasColumnType("datetime2");
            b.Property(p => p.TriggerMetricValue).HasColumnType("decimal(18,4)");
            b.Property(p => p.OutcomeMetricValue).HasColumnType("decimal(18,4)");

            // ایندس‌های پرس‌وجوهای پرتکرار:
            b.HasIndex(p => p.Status);
            b.HasIndex(p => new { p.SurveyId, p.Status });
            b.HasIndex(p => new { p.OrgUnitId, p.Status });
            b.HasIndex(p => p.CreatedAt);

            b.HasMany(p => p.Items)
                .WithOne(i => i.ActionPlan!)
                .HasForeignKey(i => i.ActionPlanId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActionItem>(b =>
        {
            b.ConfigureBase("action_items", provider);

            b.Property(i => i.Title).HasMaxLength(300).IsRequired();
            b.Property(i => i.Description).HasMaxLength(2000);
            b.Property(i => i.Status).HasConversion<int>();
            b.Property(i => i.Priority).HasConversion<int>();
            b.Property(i => i.EscalationLevel).HasConversion<int>();
            b.Property(i => i.Effectiveness).HasConversion<int>();
            b.Property(i => i.EffectivenessNote).HasMaxLength(1000);
            b.Property(i => i.AssigneeUserName).HasMaxLength(256);

            b.Property(i => i.DueDate).HasColumnType("datetime2");
            b.Property(i => i.RemindAt).HasColumnType("datetime2");
            b.Property(i => i.EscalatedAt).HasColumnType("datetime2");
            b.Property(i => i.StartedAt).HasColumnType("datetime2");
            b.Property(i => i.CompletedAt).HasColumnType("datetime2");
            b.Property(i => i.EffectivenessAssessedAt).HasColumnType("datetime2");

            // ایندس‌های پیگیری و داشبورد:
            b.HasIndex(i => i.ActionPlanId);
            b.HasIndex(i => new { i.AssigneeUserId, i.Status });
            b.HasIndex(i => new { i.Status, i.RemindAt }); // زمان‌بند یادآور
            b.HasIndex(i => new { i.Status, i.DueDate });  // تشدید سررسیده‌شده‌ها

            b.HasMany(i => i.Comments)
                .WithOne(c => c.ActionItem!)
                .HasForeignKey(c => c.ActionItemId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(i => i.Evidence)
                .WithOne(e => e.ActionItem!)
                .HasForeignKey(e => e.ActionItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ActionComment>(b =>
        {
            b.ConfigureBase("action_comments", provider);

            b.Property(c => c.Body).HasMaxLength(1000).IsRequired();
            b.Property(c => c.AuthorUserName).HasMaxLength(256);

            b.HasIndex(c => new { c.ActionItemId, c.CreatedAt });
        });

        modelBuilder.Entity<ActionEvidence>(b =>
        {
            b.ConfigureBase("action_evidence", provider);

            b.Property(e => e.FileName).HasMaxLength(256).IsRequired();
            b.Property(e => e.ContentType).HasMaxLength(128).IsRequired();
            b.Property(e => e.StoragePath).HasMaxLength(512).IsRequired();
            b.Property(e => e.UploadedByUserName).HasMaxLength(256);

            b.HasIndex(e => e.ActionItemId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
