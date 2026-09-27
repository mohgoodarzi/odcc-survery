using System.Text;
using ODCC.Application.Modules.Reporting.Abstractions;
using ODCC.Domain.Modules.Reporting.Enums;

namespace ODCC.Infrastructure.Modules.Reporting.Services;

/// <summary>
/// رندر گزارش به قالب Excel (OOXML) با <see cref="ExcelWorkbookWriter"/>.
/// هر بخش از بسته‌ی داده به یک کاربرگ تبدیل می‌شود.
/// </summary>
public sealed class ExcelReportRenderer : IReportRenderer
{
    /// <inheritdoc/>
    public ReportFormat Format => ReportFormat.Excel;

    /// <inheritdoc/>
    public Task<RenderedReport> RenderAsync(ReportDataBundle bundle, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(bundle);

        var stream = new MemoryStream();
        var rowCount = ExcelWorkbookWriter.Write(bundle, stream);
        stream.Position = 0;

        var fileName = $"{SanitizeFileName(bundle.Title)}.xlsx";

        return Task.FromResult(new RenderedReport(stream, fileName, MimeTypes.Excel) { RowCount = rowCount });
    }

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

/// <summary>
/// پسوندها و انواع محتوای رایج خروجی گزارش.
/// </summary>
internal static class MimeTypes
{
    public const string Excel = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public const string Pdf = "application/pdf";
}
