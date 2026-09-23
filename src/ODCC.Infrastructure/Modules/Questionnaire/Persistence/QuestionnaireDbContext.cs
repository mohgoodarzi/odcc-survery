using Microsoft.EntityFrameworkCore;
using ODCC.Domain.Modules.Questionnaire.Entities;
using QuestionnaireEntity = ODCC.Domain.Modules.Questionnaire.Entities.Questionnaire;
using ODCC.Application.Languages;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Modules.Questionnaire.Persistence;

/// <summary>
/// DbContext اختصاصی ماژول پرسشنامه‌ها.
///
/// در معماری «مونولیت ماژولار» هر ماژول DbContext خود را دارد که فقط جداول
/// همان ماژول را می‌شناسد و مهاجرت‌های مستقل خود را تولید می‌کند. سایر ماژول‌ها
/// هرگز از این DbContext مستقیماً استفاده نمی‌کنند؛ تنها از طریق قراردادهای
/// لایه‌ی کاربرد (IQuestionnaireService).
/// </summary>
public class QuestionnaireDbContext(DbContextOptions<QuestionnaireDbContext> options) : DbContext(options)
{
    public DbSet<QuestionnaireEntity> Questionnaires => Set<QuestionnaireEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<QuestionnaireEntity>(b =>
        {
            b.ConfigureBase("questionnaires", provider);
            b.Property(q => q.Code).HasMaxLength(64).IsRequired();
            b.Property(q => q.Status).HasConversion<int>();
            b.HasIndex(q => q.Code).IsUnique();
            b.HasIndex(q => q.Status);

            b.HasMany(q => q.Localizations)
                .WithOne()
                .HasForeignKey(l => l.QuestionnaireId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(q => q.Sections)
                .WithOne()
                .HasForeignKey(s => s.QuestionnaireId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuestionnaireLocalization>(b =>
        {
            LocalizationConfiguration.ConfigureLocalization(b, "questionnaire_localizations", nameof(QuestionnaireLocalization.QuestionnaireId));
            b.Property(l => l.Title).HasMaxLength(256).IsRequired();
            b.Property(l => l.Description).HasMaxLength(2048);
        });

        modelBuilder.Entity<Section>(b =>
        {
            b.ConfigureBase("questionnaire_sections", provider);
            b.Property(s => s.DisplayOrder).IsRequired();

            b.HasMany(s => s.Localizations)
                .WithOne()
                .HasForeignKey(l => l.SectionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(s => s.Items)
                .WithOne()
                .HasForeignKey(i => i.SectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SectionLocalization>(b =>
        {
            LocalizationConfiguration.ConfigureLocalization(b, "questionnaire_section_localizations", nameof(SectionLocalization.SectionId));
            b.Property(l => l.Title).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<QuestionnaireItem>(b =>
        {
            b.ConfigureBase("questionnaire_items", provider);
            b.Property(i => i.QuestionCode).HasMaxLength(64).IsRequired();
            b.Property(i => i.QuestionType).HasConversion<int>();
            b.Property(i => i.QuestionVersionNumber).IsRequired();
            b.Property(i => i.TitleOverride).HasMaxLength(1024);
            b.HasIndex(i => i.QuestionId);
            b.HasIndex(i => new { i.SectionId, i.DisplayOrder });

            b.HasMany(i => i.BranchingRules)
                .WithOne()
                .HasForeignKey(r => r.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BranchingRule>(b =>
        {
            b.ConfigureBase("questionnaire_branching_rules", provider);
            b.Property(r => r.Condition).HasConversion<int>();
            b.Property(r => r.ExpectedValue).HasMaxLength(256).IsRequired();
            b.HasIndex(r => r.TargetItemId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
