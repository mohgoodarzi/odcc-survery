using System.Globalization;
using FluentAssertions;
using ODCC.Domain.Common;
using ODCC.Infrastructure.Services;
using Xunit;

namespace ODCC.Infrastructure.Tests.Services;

/// <summary>
/// آزمون‌های <see cref="CalendarService"/>: تبدیل دوطرفه میان UTC و هجری شمسی.
///
/// مرجع تبدیل <see cref="System.Globalization.PersianCalendar"/> خود دات‌نت است
/// (طبق docs/localization.md الگوریتم دست‌ساز پیاده‌سازی نمی‌شود)؛ اینجا
/// فقط رفتار عمومی قرارداد تثبیت می‌شود.
/// </summary>
public class CalendarServiceTests
{
    private readonly CalendarService _calendar = new();

    [Theory]
    [InlineData("2026-09-30T00:00:00Z", "1405/07/08")]
    [InlineData("2024-03-20T00:00:00Z", "1403/01/01")]
    [InlineData("2025-03-21T00:00:00Z", "1404/01/01")]
    [InlineData("2020-08-21T00:00:00Z", "1399/05/31")]
    public void ToJalaliShortDate_converts_known_dates(string utcIso, string expected)
    {
        var utc = DateTime.Parse(utcIso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);

        _calendar.ToJalaliShortDate(utc).Should().Be(expected);
    }

    [Fact]
    public void ToJalaliLongDate_contains_persian_month_name()
    {
        var utc = new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc);

        _calendar.ToJalaliLongDate(utc).Should().Be("08 مهر 1405");
    }

    [Fact]
    public void ToJalaliDateTime_appends_local_time()
    {
        var utc = new DateTime(2026, 9, 30, 12, 30, 0, DateTimeKind.Utc);

        _calendar.ToJalaliDateTime(utc).Should().Be("1405/07/08 16:00");
    }

    [Fact]
    public void English_culture_falls_back_to_iso_date()
    {
        var utc = new DateTime(2026, 9, 30, 12, 30, 0, DateTimeKind.Utc);

        _calendar.ToJalaliShortDate(utc, Language.En).Should().Be("2026-09-30");
        _calendar.ToJalaliLongDate(utc, Language.En).Should().Be("2026-09-30");
        _calendar.ToJalaliDateTime(utc, Language.En).Should().Be("2026-09-30 12:30");
    }

    [Theory]
    [InlineData("1405/07/08", "2026-09-30")]
    [InlineData("۱۴۰۵/۰۷/۰۸", "2026-09-30")]
    [InlineData("1403-01-01", "2024-03-20")]
    [InlineData("1399.05.31", "2020-08-21")]
    public void FromJalaliToUtc_parses_persian_digits_and_separators(string jalali, string expectedTehranDate)
    {
        var actual = _calendar.FromJalaliToUtc(jalali);

        // تاریخ شمسی بدون زمان، شروعِ همان روز به وقت تهران تفسیر می‌شود.
        actual.Should().NotBeNull();
        actual!.Value.Kind.Should().Be(DateTimeKind.Utc);

        var tehran = TimeZoneInfo.ConvertTimeFromUtc(
            actual.Value, TimeZoneInfo.FindSystemTimeZoneById("Iran Standard Time"));

        tehran.Date.Should().Be(DateTime.Parse(expectedTehranDate, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// رفت و برگشت باید روی «روز تقویمی شمسی» پایدار باشد. چون تاریخ شمسی
    /// به‌صورت شروع روز به وقت تهران تفسیر می‌شود، مقایسه‌ی مستقیم تاریخ
    /// UTC می‌تواند در نزدیکی تغییرات منطقه‌ی زمانی یک روز جابه‌به شود؛
    /// بنابراین خود مقدار شمسی مقایسه می‌شود.
    /// </summary>
    [Fact]
    public void FromJalaliToUtc_roundtrips_through_ToJalaliShortDate()
    {
        var seed = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        for (var offset = 0; offset < 400; offset++)
        {
            var utc = seed.AddDays(offset);
            var jalali = _calendar.ToJalaliShortDate(utc);
            var back = _calendar.FromJalaliToUtc(jalali);

            back.Should().NotBeNull();
            _calendar.ToJalaliShortDate(back!.Value).Should().Be(jalali);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-date")]
    [InlineData("1405/13/01")]
    [InlineData("1405/07/32")]
    [InlineData("1404/12/30")] // اسفند ۱۴۰۴ کبیسه نیست
    public void FromJalaliToUtc_rejects_invalid_input(string jalali)
    {
        _calendar.FromJalaliToUtc(jalali).Should().BeNull();
    }
}
