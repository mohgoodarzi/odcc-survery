using System.Globalization;
using ODCC.Application.Abstractions;
using ODCC.Domain.Common;

namespace ODCC.Infrastructure.Services;

/// <summary>
/// پیاده‌ی <see cref="ICalendarService"/> با استفاده از <see cref="PersianCalendar"/> خود دات‌نت.
/// الگوریتم تبدیل این کلاس بخشی از کتابخانه‌ی پایه است و قابل اتکا است؛
/// هرگز الگوریتم تبدیل شمسی دست‌ساز پیاده‌سازی نمی‌شود.
///
/// قانون: ورودی همواره UTC است و خروجی فقط برای نمایش (رابط کاربری، PDF، پیامک) است.
/// </summary>
public class CalendarService : ICalendarService
{
    private static readonly PersianCalendar Persian = new();

    public string ToJalaliShortDate(DateTime utcValue, Language language = Language.Fa)
    {
        if (language == Language.En)
            return utcValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var local = ToTehranTime(utcValue);
        return string.Create(CultureInfo.InvariantCulture, $"{Persian.GetYear(local):D4}/{Persian.GetMonth(local):D2}/{Persian.GetDayOfMonth(local):D2}");
    }

    public string ToJalaliLongDate(DateTime utcValue, Language language = Language.Fa)
    {
        if (language == Language.En)
            return utcValue.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var local = ToTehranTime(utcValue);
        return string.Create(CultureInfo.InvariantCulture,
            $"{Persian.GetDayOfMonth(local):D2} {GetPersianMonthName(Persian.GetMonth(local))} {Persian.GetYear(local):D4}");
    }

    public string ToJalaliDateTime(DateTime utcValue, Language language = Language.Fa)
    {
        if (language == Language.En)
            return utcValue.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        var local = ToTehranTime(utcValue);
        return string.Create(CultureInfo.InvariantCulture,
            $"{ToJalaliShortDate(utcValue, language)} {local.Hour:D2}:{local.Minute:D2}");
    }

    /// <summary>
    /// تبدیل رشته‌ی تاریخ شمسی (۱۴۰۴/۰۶/۳۱ یا 1404/06/31) به UTC.
    /// در صورت نامعتبر بودن <c>null</c> برمی‌گرداند.
    /// </summary>
    public DateTime? FromJalaliToUtc(string jalaliDate)
    {
        if (string.IsNullOrWhiteSpace(jalaliDate))
            return null;

        // نرمال‌سازی ارقام فارسی/عربی به انگلیسی برای تجزیه.
        var normalized = jalaliDate.Trim()
            .Replace('۰', '0').Replace('۱', '1').Replace('۲', '2').Replace('۳', '3').Replace('۴', '4')
            .Replace('۵', '5').Replace('۶', '6').Replace('۷', '7').Replace('۸', '8').Replace('۹', '9');

        var parts = normalized.Split('/', '-', '.');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var year)
            || !int.TryParse(parts[1], out var month)
            || !int.TryParse(parts[2], out var day))
            return null;

        if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > 31)
            return null;

        try
        {
            var gregorian = Persian.ToDateTime(year, month, day, 0, 0, 0, 0);
            // تاریخ شمسی بدون زمان به‌صورت شروع روز در منطقه‌ی زمانی تهران تفسیر می‌شود.
            return TimeZoneInfo.ConvertTimeToUtc(gregorian, TehranTimeZone);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static readonly TimeZoneInfo TehranTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time");

    /// <summary>تبدیل UTC به زمان محلی تهران.</summary>
    private static DateTime ToTehranTime(DateTime utcValue) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcValue, DateTimeKind.Utc), TehranTimeZone);

    private static string GetPersianMonthName(int month) => month switch
    {
        1 => "فروردین", 2 => "اردیبهشت", 3 => "خرداد", 4 => "تیر",
        5 => "مرداد", 6 => "شهریور", 7 => "مهر", 8 => "آبان",
        9 => "آذر", 10 => "دی", 11 => "بهمن", 12 => "اسفند",
        _ => string.Empty
    };
}
