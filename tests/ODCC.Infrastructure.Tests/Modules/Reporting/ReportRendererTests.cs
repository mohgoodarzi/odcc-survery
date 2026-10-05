using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using FluentAssertions;
using ODCC.Application.Modules.Reporting.Abstractions;
using ODCC.Domain.Modules.Reporting.Enums;
using ODCC.Infrastructure.Modules.Reporting.Services;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Reporting;

/// <summary>
/// آزمون‌های رندر خروجی گزارش در سطح رندر (PDF و Excel).
///
/// این آزمون‌ها مستقیماً <see cref="PdfReportRenderer"/> و
/// <see cref="ExcelReportRenderer"/> را با بسته‌های داده‌ی واقعی فراخوانی
/// می‌کنند و صحت ساختار فایل تولیدشده را بررسی می‌کنند — نه محتوای پایگاه
/// داده را. هدف این است که خروجی‌ها در نرم‌افزارهای واقعی (خواننده‌ی PDF و
/// Excel) باز شوند و متن فارسی/راست‌به‌چپ درست رندر شود.
/// </summary>
public class ReportRendererTests
{
    /// <summary>
    /// بسته‌ی داده‌ی نمونه با متن فارسی، اعداد، درصدها و یادداشت — شبیه
    /// بخش «شاخص‌های کلیدی» یک گزارش واقعی.
    /// </summary>
    private static ReportDataBundle SampleBundle() => new()
    {
        Title = "گزارش نمونه",
        Subtitle = "نظرسنجی: نظرسنجی گزارش • بازه: ۲۰۲۶-۰۱-۰۱ تا ۲۰۲۶-۰۲-۰۱",
        Type = ReportType.SurveyAnalytics,
        GeneratedAt = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
        GeneratedBy = "testuser",
        Sections =
        [
            new ReportSection
            {
                Title = "شاخص‌های کلیدی",
                Columns =
                [
                    new("شاخص", 200, ReportColumnType.Text),
                    new("مقدار", 100, ReportColumnType.Number),
                    new("نرخ تکمیل", 100, ReportColumnType.Percent)
                ],
                Rows =
                [
                    ["میانگین امتیاز", 12.5m, 33.3m],
                    ["تعداد پاسخ", 40L, 50.0]
                ],
                Footnote = "کد سؤال: QB-R-NPS"
            }
        ]
    };

    /// <summary>
    /// بسته‌ی داده‌ی بخش بنچمارک: شش ستون که مجموع عرض آن‌ها از پهنای
    /// مفید A4 بیشتر است. این مورد قبلاً باعث
    /// <c>QuestPDF.Drawing.Exceptions.DocumentLayoutException</c> می‌شد.
    /// </summary>
    private static ReportDataBundle WideBenchmarkBundle() => new()
    {
        Title = "گزارش بنچمارک",
        Subtitle = "تمام شرکت",
        Type = ReportType.BenchmarkComparison,
        GeneratedAt = DateTime.UtcNow,
        GeneratedBy = "testuser",
        Sections =
        [
            new ReportSection
            {
                Title = "مقایسه با بنچمارک",
                Columns =
                [
                    new("شاخص", 200, ReportColumnType.Text),
                    new("نام بنچمارک", 200, ReportColumnType.Text),
                    new("مقدار واقعی", 110, ReportColumnType.Number),
                    new("مقدار هدف", 110, ReportColumnType.Number),
                    new("اختلاف", 100, ReportColumnType.Number),
                    new("وضعیت", 120, ReportColumnType.Text)
                ],
                Rows =
                [
                    ["NPS (شاخص خالص ترویج)", "هدف سالانه", 12.5m, 20m, -7.5m, "پایین‌تر از هدف"],
                    ["CSAT (رضایت)", "هدف سالانه", 85m, 80m, 5m, "بالاتر از هدف"]
                ]
            }
        ]
    };

    // --- PDF -----------------------------------------------------------------

    [Fact]
    public async Task Pdf_Produces_Valid_Document_With_Persian_Font()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var rendered = await new PdfReportRenderer().RenderAsync(SampleBundle());

        using var buffer = new MemoryStream();
        await rendered.Content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        // سرآیند و پایان‌وند PDF — نشانه‌ی یک سندِ بازشدنی.
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
        Encoding.ASCII.GetString(bytes, bytes.Length - 1024, 1024)
            .TrimEnd('\0', '\n', '\r', ' ')
            .Should().EndWith("%%EOF");

        rendered.ContentType.Should().Be("application/pdf");
        rendered.FileName.Should().EndWith(".pdf");
        // نام فایل از عنوان گزارش ساخته می‌شود (و نویسه‌های نامعتبر حذف می‌شوند).
        rendered.FileName.Should().StartWith("گزارش نمونه");
        rendered.RowCount.Should().Be(2);

        var text = Encoding.Latin1.GetString(bytes);

        // فونت فارسی باید در سند جاسازی (embed) شود؛ وگرنه متن فارسی در
        // خواننده‌های PDF نامرئی رندر می‌شود. فونت همراه QuestPDF (Lato)
        // گلیف‌های فارسی ندارد.
        text.Should().Contain("/FontFile2");
        text.Should().Contain("Tahoma");

        // برای قابل‌استخراج‌بودن متن (selectable text) باید CMap یونیکد وجود داشته باشد.
        text.Should().Contain("/ToUnicode");

        // متن فارسی باید واقعاً در سند رندر شده باشد (عملگرهای نمایش متن).
        var content = Encoding.Latin1.GetString(DecompressContentStreams(bytes).ToArray());
        content.Should().Contain("Tj");
    }

    [Fact]
    public async Task Pdf_Renders_Wide_Benchmark_Table_Without_Layout_Error()
    {
        // رگرسیون: بخش بنچمارک شش ستونِ پهن دارد که از پهنای A4 بیشتر است.
        // قبلاً GeneratePdf با DocumentLayoutException شکست می‌خورد.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var rendered = await new PdfReportRenderer().RenderAsync(WideBenchmarkBundle());

        using var buffer = new MemoryStream();
        await rendered.Content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        bytes.Length.Should().BeGreaterThan(0);
        Encoding.ASCII.GetString(bytes, 0, 4).Should().Be("%PDF");
        rendered.RowCount.Should().Be(2);
    }

    [Fact]
    public async Task Pdf_Handles_Empty_Sections()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        var bundle = SampleBundle() with { Sections = [] };

        var rendered = await new PdfReportRenderer().RenderAsync(bundle);

        using var buffer = new MemoryStream();
        await rendered.Content.CopyToAsync(buffer);
        Encoding.ASCII.GetString(buffer.ToArray(), 0, 4).Should().Be("%PDF");
    }

    /// <summary>
    /// باز کردن جریان‌های محتوای فشرده‌شده‌ی PDF (FlateDecode/zlib) برای
    /// بازرسی عملگرهای رندر متن.
    /// </summary>
    private static MemoryStream DecompressContentStreams(byte[] pdfBytes)
    {
        var text = Encoding.Latin1.GetString(pdfBytes);
        var output = new MemoryStream();

        foreach (Match match in Regex.Matches(text, @"stream\r?\n"))
        {
            var start = match.Index + match.Length;
            var end = text.IndexOf("endstream", start, StringComparison.Ordinal);
            if (end < 0) continue;

            var segment = Encoding.Latin1.GetBytes(text.Substring(start, end - start));

            try
            {
                using var ms = new MemoryStream(segment);
                using var zlib = new ZLibStream(ms, CompressionMode.Decompress);
                zlib.CopyTo(output);
            }
            catch
            {
                // جریان غیر Flate (مثل تصاویر)؛ نادیده گرفته می‌شود.
            }
        }

        output.Position = 0;
        return output;
    }

    // --- Excel ---------------------------------------------------------------

    [Fact]
    public async Task Excel_Produces_Valid_Ooxml_Structure()
    {
        var rendered = await new ExcelReportRenderer().RenderAsync(SampleBundle());

        rendered.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        rendered.FileName.Should().EndWith(".xlsx");
        rendered.FileName.Should().StartWith("گزارش نمونه");
        rendered.RowCount.Should().Be(2);

        using var buffer = new MemoryStream();
        await rendered.Content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        // امضای ZIP در سرآغاز فایل xlsx.
        bytes[0].Should().Be(0x50); // 'P'
        bytes[1].Should().Be(0x4B); // 'K'

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var entryNames = archive.Entries.Select(e => e.FullName).ToList();

        // بخش‌های الزامی یک کارتاب OOXML.
        entryNames.Should().Contain("[Content_Types].xml");
        entryNames.Should().Contain("_rels/.rels");
        entryNames.Should().Contain("xl/workbook.xml");
        entryNames.Should().Contain("xl/sharedStrings.xml");
        entryNames.Should().Contain("xl/styles.xml");
        entryNames.Should().Contain("xl/worksheets/sheet1.xml");

        var sheet = ReadEntry(archive, "sheet1.xml");

        // رگرسیون: ترتیب عناصر طبق طرح‌واره‌ی ECMA-376 باید
        // sheetViews قبل از cols بیاید. قبلاً برعکس نوشته می‌شد و Excel
        // فایل را خراب گزارش می‌کرد.
        var sheetViewsIndex = sheet.IndexOf("<sheetViews>", StringComparison.Ordinal);
        var colsIndex = sheet.IndexOf("<cols>", StringComparison.Ordinal);
        var sheetDataIndex = sheet.IndexOf("<sheetData>", StringComparison.Ordinal);

        sheetViewsIndex.Should().BeGreaterThan(0);
        colsIndex.Should().BeGreaterThan(sheetViewsIndex);
        sheetDataIndex.Should().BeGreaterThan(colsIndex);

        // محدوده‌ی سلول‌ها برای سازگاری با ابزارهای جدولی.
        sheet.Should().Contain("<dimension ref=\"A1:C3\"/>");

        // چینش راست‌به‌چپ برای فارسی.
        sheet.Should().Contain("rightToLeft=\"1\"");

        // سرستون یخ‌زده برای راحتی پیمایش.
        sheet.Should().Contain("state=\"frozen\"");

        // فیلتر خودکار روی محدوده‌ی داده.
        sheet.Should().Contain("<autoFilter ref=\"A1:C3\"/>");

        // رگرسیون: در styles.xml عناصر رنگ باید از ویژگی <c>rgb</c> استفاده
        // کنند، نه <c>val</c>. طبق طرح‌واره‌ی ECMA-376 (CT_Color) ویژگی‌های
        // مجاز auto/indexed/rgb/theme/tint هستند و <c>val</c> جزو آن‌ها نیست.
        // قبلاً <c>val</c> نوشته می‌شد: Excel فایل را باز می‌کرد ولی رنگ‌ها را
        // نادیده می‌گرفت و خواننده‌های سخت‌گیرتر (مثل openpyxl) کل فایل را
        // رد می‌کردند.
        var styles = ReadEntry(archive, "styles.xml");

        styles.Should().Contain("<fgColor rgb=\"FF2F4F6F\"/>");
        styles.Should().Contain("<color rgb=\"FFFFFFFF\"/>");
        styles.Should().Contain("<color rgb=\"FFBFBFBF\"/>");

        // هیچ عنصر رنگی نباید از ویژگی نامعتبر val استفاده کند.
        Regex.Count(styles, @"<(fg|bg)?Color\b[^>]*\bval=""")
            .Should().Be(0, "عناصر رنگ باید از ویژگی rgb استفاده کنند، نه val");

        // عناصری که طبق طرح‌واره val دارند نباید آسیب ببینند.
        styles.Should().Contain("<sz val=\"11\"/>");
        styles.Should().Contain("<name val=\"Calibri\"/>");
    }

    [Fact]
    public async Task Excel_Embeds_Persian_Text_And_Correct_Shared_String_Counts()
    {
        var rendered = await new ExcelReportRenderer().RenderAsync(SampleBundle());

        using var buffer = new MemoryStream();
        await rendered.Content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var sharedStrings = ReadEntry(archive, "sharedStrings.xml");

        // متن فارسی باید در رشته‌های مشترک ذخیره شده باشد.
        sharedStrings.Should().Contain("شاخص");
        sharedStrings.Should().Contain("میانگین امتیاز");

        // count = تعداد کل ارجاع‌های سلولی به رشته‌ها (سرستون + داده‌های متنی)؛
        // uniqueCount = تعداد رشته‌های یکتا. هر دو باید درست باشند تا Excel
        // فایل را معتبر بداند.
        var count = ExtractAttribute(sharedStrings, "count");
        var uniqueCount = ExtractAttribute(sharedStrings, "uniqueCount");

        // ۳ سرستون + ۲ سلول متنی داده = ۵ ارجاع؛ ۵ رشته‌ی یکتا.
        count.Should().Be("5");
        uniqueCount.Should().Be("5");

        // تعداد گره‌های <si> باید با uniqueCount هم‌خوان باشد.
        Regex.Count(sharedStrings, "<si>").Should().Be(5);
    }

    [Fact]
    public async Task Excel_Stores_Percent_As_Fraction_With_Percent_Format()
    {
        var rendered = await ExcelReportRenderer();

        using var buffer = new MemoryStream();
        await rendered.Content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var sheet = ReadEntry(archive, "sheet1.xml");

        // ستون سوم (C) از نوع درصد است. مقدار ۳۳.۳ به‌صورت کسر ۰٫۳۳۳ ذخیره
        // می‌شود تا Excel آن را با علامت درصد نمایش دهد (numFmt 10 = 0.00%).
        sheet.Should().Contain("<c r=\"C2\" s=\"3\">");
        sheet.Should().Contain("0.33");

        // ستون دوم (B) عدد ساده است (numFmt 2).
        sheet.Should().Contain("<c r=\"B2\" s=\"2\">");
    }

    [Fact]
    public async Task Excel_Renders_Wide_Benchmark_Table()
    {
        var rendered = await new ExcelReportRenderer().RenderAsync(WideBenchmarkBundle());

        using var buffer = new MemoryStream();
        await rendered.Content.CopyToAsync(buffer);
        var bytes = buffer.ToArray();

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var sheet = ReadEntry(archive, "sheet1.xml");

        // عنوان بخش به نام کاربرگ تبدیل می‌شود (در workbook.xml).
        var workbook = ReadEntry(archive, "workbook.xml");
        workbook.Should().Contain("مقایسه با بنچمارک");

        // داده‌های متنی در رشته‌های مشترک ذخیره می‌شوند.
        var sharedStrings = ReadEntry(archive, "sharedStrings.xml");
        sharedStrings.Should().Contain("هدف سالانه");

        sheet.Should().Contain("<autoFilter ref=\"A1:F3\"/>");
    }

    [Fact]
    public async Task Excel_Sanitizes_Sheet_Names_And_Uniquifies_Duplicates()
    {
        var bundle = SampleBundle() with
        {
            Sections =
            [
                new ReportSection { Title = "بخش یک", Columns = [new("ستون", 100)], Rows = [["x"]] },
                new ReportSection { Title = "بخش یک", Columns = [new("ستون", 100)], Rows = [["y"]] },
                new ReportSection { Title = "گزارش بسیار طولانی با عنوانی که از سی و یک کاراکتر بیشتر است", Columns = [new("ستون", 100)], Rows = [["z"]] }
            ]
        };

        var rendered = await new ExcelReportRenderer().RenderAsync(bundle);

        using var buffer = new MemoryStream();
        await rendered.Content.CopyToAsync(buffer);

        using var archive = new ZipArchive(new MemoryStream(buffer.ToArray()), ZipArchiveMode.Read);
        var workbook = ReadEntry(archive, "workbook.xml");

        // نام کاربرگ دوم باید یکتاشده باشد.
        workbook.Should().Contain("بخش یک (2)");

        // نام‌های کاربرگ حداکثر ۳۱ کاراکتر (محدودیت Excel).
        foreach (Match match in Regex.Matches(workbook, "<sheet name=\"([^\"]+)\""))
        {
            match.Groups[1].Value.Length.Should().BeLessThanOrEqualTo(31);
        }
    }

    private static async Task<RenderedReport> ExcelReportRenderer()
    {
        var rendered = await new ExcelReportRenderer().RenderAsync(SampleBundle());
        return rendered;
    }

    /// <summary>خواندن محتوای یک ورودی آرشیو به‌صورت متن UTF-8.</summary>
    private static string ReadEntry(ZipArchive archive, string entryName)
    {
        var entry = archive.Entries.Single(e => e.Name == entryName);
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>استخراج مقدار یک ویژگی از گره‌ی <c>sst</c>.</summary>
    private static string ExtractAttribute(string xml, string name)
    {
        var match = Regex.Match(xml, $"<sst[^>]*\\b{name}=\"(\\d+)\"");
        match.Success.Should().BeTrue($"expected '{name}' attribute on <sst>");
        return match.Groups[1].Value;
    }
}
