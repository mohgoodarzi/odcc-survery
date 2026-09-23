using Microsoft.EntityFrameworkCore;
using ODCC.Domain.Modules.QuestionBank.Entities;
using ODCC.Application.Languages;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Modules.QuestionBank.Persistence;

/// <summary>
/// DbContext اختصاصی ماژول کتابخانه‌ی سؤالات.
///
/// در معماری «مونولیت ماژولار» هر ماژول DbContext خود را دارد که فقط جداول
/// همان ماژول را می‌شناسد و مهاجرت‌های مستقل خود را تولید می‌کند. سایر ماژول‌ها
/// هرگز از این DbContext مستقیماً استفاده نمی‌کنند؛ تنها از طریق قراردادهای
/// لایه‌ی کاربرد (IQuestionService).
/// </summary>
public class QuestionBankDbContext(DbContextOptions<QuestionBankDbContext> options) : DbContext(options)
{
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<QuestionVersion> QuestionVersions => Set<QuestionVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<Question>(b =>
        {
            b.ConfigureBase("questions", provider);
            b.Property(q => q.Code).HasMaxLength(64).IsRequired();
            b.Property(q => q.Type).HasConversion<int>();
            b.Property(q => q.ScaleMax).HasDefaultValue(5);
            b.HasIndex(q => q.Code).IsUnique();
            b.HasIndex(q => q.IsArchived);
            b.HasIndex(q => q.Type);

            b.HasMany(q => q.Localizations)
                .WithOne()
                .HasForeignKey(l => l.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(q => q.Options)
                .WithOne()
                .HasForeignKey(o => o.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(q => q.Tags)
                .WithOne()
                .HasForeignKey(t => t.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasMany(q => q.Versions)
                .WithOne()
                .HasForeignKey(v => v.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuestionLocalization>(b =>
        {
            LocalizationConfiguration.ConfigureLocalization(b, "question_localizations", nameof(QuestionLocalization.QuestionId));
            b.Property(l => l.Text).HasMaxLength(1024).IsRequired();
            b.Property(l => l.Description).HasMaxLength(2048);
        });

        modelBuilder.Entity<QuestionOption>(b =>
        {
            b.ConfigureBase("question_options", provider);
            b.Property(o => o.Code).HasMaxLength(32).IsRequired();
            b.HasIndex(o => new { o.QuestionId, o.Code }).IsUnique();

            b.HasMany(o => o.Localizations)
                .WithOne()
                .HasForeignKey(l => l.QuestionOptionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuestionOptionLocalization>(b =>
        {
            LocalizationConfiguration.ConfigureLocalization(b, "question_option_localizations", nameof(QuestionOptionLocalization.QuestionOptionId));
            b.Property(l => l.Text).HasMaxLength(512).IsRequired();
        });

        modelBuilder.Entity<QuestionTag>(b =>
        {
            b.ConfigureBase("question_tags", provider);
            b.Property(t => t.Name).HasMaxLength(64).IsRequired();
            b.HasIndex(t => new { t.QuestionId, t.Name }).IsUnique();
        });

        modelBuilder.Entity<QuestionVersion>(b =>
        {
            b.ConfigureBase("question_versions", provider);
            b.Property(v => v.VersionNumber).IsRequired();
            b.Property(v => v.ChangeSummary).HasMaxLength(512);
            b.HasIndex(v => v.QuestionId);

            // nvarchar(max) فقط در SQL Server معتبر است؛ SQLite طول نامحدود را
            // با نوع TEXT و بدون تعیین حداکثر طول نمایش می‌دهد.
            if (!string.Equals(provider, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
            {
                b.Property(v => v.Snapshot).HasColumnType("nvarchar(max)").IsRequired();
            }
            else
            {
                b.Property(v => v.Snapshot).IsRequired();
            }
        });

        base.OnModelCreating(modelBuilder);
    }
}
