using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace ODCC.Infrastructure.Modules.Organization.ExcelImport;

/// <summary>
/// خواننده‌ی سبک فایل‌های اکسل (.xlsx) بدون وابستگی خارجی.
///
/// یک فایل .xlsx در واقع یک آرشیو ZIP از بخش‌های XML است. این کلاس
/// بخش‌های لازم (کاربرگ اول، رشته‌های مشترک و سبک‌ها) را باز کرده و
/// سلول‌ها را به متن تبدیل می‌کند. همان رویکرد بدون‌وابستگی‌ی
/// <c>ExcelWorkbookWriter</c> برای نوشتن اکسل اعمال شده است.
/// </summary>
internal sealed partial class XlsxReader : IDisposable
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipsNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelationshipsNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    private readonly ZipArchive _archive;
    private readonly string _worksheetPath;
    private readonly List<string> _sharedStrings = [];
    private readonly Dictionary<int, bool> _dateStyles = [];

    private XlsxReader(ZipArchive archive, string worksheetPath)
    {
        _archive = archive;
        _worksheetPath = worksheetPath;
    }

    /// <summary>
    /// باز کردن نخستین کاربرگ فایل. جریان ورودی ابتدا در حافظه کپی می‌شود
    /// تا قابل جستجو باشد (الزام خواندن ZIP).
    /// </summary>
    public static XlsxReader Open(Stream stream, int maxRows)
    {
        var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;

        var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);
        var reader = new XlsxReader(archive, ResolveFirstWorksheetPath(archive));
        reader.LoadSharedStrings();
        reader.LoadDateStyles();
        return reader;
    }

    /// <summary>خواندن همه‌ی ردیف‌های کاربرگ: شماره‌ی ردیف → نگاشت ستون → متن سلول.</summary>
    public List<XlsxRow> ReadRows(int maxRows)
    {
        var entry = _archive.GetEntry(_worksheetPath)
            ?? throw new EmployeeImportException("import_parse_failed", "ساختار فایل اکسل نامعتبر است.");

        using var sheetStream = entry.Open();
        using var xml = XmlReader.Create(sheetStream, new XmlReaderSettings
        {
            IgnoreWhitespace = true,
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null
        });

        var rows = new List<XlsxRow>();

        while (xml.Read())
        {
            if (xml.NodeType is XmlNodeType.Element && xml.LocalName == "row")
            {
                if (xml.IsEmptyElement)
                {
                    // ردیف تهی خودبسته‌شده: هیچ سلولی ندارد.
                    var emptyRowNumber = ParseIntAttribute(xml, "r") ?? rows.Count + 1;
                    rows.Add(new XlsxRow(emptyRowNumber, new Dictionary<int, string?>()));
                    continue;
                }

                var rowNumber = ParseIntAttribute(xml, "r") ?? rows.Count + 1;
                var cells = ReadRowCells(xml);
                rows.Add(new XlsxRow(rowNumber, cells));

                if (rows.Count > maxRows)
                {
                    // توقف زودهنگام برای جلوگیری از مصرف بیش از حد حافظه.
                    return rows;
                }
            }
        }

        return rows;
    }

    private static string ResolveFirstWorksheetPath(ZipArchive archive)
    {
        var workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new EmployeeImportException("import_parse_failed", "فایل اکسل ساختار استانداردی ندارد.");

        string? firstSheetId = null;
        using (var stream = workbookEntry.Open())
        using (var xml = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
        {
            while (xml.Read())
            {
                if (xml.NodeType is XmlNodeType.Element && xml.LocalName == "sheet")
                {
                    firstSheetId = xml.GetAttribute("id", RelationshipsNs);
                    break;
                }
            }
        }

        var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
        var targets = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (relationshipsEntry is not null)
        {
            using var stream = relationshipsEntry.Open();
            using var xml = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });
            while (xml.Read())
            {
                if (xml.NodeType is XmlNodeType.Element && xml.LocalName == "Relationship")
                {
                    var id = xml.GetAttribute("Id");
                    var target = xml.GetAttribute("Target");
                    if (id is not null && target is not null)
                    {
                        targets[id] = NormalizePartPath(target);
                    }
                }
            }
        }

        if (firstSheetId is not null && targets.TryGetValue(firstSheetId, out var path))
        {
            return path;
        }

        // مسیریابی ناموفق: پیش‌فرض نخستین کاربرگ.
        return "xl/worksheets/sheet1.xml";
    }

    private static string NormalizePartPath(string target)
    {
        // Target ممکن است مطلق (/xl/worksheets/sheet1.xml) یا نسبی (worksheets/sheet1.xml) باشد.
        return target.StartsWith('/')
            ? target.TrimStart('/')
            : $"xl/{target}";
    }

    private void LoadSharedStrings()
    {
        var entry = _archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return;
        }

        using var stream = entry.Open();
        using var xml = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });

        while (xml.Read())
        {
            if (xml.NodeType is XmlNodeType.Element && xml.LocalName == "si")
            {
                // یک <si> می‌تواند چندین <t> داشته باشد (مثلاً متن غنی‌شده).
                // همه را به هم می‌چسبانیم و سپس رشته‌ی نهایی را اضافه می‌کنیم.
                _sharedStrings.Add(ReadSharedStringContent(xml));
            }
        }
    }

    /// <summary>
    /// خواندن محتوای یک عنصر <c>si</c>. خواننده هنگام ورود روی عنصر <c>si</c> قرار دارد
    /// و پس از بازگشت روی عنصر پایانی <c>si</c> متوقف می‌شود.
    /// </summary>
    private static string ReadSharedStringContent(XmlReader xml)
    {
        if (xml.IsEmptyElement)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        while (true)
        {
            if (xml.NodeType is XmlNodeType.Element && xml.LocalName == "t")
            {
                // توجه: ReadElementContentAsString از </t> عبور می‌کند و خواننده را روی
                // گره بعدی قرار می‌دهد. نباید بلافاصله Read() صدا زد، چون ممکن است
                // همان گره، عنصر پایانی si باشد و با Read() از آن عبور کنیم. به‌جای آن
                // حلقه از نو بررسی می‌شود.
                builder.Append(xml.ReadElementContentAsString());
                continue;
            }

            if (xml.NodeType is XmlNodeType.EndElement && xml.LocalName == "si")
            {
                break;
            }

            if (!xml.Read())
            {
                break;
            }
        }

        return builder.ToString();
    }

    private void LoadDateStyles()
    {
        var entry = _archive.GetEntry("xl/styles.xml");
        if (entry is null)
        {
            return;
        }

        using var stream = entry.Open();
        using var xml = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Ignore, XmlResolver = null });

        // ۱. قالب‌های عددی سفارشی: شناسه → آیا تاریخ است؟
        var customFormats = new Dictionary<int, bool>();

        while (xml.Read())
        {
            if (xml.NodeType is XmlNodeType.Element && xml.LocalName == "numFmt")
            {
                var id = ParseIntAttribute(xml, "numFmtId");
                var code = xml.GetAttribute("formatCode");
                if (id is not null && code is not null)
                {
                    customFormats[id.Value] = IsDateFormatCode(code);
                }
            }
            else if (xml.NodeType is XmlNodeType.Element && xml.LocalName == "cellXfs")
            {
                // ۲. نگاشت «اندیس سبک سلول → شناسه‌ی قالب عددی».
                var styleIndex = 0;
                while (xml.Read())
                {
                    if (xml.NodeType is XmlNodeType.Element && xml.LocalName == "xf")
                    {
                        var numFmtId = ParseIntAttribute(xml, "numFmtId") ?? 0;
                        _dateStyles[styleIndex] = customFormats.TryGetValue(numFmtId, out var custom)
                            ? custom
                            : IsBuiltInDateFormat(numFmtId);
                        styleIndex++;
                    }
                    else if (xml.NodeType is XmlNodeType.EndElement && xml.LocalName == "cellXfs")
                    {
                        break;
                    }
                }
                break;
            }
        }
    }

    private static bool IsBuiltInDateFormat(int numFmtId) =>
        numFmtId is (>= 14 and <= 22) or (>= 27 and <= 36) or (>= 45 and <= 47) or (>= 50 and <= 58);

    private static bool IsDateFormatCode(string code)
    {
        // قالب‌های تاریخ حاوی نشانه‌های y/d/h/s هستند؛ بخش‌های نقل‌قولی و رنگ نادیده گرفته می‌شوند.
        var withoutQuotes = QuotedSection().Replace(code, string.Empty);
        var lower = withoutQuotes.ToLowerInvariant();
        return lower.Contains('y') || lower.Contains('d') || lower.Contains('h') || lower.Contains('s');
    }

    [GeneratedRegex(@"\[[^\]]*\]|""[^""]*""")]
    private static partial Regex QuotedSection();

    private Dictionary<int, string?> ReadRowCells(XmlReader xml)
    {
        var cells = new Dictionary<int, string?>();

        while (xml.Read())
        {
            if (xml.NodeType is XmlNodeType.Element && xml.LocalName == "c")
            {
                var (column, text) = ReadCell(xml);
                if (column > 0)
                {
                    cells[column] = text;
                }
            }
            else if (xml.NodeType is XmlNodeType.EndElement && xml.LocalName == "row")
            {
                // خواننده روی </row> متوقف شده؛ حلقه‌ی بیرونی با Read() بعدی از آن عبور می‌کند.
                break;
            }
        }

        return cells;
    }

    private (int Column, string? Text) ReadCell(XmlReader xml)
    {
        var reference = xml.GetAttribute("r") ?? string.Empty;
        var type = xml.GetAttribute("t");
        var styleIndex = ParseIntAttribute(xml, "s");
        var column = ParseColumn(reference);

        var isDateStyle = styleIndex is not null && _dateStyles.GetValueOrDefault(styleIndex.Value, false);

        if (xml.IsEmptyElement)
        {
            // سلول خودبسته‌شده بدون مقدار.
            return (column, null);
        }

        string? value = null;

        while (true)
        {
            if (xml.NodeType is XmlNodeType.Element && (xml.LocalName == "v" || xml.LocalName == "t"))
            {
                // توجه: ReadElementContentAsString از </v> عبور می‌کند و خواننده را روی
                // گره بعدی (معمولاً </c>) قرار می‌دهد. اگر اینجا Read() صدا بزنیم، از
                // </c> عبور کرده و سلول‌های بعدی قورت داده می‌شوند؛ بنابراین حلقه از نو
                // بررسی می‌شود تا روی عنصر پایانی توقف کنیم.
                value = xml.ReadElementContentAsString();
                continue;
            }

            if (xml.NodeType is XmlNodeType.EndElement && xml.LocalName == "c")
            {
                break;
            }

            if (!xml.Read())
            {
                break;
            }
        }

        var text = type switch
        {
            "s" => ResolveSharedString(value),
            "b" => value is "1" or "true" ? "true" : "false",
            "str" or "inlineStr" => value,
            // t="d": تاریخ به‌صورت متنی ISO
            "d" => value,
            "e" => null,
            null or "" => ResolveNumeric(value, isDateStyle),
            _ => value
        };

        return (column, text);
    }

    private string? ResolveSharedString(string? value)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index)
            && index >= 0 && index < _sharedStrings.Count)
        {
            return _sharedStrings[index];
        }

        return null;
    }

    private static string? ResolveNumeric(string? value, bool isDate)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return value;
        }

        if (isDate)
        {
            try
            {
                return DateTime.FromOADate(number).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }
            catch (ArgumentException)
            {
                return value;
            }
        }

        return Math.Truncate(number) == number
            ? ((long)number).ToString(CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>تبدیل مرجع سلول (مثل «B3») به شماره‌ی ستون یک‌پایه.</summary>
    private static int ParseColumn(string reference)
    {
        var column = 0;
        foreach (var ch in reference)
        {
            if (char.IsAsciiLetterUpper(ch))
            {
                column = column * 26 + (ch - 'A' + 1);
            }
            else if (char.IsDigit(ch))
            {
                break;
            }
        }

        return column;
    }

    private static int? ParseIntAttribute(XmlReader xml, string name)
    {
        var value = xml.GetAttribute(name);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    public void Dispose() => _archive.Dispose();
}

/// <summary>یک ردیف کاربرگ: شماره‌ی ردیف و سلول‌های آن.</summary>
internal sealed record XlsxRow(int RowNumber, Dictionary<int, string?> Cells);
