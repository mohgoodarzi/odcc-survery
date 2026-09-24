using Microsoft.EntityFrameworkCore;
using ODCC.Application.Languages;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Modules.Survey.Entities;
using ODCC.Infrastructure.Persistence.Common;
using SurveyEntity = ODCC.Domain.Modules.Survey.Entities.Survey;

namespace ODCC.Infrastructure.Modules.Survey.Persistence;

/// <summary>
/// DbContext اختصاصی ماژول نظرسنجی‌ها.
///
/// در معماری «مونولیت ماژولار» هر ماژول DbContext خود را دارد که فقط جداول
/// همان ماژول را می‌شناسد و مهاجرت‌های مستقل خود را تولید می‌کند. سایر ماژول‌ها
/// هرگز از این DbContext مستقیماً استفاده نمی‌کنند؛ تنها از طریق قراردادهای
/// لایه‌ی کاربرد (ISurveyService).
/// </summary>
public class SurveyDbContext(DbContextOptions<SurveyDbContext> options) : DbContext(options)
{
    public DbSet<SurveyEntity> Surveys => Set<SurveyEntity>();
    public DbSet<SurveyTemplate> Templates => Set<SurveyTemplate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<SurveyEntity>(b =>
        {
            b.ConfigureBase("surveys", provider);
            b.Property(s => s.Code).HasMaxLength(64).IsRequired();
            b.Property(s => s.Status).HasConversion<int>();
            b.Property(s => s.QuestionnaireCode).HasMaxLength(64).IsRequired();
            b.Property(s => s.QuestionnaireVersion).IsRequired();
            b.Property(s => s.StartDate).HasColumnType("datetime2");
            b.Property(s => s.EndDate).HasColumnType("datetime2");
            b.Property(s => s.PublishedAt).HasColumnType("datetime2");
            b.Property(s => s.ActivatedAt).HasColumnType("datetime2");
            b.Property(s => s.ClosedAt).HasColumnType("datetime2");
            b.Property(s => s.ArchivedAt).HasColumnType("datetime2");

            b.HasIndex(s => s.Code).IsUnique();
            b.HasIndex(s => s.Status);
            b.HasIndex(s => s.QuestionnaireId);

            b.HasMany(s => s.Localizations)
                .WithOne()
                .HasForeignKey(l => l.SurveyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SurveyLocalization>(b =>
        {
            LocalizationConfiguration.ConfigureLocalization(b, "survey_localizations", nameof(SurveyLocalization.SurveyId));
            b.Property(l => l.Title).HasMaxLength(256).IsRequired();
            b.Property(l => l.Description).HasMaxLength(2048);
            b.Property(l => l.WelcomeMessage).HasMaxLength(2048);
            b.Property(l => l.ThankYouMessage).HasMaxLength(2048);
        });

        modelBuilder.Entity<SurveyTemplate>(b =>
        {
            b.ConfigureBase("survey_templates", provider);
            b.Property(t => t.Code).HasMaxLength(64).IsRequired();
            b.Property(t => t.Status).HasConversion<int>();
            b.Property(t => t.QuestionnaireCode).HasMaxLength(64).IsRequired();

            b.HasIndex(t => t.Code).IsUnique();
            b.HasIndex(t => t.QuestionnaireId);

            b.HasMany(t => t.Localizations)
                .WithOne()
                .HasForeignKey(l => l.TemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SurveyTemplateLocalization>(b =>
        {
            LocalizationConfiguration.ConfigureLocalization(b, "survey_template_localizations", nameof(SurveyTemplateLocalization.TemplateId));
            b.Property(l => l.Title).HasMaxLength(256).IsRequired();
            b.Property(l => l.Description).HasMaxLength(2048);
        });

        base.OnModelCreating(modelBuilder);
    }
}
