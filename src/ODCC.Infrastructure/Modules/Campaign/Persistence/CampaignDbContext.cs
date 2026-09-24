using Microsoft.EntityFrameworkCore;
using ODCC.Application.Languages;
using ODCC.Application.Modules.Campaign.Dtos;
using ODCC.Domain.Modules.Campaign.Entities;
using ODCC.Infrastructure.Persistence.Common;
using CampaignEntity = ODCC.Domain.Modules.Campaign.Entities.Campaign;

namespace ODCC.Infrastructure.Modules.Campaign.Persistence;

/// <summary>
/// DbContext اختصاصی ماژول کمپین‌ها.
///
/// در معماری «مونولیت ماژولار» هر ماژول DbContext خود را دارد که فقط جداول
/// همان ماژول را می‌شناسد و مهاجرت‌های مستقل خود را تولید می‌کند. سایر ماژول‌ها
/// هرگز از این DbContext مستقیماً استفاده نمی‌کنند؛ تنها از طریق قراردادهای
/// لایه‌ی کاربرد (ICampaignService).
/// </summary>
public class CampaignDbContext(DbContextOptions<CampaignDbContext> options) : DbContext(options)
{
    public DbSet<CampaignEntity> Campaigns => Set<CampaignEntity>();
    public DbSet<Distribution> Distributions => Set<Distribution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<CampaignEntity>(b =>
        {
            b.ConfigureBase("campaigns", provider);
            b.Property(c => c.Code).HasMaxLength(64).IsRequired();
            b.Property(c => c.Status).HasConversion<int>();
            b.Property(c => c.SurveyCode).HasMaxLength(64).IsRequired();
            b.Property(c => c.AudienceType).HasConversion<int>();
            b.Property(c => c.Channel).HasConversion<int>();
            b.Property(c => c.ScheduledAt).HasColumnType("datetime2");
            b.Property(c => c.EndsAt).HasColumnType("datetime2");
            b.Property(c => c.StartedAt).HasColumnType("datetime2");
            b.Property(c => c.CompletedAt).HasColumnType("datetime2");
            b.Property(c => c.ArchivedAt).HasColumnType("datetime2");

            b.HasIndex(c => c.Code).IsUnique();
            b.HasIndex(c => c.Status);
            b.HasIndex(c => c.SurveyId);

            b.HasMany(c => c.Localizations)
                .WithOne()
                .HasForeignKey(l => l.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(c => c.TargetUnits)
                .WithOne()
                .HasForeignKey(t => t.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(c => c.TargetMembers)
                .WithOne()
                .HasForeignKey(m => m.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(c => c.Reminders)
                .WithOne()
                .HasForeignKey(r => r.CampaignId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CampaignLocalization>(b =>
        {
            LocalizationConfiguration.ConfigureLocalization(b, "campaign_localizations", nameof(CampaignLocalization.CampaignId));
            b.Property(l => l.Title).HasMaxLength(256).IsRequired();
            b.Property(l => l.Description).HasMaxLength(2048);
        });

        modelBuilder.Entity<CampaignTargetUnit>(b =>
        {
            b.ConfigureBase("campaign_target_units", provider);
            b.HasIndex(t => new { t.CampaignId, t.OrgUnitId }).IsUnique();
        });

        modelBuilder.Entity<CampaignTargetMember>(b =>
        {
            b.ConfigureBase("campaign_target_members", provider);
            b.HasIndex(m => new { m.CampaignId, m.EmployeeId }).IsUnique();
        });

        modelBuilder.Entity<Reminder>(b =>
        {
            b.ConfigureBase("campaign_reminders", provider);
            b.Property(r => r.Status).HasConversion<int>();
            b.Property(r => r.SendAt).HasColumnType("datetime2");
            b.Property(r => r.SentAt).HasColumnType("datetime2");
            b.HasIndex(r => r.CampaignId);

            b.HasMany(r => r.Localizations)
                .WithOne()
                .HasForeignKey(l => l.ReminderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReminderLocalization>(b =>
        {
            LocalizationConfiguration.ConfigureLocalization(b, "campaign_reminder_localizations", nameof(ReminderLocalization.ReminderId));
            b.Property(l => l.Subject).HasMaxLength(256).IsRequired();
            b.Property(l => l.Body).HasMaxLength(2048);
        });

        // ردیف‌های توزیع موجودیتی مستقل از تجمع کمپین هستند (ممکن است هزاران ردیف باشد)
        // و فقط با کلید خارجی به کمپین ارجاع می‌دهند؛ ناوبری به سمت کمپین وجود ندارد.
        modelBuilder.Entity<Distribution>(b =>
        {
            b.ConfigureBase("campaign_distributions", provider);
            b.Property(d => d.Status).HasConversion<int>();
            b.Property(d => d.SentAt).HasColumnType("datetime2");
            b.Property(d => d.RespondedAt).HasColumnType("datetime2");
            b.Property(d => d.WorkEmail).HasMaxLength(256);
            b.Property(d => d.FailureReason).HasMaxLength(512);
            b.HasIndex(d => d.CampaignId);
            b.HasIndex(d => new { d.CampaignId, d.Status });
            b.HasIndex(d => new { d.CampaignId, d.EmployeeId }).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}
