using System.Globalization;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ODCC.Application.Modules.Reporting.Abstractions;
using ODCC.Domain.Modules.Reporting.Enums;

namespace ODCC.Infrastructure.Modules.Reporting.Services;

/// <summary>
/// رندر گزارش به قالب PDF با QuestPDF.
///
/// **فارسی و راست‌به‌چپ:** کل صفحه با <c>ContentFromRightToLeft</c> رندر می‌شود
/// و متن‌ها با <c>DirectionFromRightToLeft</c> چیده می‌شوند. فونت گزارش از طریق
/// <see cref="ReportFontLoader"/> بارگذاری می‌شود.
/// </summary>
public sealed class PdfReportRenderer : IReportRenderer
{
    /// <inheritdoc/>
    public ReportFormat Format => ReportFormat.Pdf;

    /// <inheritdoc/>
    public Task<RenderedReport> RenderAsync(ReportDataBundle bundle, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(style => style
                    .FontFamily(ReportFontLoader.FontFamily)
                    .FontSize(10));

                page.Header().Element(compose => ComposeHeader(compose, bundle));
                page.Content().Element(compose => ComposeContent(compose, bundle));
                page.Footer().Element(compose => ComposeFooter(compose, bundle));
            });
        });

        var stream = new MemoryStream();
        document.GeneratePdf(stream);
        stream.Position = 0;

        var rowCount = bundle.Sections.Sum(s => s.Rows.Count);
        var fileName = $"{SanitizeFileName(bundle.Title)}.pdf";

        return Task.FromResult(new RenderedReport(stream, fileName, MimeTypes.Pdf) { RowCount = rowCount });
    }

    private static void ComposeHeader(IContainer container, ReportDataBundle bundle)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.RelativeItem().Column(titleColumn =>
                {
                    titleColumn.Item().Text(bundle.Title)
                        .FontSize(18)
                        .Bold()
                        .FontColor(Colors.Blue.Darken2);

                    titleColumn.Item().Text(bundle.Subtitle)
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                });

                // متاداده‌ی سمت چپ صفحه (تاریخ و راه‌انداز) برای جلوگیری از تداخل
                // با چینش راست‌به‌چپ، مستقل و بدون چرخش متن نمایش داده می‌شود.
                row.ConstantItem(160).AlignLeft().Column(metaColumn =>
                {
                    metaColumn.Item().Text($"{ReportPdfLabels.GeneratedAt}: {bundle.GeneratedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);

                    metaColumn.Item().Text($"{ReportPdfLabels.GeneratedBy}: {bundle.GeneratedBy}")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                });
            });

            column.Item().PaddingVertical(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeContent(IContainer container, ReportDataBundle bundle)
    {
        if (bundle.Sections.Count == 0)
            return;

        container.Column(column =>
        {
            foreach (var section in bundle.Sections)
            {
                column.Item().PaddingBottom(12).Element(compose => ComposeSection(compose, section));
            }
        });
    }

    private static void ComposeSection(IContainer container, ReportSection section)
    {
        container.Column(column =>
        {
            column.Item().Text(section.Title)
                .FontSize(12)
                .Bold()
                .FontColor(Colors.Blue.Darken1);

            column.Spacing(4);

            column.Item().Element(compose => ComposeTable(compose, section));

            if (!string.IsNullOrWhiteSpace(section.Footnote))
            {
                column.Item().Text(section.Footnote)
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken1);
            }
        });
    }

    private static void ComposeTable(IContainer container, ReportSection section)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                foreach (var column in section.Columns)
                {
                    // عرض ستون از پوینت بسته‌ی داده به نسبت اندازه‌ی A4 تبدیل می‌شود.
                    columns.ConstantColumn(Math.Clamp((float)column.Width * 0.75f, 40f, 220f));
                }
            });

            table.Header(header =>
            {
                foreach (var column in section.Columns)
                {
                    header.Cell().Element(cell => ComposeHeaderCell(cell).Text(column.Title));
                }
            });

            foreach (var row in section.Rows)
            {
                for (var i = 0; i < section.Columns.Count && i < row.Count; i++)
                {
                    var value = row[i];
                    var column = section.Columns[i];
                    table.Cell().Element(cell => ComposeDataCell(cell, column).Text(FormatValue(value, column)));
                }
            }
        });
    }

    private static IContainer ComposeHeaderCell(IContainer container) => container
        .Background(Colors.Blue.Darken2)
        .Padding(5)
        .AlignMiddle()
        .AlignCenter();

    private static IContainer ComposeDataCell(IContainer container, ReportColumn column)
    {
        var styled = container
            .Border(0.5f)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.White)
            .Padding(5)
            .AlignMiddle();

        // در چینش راست‌به‌چپ، متن از سمت راست شروع می‌شود و اعداد چپ‌چین می‌مانند
        // تا ارقام به‌درستی و بدون جابجایی خوانده شوند.
        return column.ColumnType == ReportColumnType.Text ? styled.AlignRight() : styled.AlignLeft();
    }

    private static void ComposeFooter(IContainer container, ReportDataBundle bundle)
    {
        container.Column(column =>
        {
            column.Item().AlignCenter().Text(text =>
            {
                text.Span($"{ReportPdfLabels.Page} ");
                text.CurrentPageNumber();
                text.Span($" {ReportPdfLabels.Of} ");
                text.TotalPages();
            });

            column.Item().PaddingTop(4).AlignCenter()
                .Text($"{ReportPdfLabels.DocumentTitle}: {bundle.Title}")
                .FontSize(7)
                .FontColor(Colors.Grey.Darken2);
        });
    }

    /// <summary>قالب‌بندی مقدار یک سلول بر اساس نوع ستون.</summary>
    private static string FormatValue(object? value, ReportColumn column)
    {
        if (value is null)
            return "—";

        return column.ColumnType switch
        {
            ReportColumnType.Percent when value is decimal d => FormatDecimal(d) + "٪",
            ReportColumnType.Percent when value is double dbl => FormatDecimal((decimal)dbl) + "٪",
            ReportColumnType.Number when value is decimal d => FormatDecimal(d),
            ReportColumnType.Number when value is double dbl => FormatDecimal((decimal)dbl),
            ReportColumnType.Date when value is DateTime date => date.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? "—"
        };
    }

    private static string FormatDecimal(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string SanitizeFileName(string title)
    {
        var builder = new StringBuilder();
        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());

        foreach (var c in title.Trim())
        {
            if (!invalid.Contains(c))
            {
                builder.Append(c);
            }
        }

        var name = builder.ToString().Trim();

        return name.Length > 120 ? name[..120] : name;
    }
}

/// <summary>برچسب‌های ثابت بخش‌های ساختاری PDF (راست‌چین).</summary>
internal static class ReportPdfLabels
{
    public const string GeneratedAt = "تاریخ تولید";
    public const string GeneratedBy = "تولیدکننده";
    public const string Page = "صفحه";
    public const string Of = "از";
    public const string DocumentTitle = "سند";
}
