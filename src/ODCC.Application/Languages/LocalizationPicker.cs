using ODCC.Domain.Common;

namespace ODCC.Application.Languages;

/// <summary>
/// انتخاب ترجمه‌ی متناسب با زبان درخواست‌شده.
/// در صورت نبودن ترجمه‌ی دقیق، به هر ترجمه‌ی موجود تنزل می‌کند تا فقدان یک زبان
/// هرگز بخشی از رابط کاربری را خالی نکند.
/// </summary>
public static class LocalizationPicker
{
    public static TLoc? Pick<TLoc>(this IEnumerable<TLoc> localizations, Language language)
        where TLoc : Localization =>
        localizations.FirstOrDefault(l => l.Language == language)
        ?? localizations.FirstOrDefault();
}
