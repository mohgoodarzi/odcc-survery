using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ODCC.Domain.Common;

namespace ODCC.Infrastructure.Persistence.Common;

/// <summary>
/// شکل مشترک تمام جدول‌های ترجمه: نام snake_case، اندیس یکتا روی (parent, language)
/// تا هر زبان برای هر والد فقط یک ردیف داشته باشد، و حذف آبشاری.
/// </summary>
public static class LocalizationConfiguration
{
    public static EntityTypeBuilder<TLocalization> ConfigureLocalization<TLocalization>(
        EntityTypeBuilder<TLocalization> builder,
        string tableName,
        string foreignKeyColumn)
        where TLocalization : Localization
    {
        builder.ToTable(tableName);
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Language).HasConversion<int>();

        // اندیس یکتا (parent, language). از overload رشته‌ای استفاده می‌شود چون
        // نام ستون کلید خارجی در هر نوع ترجمه متفاوت است و عبارت EF.Property داخل
        // یک نوع ناشناس برای سازنده‌ی مدل، یک عبارت اندیس معتبر نیست.
        builder.HasIndex(new[] { foreignKeyColumn, nameof(Localization.Language) }).IsUnique();

        return builder;
    }
}
