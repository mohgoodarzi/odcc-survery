using Microsoft.EntityFrameworkCore;
using ODCC.Domain.Modules.Response.Entities;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Modules.Response.Persistence;

/// <summary>
/// DbContext اختصاصی ماژول پاسخ‌ها.
///
/// در معماری «مونولیت ماژولار» هر ماژول DbContext خود را دارد که فقط جداول
/// همان ماژول را می‌شناسد و مهاجرت‌های مستقل خود را تولید می‌کند. سایر ماژول‌ها
/// هرگز از این DbContext مستقیماً استفاده نمی‌کنند؛ تنها از طریق قراردادهای
/// لایه‌ی کاربرد (IResponseService).
/// </summary>
public class ResponseDbContext(DbContextOptions<ResponseDbContext> options) : DbContext(options)
{
    public DbSet<ResponseSession> Sessions => Set<ResponseSession>();
    public DbSet<ResponseAnswer> Answers => Set<ResponseAnswer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var provider = Database.ProviderName;

        modelBuilder.Entity<ResponseSession>(b =>
        {
            b.ConfigureBase("response_sessions", provider);
            b.Property(s => s.SurveyCode).HasMaxLength(64).IsRequired();
            b.Property(s => s.CampaignCode).HasMaxLength(64);
            b.Property(s => s.RespondentDisplayName).HasMaxLength(256);
            b.Property(s => s.Status).HasConversion<int>();
            b.Property(s => s.Source).HasConversion<int>();
            b.Property(s => s.ResponseLanguage).HasConversion<int>();
            b.Property(s => s.StartedAt).HasColumnType("datetime2");
            b.Property(s => s.SubmittedAt).HasColumnType("datetime2");
            b.Property(s => s.LastActivityAt).HasColumnType("datetime2");

            // کوئری‌های پرتکرار: نشست‌های یک کاربر و نشست‌های یک نظرسنجی.
            b.HasIndex(s => new { s.SurveyId, s.RespondentUserId, s.Status });
            b.HasIndex(s => s.SurveyId);
            b.HasIndex(s => s.Status);

            // پاسخ‌ها به‌صورت آبشاری حذف می‌شوند (خانواده‌ی نشست).
            b.HasMany(s => s.Answers)
                .WithOne()
                .HasForeignKey(a => a.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResponseAnswer>(b =>
        {
            b.ConfigureBase("response_answers", provider);
            b.Property(a => a.QuestionCode).HasMaxLength(64).IsRequired();
            b.Property(a => a.QuestionType).HasConversion<int>();
            b.Property(a => a.TextValue).HasMaxLength(4000);
            b.Property(a => a.NumericValue).HasColumnType("decimal(18,3)");

            // هر آیتم در هر نشست فقط یک پاسخ می‌تواند داشته باشد.
            b.HasIndex(a => new { a.SessionId, a.QuestionnaireItemId }).IsUnique();

            b.HasMany(a => a.Selections)
                .WithOne()
                .HasForeignKey(s => s.AnswerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ResponseAnswerSelection>(b =>
        {
            b.ConfigureBase("response_answer_selections", provider);
            b.Property(s => s.OptionCode).HasMaxLength(64).IsRequired();
            b.HasIndex(s => s.OptionId);
        });

        base.OnModelCreating(modelBuilder);
    }
}
