using System.Globalization;
using System.IO.Compression;
using System.Text;
using ODCC.Application.Modules.Reporting.Abstractions;

namespace ODCC.Infrastructure.Modules.Reporting.Services;

/// <summary>
/// نوشتن کاربرگ Excel (OOXML/xlsx) بدون هیچ وابستگی خارجی.
///
/// <b>چرا به‌جای کتابخانه‌ی خارجی:</b> ماژول گزارش‌گیری فقط خروجی جدولی ساده
/// تولید می‌کند. نوشتن یک xlsx مینیمال با <see cref="ZipArchive"/> (بخشی از
/// چارچوب مشترک) کامل است و یک وابستگی سنگین (و مجوز جداگانه) اضافه نمی‌کند.
/// ساختار تولیدشده قابل باز کردن در Excel، LibreOffice و ابزارهای جدولی است.
///
/// **راست‌به‌چپ:** نماهای کاربرگ با <c>rightToLeft="1"</c> تولید می‌شوند تا
/// ستون‌ها برای کاربر فارسی از راست به چپ بچینند.
/// </summary>
public static class ExcelWorkbookWriter
{
    private const string DefaultSheetName = "گزارش";

    /// <summary>
    /// نوشتن بسته‌ی داده به‌صورت یک فایل xlsx در جریان خروجی.
    /// </summary>
    /// <param name="bundle">بسته‌ی داده‌ی گزارش.</param>
    /// <param name="output">جریان خروجی (قرار است dispose شود).</param>
    /// <returns>تعداد کل ردیف‌های داده نوشته‌شده.</returns>
    public static int Write(ReportDataBundle bundle, Stream output)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var sections = bundle.Sections;
        var sheetNames = BuildSheetNames(sections);
        var rowCount = sections.Sum(s => s.Rows.Count);

        // رشته‌های مشترک (shared strings) برای نگه‌داری یک نسخه‌ی واحد از هر متن.
        var sharedStrings = new List<string>();
        var stringIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        // تعداد کل ارجاع‌های سلول‌های رشته‌ای در کل کارتاب (مجموع سرستون‌ها و
        // داده‌ها). ویژگی <c>count</c> در sharedStrings باید این مقدار باشد، نه
        // تعداد رشته‌های یکتا (<c>uniqueCount</c>).
        var totalStringReferences = 0;

        using var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true);

        WriteEntry(archive, "[Content_Types].xml", ContentTypesXml(sheetNames.Count));
        WriteEntry(archive, "_rels/.rels", RootRelsXml);
        WriteEntry(archive, "xl/workbook.xml", WorkbookXml(sheetNames));
        WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelsXml(sheetNames.Count));
        WriteEntry(archive, "xl/styles.xml", StylesXml);

        for (var i = 0; i < sections.Count; i++)
        {
            var sheet = $"xl/worksheets/sheet{i + 1}.xml";
            WriteEntry(archive, sheet, SheetXml(sections[i], i + 1, sharedStrings, stringIndex, ref totalStringReferences));
        }

        WriteEntry(archive, "xl/sharedStrings.xml", SharedStringsXml(sharedStrings, totalStringReferences));

        return rowCount;
    }

    /// <summary>
    /// ساخت نام کاربرگ‌ها: عنوان بخش، یکتاشده در صورت تکرار و محدود به ۳۱ کاراکتر
    /// (محدودیت Excel) و بدون نویسه‌های ممنوع.
    /// </summary>
    private static List<string> BuildSheetNames(IReadOnlyList<ReportSection> sections)
    {
        var names = new List<string>(sections.Count);
        var used = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var section in sections)
        {
            var name = SanitizeSheetName(section.Title);

            if (used.TryGetValue(name, out var occurrence))
            {
                used[name] = occurrence + 1;
                var suffix = $" ({occurrence + 1})";
                name = name.Length + suffix.Length > 31
                    ? string.Concat(name.AsSpan(0, 31 - suffix.Length), suffix)
                    : name + suffix;
            }
            else
            {
                used[name] = 1;
            }

            names.Add(name);
        }

        return names.Count > 0 ? names : [DefaultSheetName];
    }

    private static string SanitizeSheetName(string title)
    {
        var builder = new StringBuilder();
        var invalid = new HashSet<char> { ':', '\\', '/', '?', '*', '[', ']' };

        foreach (var c in title.Trim())
        {
            if (invalid.Contains(c))
                continue;

            builder.Append(c);
        }

        var name = builder.ToString().Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            name = DefaultSheetName;
        }

        return name.Length > 31 ? name[..31] : name;
    }

    private static void WriteEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    /// <summary>نوع محتوای each part (همه‌ی کاربرگ‌ها + کتاب + رشته‌های مشترک).</summary>
    private static string ContentTypesXml(int sheetCount)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">");
        builder.Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>");
        builder.Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>");
        builder.Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>");
        builder.Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
        builder.Append("<Override PartName=\"/xl/sharedStrings.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml\"/>");

        for (var i = 1; i <= sheetCount; i++)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
        }

        builder.Append("</Types>");
        return builder.ToString();
    }

    private const string RootRelsXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private static string WorkbookXml(List<string> sheetNames)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        builder.Append("<sheets>");

        for (var i = 0; i < sheetNames.Count; i++)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<sheet name=\"{Escape(sheetNames[i])}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"/>");
        }

        builder.Append("</sheets>");
        builder.Append("</workbook>");
        return builder.ToString();
    }

    private static string WorkbookRelsXml(int sheetCount)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
        builder.Append("<Relationship Id=\"rIdStyles\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>");
        builder.Append("<Relationship Id=\"rIdShared\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings\" Target=\"sharedStrings.xml\"/>");

        for (var i = 1; i <= sheetCount; i++)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<Relationship Id=\"rId{i}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
        }

        builder.Append("</Relationships>");
        return builder.ToString();
    }

    /// <summary>
    /// سبک‌ها: ۰ = پیش‌فرض، ۱ = سرستون (پررنگ، وسط‌چین، پس‌زمینه)، ۲ = عدد،
    /// ۳ = درصد، ۴ = تاریخ. اعداد لاتین برای سازگاری با Excel نمایش داده می‌شوند.
    /// </summary>
    private const string StylesXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
        "<fonts count=\"2\">" +
        "<font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
        "<font><b/><sz val=\"11\"/><color rgb=\"FFFFFFFF\"/><name val=\"Calibri\"/></font>" +
        "</fonts>" +
        "<fills count=\"3\">" +
        "<fill><patternFill patternType=\"none\"/></fill>" +
        "<fill><patternFill patternType=\"gray125\"/></fill>" +
        "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FF2F4F6F\"/><bgColor rgb=\"FF2F4F6F\"/></patternFill></fill>" +
        "</fills>" +
        "<borders count=\"2\">" +
        "<border/>" +
        "<border>" +
        "<left style=\"thin\"><color rgb=\"FFBFBFBF\"/></left>" +
        "<right style=\"thin\"><color rgb=\"FFBFBFBF\"/></right>" +
        "<top style=\"thin\"><color rgb=\"FFBFBFBF\"/></top>" +
        "<bottom style=\"thin\"><color rgb=\"FFBFBFBF\"/></bottom>" +
        "</border>" +
        "</borders>" +
        "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
        "<cellXfs count=\"5\">" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
        "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\">" +
        "<alignment horizontal=\"center\" vertical=\"center\"/></xf>" +
        "<xf numFmtId=\"2\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"/>" +
        "<xf numFmtId=\"10\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"/>" +
        "<xf numFmtId=\"14\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\"/>" +
        "</cellXfs>" +
        "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
        "</styleSheet>";

    private static string SheetXml(
        ReportSection section,
        int sheetId,
        List<string> sharedStrings,
        Dictionary<string, int> stringIndex,
        ref int totalStringReferences)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");

        // ترتیب عناصر طبق طرح‌واره‌ی ECMA-376 (CT_Worksheet) الزامی است:
        // sheetPr، dimension، sheetViews، sheetFormatPr، cols، sheetData، ...
        // اگر ترتیب رعایت نشود، Excel فایل را خراب گزارش می‌کند. در گذشته
        // cols قبل از sheetViews نوشته می‌شد که نامعتبر بود.

        // محدوده‌ی سلول‌های استفاده‌شده (برای نمایش سریع و سازگاری با
        // ابزارهایی مثل LibreOffice که به آن وابسته‌اند).
        var lastColumn = section.Columns.Count > 0 ? ColumnLetter(section.Columns.Count - 1) : "A";
        var lastRow = Math.Max(1, section.Rows.Count + 1);
        builder.Append(CultureInfo.InvariantCulture, $"<dimension ref=\"A1:{lastColumn}{lastRow}\"/>");

        // راست‌به‌چپ برای فارسی.
        builder.Append("<sheetViews><sheetView workbookViewId=\"0\" rightToLeft=\"1\">");
        builder.Append(CultureInfo.InvariantCulture, $"<pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/>");
        builder.Append("</sheetView></sheetViews>");

        // عرض ستون‌ها از پهنای پیشنهادی ستون‌ها.
        builder.Append("<cols>");
        for (var i = 0; i < section.Columns.Count; i++)
        {
            var width = Math.Max(10, Math.Min(80, section.Columns[i].Width / 7.0));
            builder.Append(CultureInfo.InvariantCulture, $"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{width.ToString("0.0", CultureInfo.InvariantCulture)}\" customWidth=\"1\"/>");
        }

        builder.Append("</cols>");

        builder.Append("<sheetData>");

        // ردیف سرستون.
        builder.Append("<row r=\"1\">");
        for (var c = 0; c < section.Columns.Count; c++)
        {
            // عنوان سرستون مستقیماً به‌صورت رشته‌ی مشترک نوشته می‌شود (نه
            // اندیسِ آن) تا مسیر Cell با مجموعه‌های نامعتفر مواجه نشود.
            builder.Append(Cell('A', c, 1, section.Columns[c].Title, ref totalStringReferences, null, sharedStrings, stringIndex, 1));
        }

        builder.Append("</row>");

        // ردیف‌های داده.
        for (var r = 0; r < section.Rows.Count; r++)
        {
            var row = section.Rows[r];
            builder.Append(CultureInfo.InvariantCulture, $"<row r=\"{r + 2}\">");

            for (var c = 0; c < section.Columns.Count && c < row.Count; c++)
            {
                builder.Append(Cell('A', c, r + 2, row[c], ref totalStringReferences, section.Columns[c], sharedStrings, stringIndex));
            }

            builder.Append("</row>");
        }

        builder.Append("</sheetData>");

        // فیلتر خودکار روی سرستون برای راحتی تحلیل در ابزارهای جدولی.
        if (section.Columns.Count > 0)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<autoFilter ref=\"A1:{ColumnLetter(section.Columns.Count - 1)}{section.Rows.Count + 1}\"/>");
        }

        builder.Append("</worksheet>");
        return builder.ToString();
    }

    private static string Cell(
        char baseColumn,
        int columnIndex,
        int rowIndex,
        object? value,
        ref int totalStringReferences,
        ReportColumn? column = null,
        List<string>? sharedStrings = null,
        Dictionary<string, int>? stringIndex = null,
        int styleIndex = 0)
    {
        var reference = $"{ColumnReference(baseColumn, columnIndex)}{rowIndex}";

        switch (value)
        {
            case null:
                return $"<c r=\"{reference}\" s=\"{styleIndex}\"/>";

            case string text:
                totalStringReferences++;
                return $"<c r=\"{reference}\" s=\"{styleIndex}\" t=\"s\">" +
                       $"<v>{SharedString(text, sharedStrings!, stringIndex!)}</v></c>";

            case bool boolean:
                return $"<c r=\"{reference}\" s=\"{styleIndex}\" t=\"b\"><v>{(boolean ? 1 : 0)}</v></c>";

            case DateTime date:
                return FormattedCell(reference, 4, ToExcelDate(date));

            case decimal number:
                return NumberCell(reference, (double)number, column);

            case double number:
                return NumberCell(reference, number, column);

            case int number:
                return NumberCell(reference, number, column);

            case long number:
                return NumberCell(reference, number, column);

            case float number:
                return NumberCell(reference, number, column);

            default:
                totalStringReferences++;
                return $"<c r=\"{reference}\" s=\"{styleIndex}\" t=\"s\">" +
                       $"<v>{SharedString(value.ToString() ?? string.Empty, sharedStrings!, stringIndex!)}</v></c>";
        }
    }

    private static string FormattedCell(string reference, int style, string value) => $"<c r=\"{reference}\" s=\"{style}\"><v>{value}</v></c>";
    private static string NumberCell(string reference, double value, ReportColumn? column)
    {
        // درصد‌ها به‌صورت کسر اعشاری ذخیره می‌شوند تا Excel آن‌ها را با علامت
        // درصد نمایش دهد (numFmt 10 = 0.00%).
        var normalized = column?.ColumnType == ReportColumnType.Percent ? value / 100.0 : value;
        var style = column?.ColumnType switch
        {
            ReportColumnType.Percent => 3,
            _ => 2
        };

        return $"<c r=\"{reference}\" s=\"{style}\"><v>{normalized.ToString("R", CultureInfo.InvariantCulture)}</v></c>";
    }

    private static string SharedString(string text, List<string> sharedStrings, Dictionary<string, int> stringIndex)
    {
        if (stringIndex.TryGetValue(text, out var index))
        {
            return index.ToString(CultureInfo.InvariantCulture);
        }

        index = sharedStrings.Count;
        sharedStrings.Add(text);
        stringIndex[text] = index;

        return index.ToString(CultureInfo.InvariantCulture);
    }

    private static string SharedStringsXml(List<string> sharedStrings, int totalStringReferences)
    {
        var builder = new StringBuilder();
        builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        builder.Append("<sst xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"");
        builder.Append(CultureInfo.InvariantCulture, $" count=\"{totalStringReferences}\" uniqueCount=\"{sharedStrings.Count}\">");

        foreach (var text in sharedStrings)
        {
            builder.Append(CultureInfo.InvariantCulture, $"<si><t xml:space=\"preserve\">{Escape(text)}</t></si>");
        }

        builder.Append("</sst>");
        return builder.ToString();
    }

    /// <summary>تبدیل تاریخ به عدد سریال Excel (روز از ۱۸۹۹/۱۲/۳۰).</summary>
    private static string ToExcelDate(DateTime date)
    {
        var serial = (date.ToUniversalTime() - new DateTime(1899, 12, 30, 0, 0, 0, DateTimeKind.Utc)).TotalDays;
        return serial.ToString("0.######", CultureInfo.InvariantCulture);
    }

    /// <summary>مرجع ستون از مبنای ۲۶ (A، B، ...، AA).</summary>
    private static string ColumnReference(char baseColumn, int offset)
    {
        var index = (baseColumn - 'A') + offset;
        return ColumnLetter(index);
    }

    private static string ColumnLetter(int index)
    {
        var builder = new StringBuilder();

        do
        {
            builder.Insert(0, (char)('A' + index % 26));
            index = index / 26 - 1;
        }
        while (index >= 0);

        return builder.ToString();
    }

    private static string Escape(string text) =>
        text.Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
}
