using ODCC.Domain.Common;

namespace ODCC.Application.Abstractions;

/// <summary>
/// قرارداد تبدیل تاریخ میان میلادی/UTC و هجری شمسی (جلالی).
///
/// قانون معماری: تاریخ‌ها همواره به‌صورت میلادی/UTC در پایگاه داده
/// (ستون‌های <c>datetime2</c>/<c>date</c>) ذخیره می‌شوند. تبدیل به شمسی
/// فقط در لایه‌ی ارائه (رابط کاربری، گزارش‌های PDF، پیامک‌ها) انجام می‌شود
/// و هرگز تاریخ شمسی به‌صورت رشته در پایگاه داده نمی‌نشیند.
/// </summary>
public interface ICalendarService
{
    /// <summary>تاریخ کوتاه شمسی، مثلاً «۱۴۰۴/۰۶/۳۱».</summary>
    string ToJalaliShortDate(DateTime utcValue, Language language = Language.Fa);

    /// <summary>تاریخ بلند شمسی، مثلاً «۳۱ شهریور ۱۴۰۴».</summary>
    string ToJalaliLongDate(DateTime utcValue, Language language = Language.Fa);

    /// <summary>تاریخ و زمان شمسی.</summary>
    string ToJalaliDateTime(DateTime utcValue, Language language = Language.Fa);

    /// <summary>
    /// تبدیل یک رشته‌ی تاریخ شمسی (مثل ۱۴۰۴/۰۶/۳۱) به UTC.
    /// در صورت نامعتبر بودن <c>null</c> برمی‌گرداند.
    /// </summary>
    DateTime? FromJalaliToUtc(string jalaliDate);
}
