using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using ODCC.Domain.Modules.Organization.Enums;

namespace ODCC.Infrastructure.Modules.Organization.ExcelImport;

/// <summary>
/// یک ردیف خام استخراج‌شده از فایل اکسل قبل از اعتبارسنجی.
/// </summary>
public sealed record EmployeeImportRow
{
    public int RowNumber { get; init; }
    public string? EmployeeCode { get; init; }
    public string? NationalCode { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? FatherName { get; init; }
    public string? OrgUnitCode { get; init; }
    public string? PositionCode { get; init; }
    public string? StatusText { get; init; }
    public string? StartDateText { get; init; }
    public string? EndDateText { get; init; }
    public string? WorkEmail { get; init; }
    public string? InternalPhone { get; init; }
    public string? ManagerCode { get; init; }
}

/// <summary>
/// خطای سطح فایل (فاقد ستون، خالی، بیش از حد بزرگ) که ورود را متوقف می‌کند.
/// </summary>
public sealed class EmployeeImportException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// خواندن فایل اکسل (xlsx) و نگاشت ستون‌ها به فیلدهای کارمند.
///
/// عناوین ستون‌ها به‌صورت هوشمند تطبیق داده می‌شوند: نام ستون نرمال‌سازی شده
/// (حروف کوچک، بدون فاصله/خط زیر/نیم‌فاصله) با مجموعه‌ای از نام‌های فارسی و
/// انگلیسی مقایسه می‌شود. ستون‌های ناشناخته نادیده گرفته می‌شوند.
/// </summary>
public static partial class EmployeeExcelParser
{
    private const char Zwnj = '\u200c';
    private const int MaxRows = 1000;

    /// <summary>
    /// تجزیه‌ی فایل اکسل به ردیف‌های خام کارمند.
    /// </summary>
    /// <exception cref="EmployeeImportException">در صورت نامعتبر بودن ساختار فایل.</exception>
    public static List<EmployeeImportRow> Parse(Stream stream, int? maxRows = null)
    {
        var limit = maxRows ?? MaxRows;

        using var workbook = XlsxReader.Open(stream, limit);

        List<XlsxRow> sheetRows;
        try
        {
            sheetRows = workbook.ReadRows(limit + 1);
        }
        catch (XmlException)
        {
            throw new EmployeeImportException(
                "import_parse_failed", "فایل قابل خواندن نیست. مطمئن شوید یک فایل اکسل معتبر (.xlsx) است.");
        }

        if (sheetRows.Count == 0)
        {
            throw new EmployeeImportException("import_empty_workbook", "فایل اکسل هیچ ردیف داده‌ای ندارد.");
        }

        var map = ReadColumnMap(sheetRows[0]);

        var missing = RequiredColumns.Where(column => !map.ContainsKey(column)).ToList();
        if (missing.Count > 0)
        {
            var names = string.Join("، ", missing.Select(ColumnNameToPersian));
            throw new EmployeeImportException(
                "import_missing_columns",
                $"ستون‌های ضروری در فایل پیدا نشدند: {names}");
        }

        var rows = new List<EmployeeImportRow>();

        // نخستین ردیف، ردیف عناوین است.
        foreach (var sheetRow in sheetRows.Skip(1))
        {
            var values = map.ToDictionary(pair => pair.Key, pair => sheetRow.Cells.GetValueOrDefault(pair.Value));

            // ردیف‌های کاملاً خالی را نادیده می‌گیریم (مثلاً ردیف‌های تهی انتهای فایل).
            if (values.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(new EmployeeImportRow
            {
                RowNumber = sheetRow.RowNumber,
                EmployeeCode = values[nameof(EmployeeImportRow.EmployeeCode)],
                NationalCode = values[nameof(EmployeeImportRow.NationalCode)],
                FirstName = values[nameof(EmployeeImportRow.FirstName)],
                LastName = values[nameof(EmployeeImportRow.LastName)],
                FatherName = values[nameof(EmployeeImportRow.FatherName)],
                OrgUnitCode = values[nameof(EmployeeImportRow.OrgUnitCode)],
                PositionCode = values[nameof(EmployeeImportRow.PositionCode)],
                StatusText = values[nameof(EmployeeImportRow.StatusText)],
                StartDateText = values[nameof(EmployeeImportRow.StartDateText)],
                EndDateText = values[nameof(EmployeeImportRow.EndDateText)],
                WorkEmail = values[nameof(EmployeeImportRow.WorkEmail)],
                InternalPhone = values[nameof(EmployeeImportRow.InternalPhone)],
                ManagerCode = values[nameof(EmployeeImportRow.ManagerCode)]
            });
        }

        if (rows.Count == 0)
        {
            throw new EmployeeImportException("import_no_rows", "هیچ ردیف داده‌ای در فایل پیدا نشد.");
        }

        if (rows.Count > limit)
        {
            throw new EmployeeImportException(
                "import_too_many_rows",
                $"فایل شامل بیش از سقف مجاز ({limit} ردیف) است. لطفاً فایل را بخش‌بندی کنید.");
        }

        return rows;
    }

    private static readonly string[] RequiredColumns =
    [
        nameof(EmployeeImportRow.EmployeeCode),
        nameof(EmployeeImportRow.FirstName),
        nameof(EmployeeImportRow.LastName),
        nameof(EmployeeImportRow.OrgUnitCode)
    ];

    /// <summary>تبدیل کلید ستون به نام فارسی برای پیام‌های خطا.</summary>
    private static string ColumnNameToPersian(string key) => key switch
    {
        nameof(EmployeeImportRow.EmployeeCode) => "کد پرسنلی",
        nameof(EmployeeImportRow.FirstName) => "نام",
        nameof(EmployeeImportRow.LastName) => "نام خانوادگی",
        nameof(EmployeeImportRow.OrgUnitCode) => "کد واحد سازمانی",
        nameof(EmployeeImportRow.NationalCode) => "کد ملی",
        nameof(EmployeeImportRow.FatherName) => "نام پدر",
        nameof(EmployeeImportRow.PositionCode) => "کد موقعیت شغلی",
        nameof(EmployeeImportRow.StatusText) => "وضعیت",
        nameof(EmployeeImportRow.StartDateText) => "تاریخ شروع",
        nameof(EmployeeImportRow.EndDateText) => "تاریخ پایان",
        nameof(EmployeeImportRow.WorkEmail) => "ایمیل سازمانی",
        nameof(EmployeeImportRow.InternalPhone) => "تلفن داخلی",
        nameof(EmployeeImportRow.ManagerCode) => "کد پرسنلی مدیر",
        _ => key
    };

    /// <summary>تطبیق عنوان ستون خام با کلید استاندارد.</summary>
    private static string? CanonicalizeColumnName(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var key = header.Trim().ToLowerInvariant()
            .Replace(Zwnj.ToString(), string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        return key switch
        {
            // کد پرسنلی
            "کدپرسنلی" or "کدکارمندی" or "کد" or "employeecode" or "employeenumber" or "code" => nameof(EmployeeImportRow.EmployeeCode),
            // کد ملی
            "کدملی" or "کدشناسایی" or "nationalcode" or "nationalid" or "nid" => nameof(EmployeeImportRow.NationalCode),
            // نام
            "نام" or "اسم" or "firstname" or "name" or "givenname" => nameof(EmployeeImportRow.FirstName),
            // نام خانوادگی
            "نامخانوادگی" or "lastname" or "surname" or "family" or "familyname" => nameof(EmployeeImportRow.LastName),
            // نام پدر
            "نامپدر" or "fathername" or "father" => nameof(EmployeeImportRow.FatherName),
            // کد واحد سازمانی
            "کدواحدسازمانی" or "کدواحد" or "کدبخش" or "کددفتر" or "orgunitcode" or "unitcode" or "departmentcode" or "orgunit" => nameof(EmployeeImportRow.OrgUnitCode),
            // کد موقعیت شغلی
            "کدموقعیتشغلی" or "کدموقعیت" or "کدشغل" or "positioncode" or "jobcode" or "position" => nameof(EmployeeImportRow.PositionCode),
            // وضعیت
            "وضعیت" or "وضعیتاشتغال" or "وضعیتشغلی" or "status" or "employeestatus" => nameof(EmployeeImportRow.StatusText),
            // تاریخ شروع
            "تاریخشروع" or "تاریخشروعبهکار" or "تاریخاستخدام" or "startdate" or "start" or "hiredate" => nameof(EmployeeImportRow.StartDateText),
            // تاریخ پایان
            "تاریخپایان" or "تاریخترککار" or "تاریخخاتمه" or "enddate" or "end" => nameof(EmployeeImportRow.EndDateText),
            // ایمیل سازمانی
            "ایمیلسازمانی" or "ایمیلکاری" or "ایمیل" or "workemail" or "email" => nameof(EmployeeImportRow.WorkEmail),
            // تلفن داخلی
            "تلفنداخلی" or "داخلی" or "internalphone" or "phone" or "extension" => nameof(EmployeeImportRow.InternalPhone),
            // کد پرسنلی مدیر
            "کدپرسنلیمدیر" or "کدمدیر" or "مدیر" or "managercode" or "manageremployeecode" or "manager" => nameof(EmployeeImportRow.ManagerCode),
            _ => null
        };
    }

    /// <summary>ساخت نگاشت «کلید استاندارد → شماره ستون» از ردیف عناوین.</summary>
    private static Dictionary<string, int> ReadColumnMap(XlsxRow headerRow)
    {
        var map = new Dictionary<string, int>();

        foreach (var (column, text) in headerRow.Cells)
        {
            var canonical = CanonicalizeColumnName(text);
            if (canonical is not null && !map.ContainsKey(canonical))
            {
                map[canonical] = column;
            }
        }

        return map;
    }

    /// <summary>
    /// تبدیل متن وضعیت به مقدار شمارشی. پیش‌فرض: «فعال».
    /// </summary>
    public static EmployeeStatus ParseStatus(string? text) => NormalizeKey(text) switch
    {
        "فعال" or "مشغول" or "active" or "employed" or "1" => EmployeeStatus.Active,
        "مرخصی" or "onleave" or "leave" or "2" => EmployeeStatus.OnLeave,
        "معلق" or "تعلیق" or "suspended" or "suspension" or "3" => EmployeeStatus.Suspended,
        "خاتمهیافته" or "خاتمه" or "ترککار" or "terminated" or "former" or "4" => EmployeeStatus.Terminated,
        _ => EmployeeStatus.Active
    };

    /// <summary>
    /// تجزیه‌ی تاریخ از متن. از فرمت‌های میلادی (ISO و…)، فرهنگ فارسی و
    /// الگوی شمسی (۱۴۰۳/۰۱/۱۵) پشتیبانی می‌کند.
    ///
    /// نکته: الگوی عددی «سال/ماه/روز» مبهم است — فرهنگ خنثی «۱۴۰۲/۰۳/۱۱» را
    /// به‌جای یک تاریخ شمسی، به‌عنوان سال میلادیِ ۱۴۰۲ می‌خواند. چون سال‌های
    /// میلادیِ ۱۳۰۰ تا ۱۵۰۰ برای تاریخ استخدام بی‌معنا هستند، این بازه را
    /// به‌عنوان سال شمسی تفسیر می‌کنیم.
    /// </summary>
    public static DateOnly? ParseDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        // ارقام فارسی/عربی را به لاتین نرمال می‌کنیم تا همه‌ی مسیرها یکدست باشند.
        var value = NormalizeDigits(text.Trim());

        // ۱. الگوی عددی ۴-۲-۲: شمسی یا میلادی.
        var match = DatePattern().Match(value);
        if (match.Success)
        {
            var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);

            // بازه‌ی ۱۳۰۰–۱۵۰۰ تقویم جلالی است؛ میلادیِ این بازه برای استخدام معنا ندارد.
            if (year is >= 1300 and <= 1500)
            {
                try
                {
                    return DateOnly.FromDateTime(new PersianCalendar().ToDateTime(year, month, day, 0, 0, 0, 0));
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }
            }

            try
            {
                return new DateOnly(year, month, day);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        // ۲. فرهنگ خنثی (میلادی، ISO و قالب‌های محلی).
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var gregorian))
        {
            return gregorian;
        }

        // ۳. فرهنگ فارسی (.NET از تقویم شمسی پشتیبانی می‌کند).
        try
        {
            if (DateOnly.TryParse(value, new CultureInfo("fa-IR"), out var persian))
            {
                return persian;
            }
        }
        catch (CultureNotFoundException)
        {
            // فرهنگ روی این سیستم نصب نیست.
        }

        return null;
    }

    /// <summary>تبدیل ارقام فارسی (۰۶F۰) و عربی (۰۶۶۰) به ارقام لاتین.</summary>
    private static string NormalizeDigits(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var ch in text)
        {
            if (ch >= '\u06F0' && ch <= '\u06F9')
            {
                builder.Append((char)('0' + (ch - '\u06F0')));
            }
            else if (ch >= '\u0660' && ch <= '\u0669')
            {
                builder.Append((char)('0' + (ch - '\u0660')));
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static string NormalizeKey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return text.Trim().ToLowerInvariant()
            .Replace(Zwnj.ToString(), string.Empty)
            .Replace(" ", string.Empty);
    }

    [GeneratedRegex(@"^(\d{4})[/\-.](\d{1,2})[/\-.](\d{1,2})$")]
    private static partial Regex DatePattern();
}
