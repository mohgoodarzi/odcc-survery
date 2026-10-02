using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Organization.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Organization.Entities;
using ODCC.Domain.Modules.Organization.Enums;
using ODCC.Infrastructure.Modules.Organization.ExcelImport;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Organization;

/// <summary>
/// آزمون‌های ورود گروهی کارمندان از فایل اکسل: تجزیه‌ی فایل، اعتبارسنجی،
/// شناسایی کد تکراری، ارجاع به مدیر داخل همان فایل و محدودسازی دامنه.
/// </summary>
public class EmployeeExcelImportTests
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipsNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly string[] ModulePermissions =
    [
        Permissions.Organization.EmployeesView,
        Permissions.Organization.EmployeesManage,
        Permissions.Organization.UnitsView,
        Permissions.Organization.PositionsView
    ];

    // داده‌ی نمونه (هماهنگ با seed/employees-sample.xlsx)
    private static readonly string[] Headers =
    [
        "کد پرسنلی", "کد ملی", "نام", "نام خانوادگی", "نام پدر",
        "کد واحد سازمانی", "کد موقعیت شغلی", "وضعیت", "تاریخ شروع", "تاریخ پایان",
        "ایمیل سازمانی", "تلفن داخلی", "کد پرسنلی مدیر"
    ];

    /// <summary>
    /// ساخت یک فایل اکسل واقعی (آرشیو ZIP از XML) درون حافظه. همه‌ی سلول‌ها
    /// رشته‌ی مشترک هستند؛ تاریخ‌ها ISO میلادی هستند که ParseDate می‌فهمد.
    /// </summary>
    private static MemoryStream BuildXlsx(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var sharedStrings = new List<string>();
        var indexByValue = new Dictionary<string, int>();

        var sheet = new StringBuilder();
        sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sheet.Append($"<worksheet xmlns=\"{SpreadsheetNs}\"><sheetData>");

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            sheet.Append(CultureInfo.InvariantCulture, $"<row r=\"{rowIndex + 1}\">");

            for (var columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                var value = rows[rowIndex][columnIndex] ?? string.Empty;
                if (!indexByValue.TryGetValue(value, out var sharedIndex))
                {
                    sharedIndex = sharedStrings.Count;
                    indexByValue[value] = sharedIndex;
                    sharedStrings.Add(value);
                }

                sheet.Append(CultureInfo.InvariantCulture,
                    $"<c r=\"{ToColumnLetter(columnIndex + 1)}{rowIndex + 1}\" t=\"s\"><v>{sharedIndex}</v></c>");
            }

            sheet.Append("</row>");
        }

        sheet.Append("</sheetData></worksheet>");

        var strings = new StringBuilder();
        strings.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        strings.Append(CultureInfo.InvariantCulture,
            $"<sst xmlns=\"{SpreadsheetNs}\" count=\"{sharedStrings.Count}\" uniqueCount=\"{sharedStrings.Count}\">");
        foreach (var value in sharedStrings)
        {
            strings.Append("<si><t>").Append(XmlEscape(value)).Append("</t></si>");
        }
        strings.Append("</sst>");

        const string workbookXml =
            $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="{SpreadsheetNs}" xmlns:r="{RelationshipsNs}"><sheets><sheet name="employees" sheetId="1" r:id="rId1" /></sheets></workbook>""";

        const string workbookRelsXml =
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" /><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml" /></Relationships>""";

        const string rootRelsXml =
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" /></Relationships>""";

        const string contentTypesXml =
            """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" /><Default Extension="xml" ContentType="application/xml" /><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" /><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" /><Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml" /></Types>""";

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteEntry(archive, "_rels/.rels", rootRelsXml);
            WriteEntry(archive, "xl/workbook.xml", workbookXml);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", workbookRelsXml);
            WriteEntry(archive, "xl/sharedStrings.xml", strings.ToString());
            WriteEntry(archive, "xl/worksheets/sheet1.xml", sheet.ToString());
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        var bytes = new UTF8Encoding(false).GetBytes(content);
        entryStream.Write(bytes, 0, bytes.Length);
    }

    private static string XmlEscape(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    /// <summary>تبدیل شماره‌ی ستون یک‌پایه به حرف لاتین (A، B، …، AA).</summary>
    private static string ToColumnLetter(int number)
    {
        var letter = string.Empty;
        while (number > 0)
        {
            var remainder = (number - 1) % 26;
            letter = (char)('A' + remainder) + letter;
            number = (number - remainder) / 26;
        }

        return letter;
    }

    private static MemoryStream BuildSheet(params string[][] rows) =>
        BuildXlsx([Headers, .. rows]);

    private static string[] Row(
        string code, string firstName, string lastName, string unitCode,
        string? nationalCode = null, string? fatherName = null, string? positionCode = null,
        string? status = null, string? startDate = null, string? endDate = null,
        string? workEmail = null, string? internalPhone = null, string? managerCode = null) =>
    [
        code,
        nationalCode ?? string.Empty,
        firstName,
        lastName,
        fatherName ?? string.Empty,
        unitCode,
        positionCode ?? string.Empty,
        status ?? string.Empty,
        startDate ?? string.Empty,
        endDate ?? string.Empty,
        workEmail ?? string.Empty,
        internalPhone ?? string.Empty,
        managerCode ?? string.Empty
    ];

    // --- تجزیه‌ی فایل -------------------------------------------------------

    [Fact]
    public void Parse_Maps_Persian_Headers_To_Fields()
    {
        var stream = BuildSheet(Row("E1", "نام", "خانوادگی", "IT"));

        var rows = EmployeeExcelParser.Parse(stream);

        rows.Should().HaveCount(1);
        rows[0].EmployeeCode.Should().Be("E1");
        rows[0].FirstName.Should().Be("نام");
        rows[0].LastName.Should().Be("خانوادگی");
        rows[0].OrgUnitCode.Should().Be("IT");
    }

    [Fact]
    public void Parse_Maps_English_Headers_To_Fields()
    {
        var englishHeaders = new[]
        {
            "EmployeeCode", "NationalCode", "FirstName", "LastName", "FatherName",
            "OrgUnitCode", "PositionCode", "Status", "StartDate", "EndDate",
            "WorkEmail", "InternalPhone", "ManagerCode"
        };

        var stream = BuildXlsx([englishHeaders, Row("E1", "نام", "خانوادگی", "IT")]);

        var rows = EmployeeExcelParser.Parse(stream);

        rows.Should().HaveCount(1);
        rows[0].EmployeeCode.Should().Be("E1");
        rows[0].OrgUnitCode.Should().Be("IT");
    }

    [Fact]
    public void Parse_Ignores_Empty_Trailing_Rows()
    {
        var stream = BuildXlsx(
            [
                Headers,
                Row("E1", "نام", "خانوادگی", "IT"),
                new string[13]
            ]);

        var rows = EmployeeExcelParser.Parse(stream);

        rows.Should().HaveCount(1);
    }

    [Fact]
    public void Parse_Missing_Required_Columns_Throws_File_Exception()
    {
        // سرستون «کد واحد سازمانی» حذف شده است.
        var brokenHeaders = Headers
            .Where(h => h != "کد واحد سازمانی")
            .ToArray();

        var stream = BuildXlsx([brokenHeaders, Row("E1", "نام", "خانوادگی", "IT")]);

        var act = () => EmployeeExcelParser.Parse(stream);

        var ex = act.Should().Throw<EmployeeImportException>().Which;
        ex.Code.Should().Be("import_missing_columns");
    }

    [Fact]
    public void Parse_Empty_Workbook_Throws()
    {
        var stream = BuildXlsx([Headers]);

        var act = () => EmployeeExcelParser.Parse(stream);

        act.Should().Throw<EmployeeImportException>()
            .Which.Code.Should().Be("import_no_rows");
    }

    [Fact]
    public void ParseDate_Handles_Iso_And_Persian_Calendar_Texts()
    {
        EmployeeExcelParser.ParseDate("2023-06-01").Should().Be(new DateOnly(2023, 6, 1));
        EmployeeExcelParser.ParseDate("1402/03/11").Should().Be(new DateOnly(2023, 6, 1));
        EmployeeExcelParser.ParseDate(string.Empty).Should().BeNull();
        EmployeeExcelParser.ParseDate("نا معتبر").Should().BeNull();
    }

    [Fact]
    public void ParseStatus_Defaults_To_Active()
    {
        EmployeeExcelParser.ParseStatus("فعال").Should().Be(EmployeeStatus.Active);
        EmployeeExcelParser.ParseStatus("مرخصی").Should().Be(EmployeeStatus.OnLeave);
        EmployeeExcelParser.ParseStatus("معلق").Should().Be(EmployeeStatus.Suspended);
        EmployeeExcelParser.ParseStatus("خاتمه‌یافته").Should().Be(EmployeeStatus.Terminated);
        EmployeeExcelParser.ParseStatus(string.Empty).Should().Be(EmployeeStatus.Active);
    }

    // --- سرویس: جریان ورود -------------------------------------------------

    private static async Task<(TestEnvironment env, IEmployeeService service, Guid companyId, string unitCode, string otherUnitCode)> CreateServiceAsync()
    {
        var env = await TestEnvironment.CreateAsync();
        var (companyId, divisionId, departmentId, teamId, otherDivisionId, otherDepartmentId) = await env.SeedOrgHierarchyAsync();

        env.OrganizationDbContext.Positions.AddRange(
            new Position { Code = "POS-IT-DEV", Title = "توسعه‌دهنده نرم‌افزار", OrgUnitId = otherDepartmentId },
            new Position { Code = "POS-ACC", Title = "حسابدار", OrgUnitId = departmentId });
        await env.OrganizationDbContext.SaveChangesAsync();

        // یک کارمند موجود برای آزمون کد تکراری و ارجاع به مدیر.
        env.OrganizationDbContext.Employees.Add(new Employee
        {
            EmployeeCode = "EMP-0001",
            FirstName = "مدیر",
            LastName = "سامانه",
            OrgUnitId = companyId,
            Status = EmployeeStatus.Active,
            StartDate = new DateOnly(2020, 1, 1)
        });
        await env.OrganizationDbContext.SaveChangesAsync();

        env.SetCurrentUser(companyId, DataScope.Company, permissions: ModulePermissions);

        return (env, env.Services.GetRequiredService<IEmployeeService>(), companyId, "hq", "fin");
    }

    [Fact]
    public async Task Import_Creates_Employees_From_Valid_Rows()
    {
        var (env, service, _, _, _) = await CreateServiceAsync();
        await using (env)
        {
            var stream = BuildSheet(
                Row("EMP-0100", "سارا", "موسوی", "hq", nationalCode: "1000000100", fatherName: "محمود",
                    positionCode: "POS-IT-DEV", status: "فعال", startDate: "2019-04-01",
                    workEmail: "s.mousavi@test.local", internalPhone: "1007"),
                Row("EMP-0101", "محمد", "رحیمی", "fin", nationalCode: "1000000101",
                    status: "مرخصی", startDate: "1400/05/24"));

            var result = await service.ImportAsync(stream, default);

            result.IsSuccess.Should().BeTrue();
            result.Value!.TotalRows.Should().Be(2);
            result.Value.CreatedCount.Should().Be(2);
            result.Value.SkippedCount.Should().Be(0);
            result.Value.FailedCount.Should().Be(0);

            var created = await env.OrganizationDbContext.Employees
                .Where(e => e.EmployeeCode == "EMP-0100" || e.EmployeeCode == "EMP-0101")
                .OrderBy(e => e.EmployeeCode)
                .ToListAsync();

            created.Should().HaveCount(2);
            created[0].FirstName.Should().Be("سارا");
            created[0].NationalCode.Should().Be("1000000100");
            created[0].WorkEmail.Should().Be("s.mousavi@test.local");
            created[0].Status.Should().Be(EmployeeStatus.Active);

            // تاریخ شمسی ۱۴۰۰/۰۵/۲۴ باید به میلادی تبدیل شود.
            created[1].StartDate.Should().Be(new DateOnly(2021, 8, 15));
            created[1].Status.Should().Be(EmployeeStatus.OnLeave);
        }
    }

    [Fact]
    public async Task Import_Skips_Duplicate_Codes()
    {
        var (env, service, _, _, _) = await CreateServiceAsync();
        await using (env)
        {
            var stream = BuildSheet(
                Row("EMP-0001", "تکراری", "است", "hq"),
                Row("EMP-0200", "سارا", "موسوی", "hq"),
                Row("EMP-0200", "دوباره", "تکراری", "hq"));

            var result = await service.ImportAsync(stream, default);

            result.IsSuccess.Should().BeTrue();
            result.Value!.TotalRows.Should().Be(3);
            result.Value.CreatedCount.Should().Be(1);
            result.Value.SkippedCount.Should().Be(2);
            result.Value.FailedCount.Should().Be(0);

            result.Value.Rows.Should().Contain(r => r.EmployeeCode == "EMP-0001" && r.Outcome == "skipped");
            result.Value.Rows.Should().Contain(r => r.EmployeeCode == "EMP-0200" && r.Outcome == "created");
            result.Value.Rows.Should().Contain(r => r.EmployeeCode == "EMP-0200" && r.Outcome == "skipped");

            (await env.OrganizationDbContext.Employees.CountAsync(e => e.EmployeeCode == "EMP-0001"))
                .Should().Be(1);
        }
    }

    [Fact]
    public async Task Import_Reports_Invalid_Rows_Without_Stopping()
    {
        var (env, service, _, _, _) = await CreateServiceAsync();
        await using (env)
        {
            var stream = BuildSheet(
                Row("EMP-0300", "نام", "خانوادگی", "hq", nationalCode: "کوتاه"),
                Row("EMP-0301", string.Empty, "خانوادگی", "hq"),
                Row("EMP-0302", "نام", "خانوادگی", "UNKNOWN-UNIT"),
                Row("EMP-0303", "نام", "خانوادگی", "hq", endDate: "2010-01-01", startDate: "2020-01-01"),
                Row("EMP-0304", "نام", "خانوادگی", "hq"));

            var result = await service.ImportAsync(stream, default);

            result.IsSuccess.Should().BeTrue();
            result.Value!.TotalRows.Should().Be(5);
            result.Value.CreatedCount.Should().Be(1);
            result.Value.FailedCount.Should().Be(4);

            var failed = result.Value.Rows.Where(r => r.Outcome == "failed").Select(r => r.EmployeeCode);
            failed.Should().BeEquivalentTo("EMP-0300", "EMP-0301", "EMP-0302", "EMP-0303");

            // هیچ ردیف نامعتذاری نباید ذخیره می‌شد.
            (await env.OrganizationDbContext.Employees.CountAsync(e => e.EmployeeCode == "EMP-0300"))
                .Should().Be(0);
        }
    }

    [Fact]
    public async Task Import_Resolves_Manager_Created_In_The_Same_File()
    {
        var (env, service, _, _, _) = await CreateServiceAsync();
        await using (env)
        {
            var stream = BuildSheet(
                Row("EMP-0400", "بهمن", "قاسمی", "hq"),
                Row("EMP-0401", "سمیرا", "یوسفی", "hq", managerCode: "EMP-0400"));

            var result = await service.ImportAsync(stream, default);

            result.IsSuccess.Should().BeTrue();
            result.Value!.CreatedCount.Should().Be(2);

            var subordinate = await env.OrganizationDbContext.Employees
                .FirstAsync(e => e.EmployeeCode == "EMP-0401");

            var manager = await env.OrganizationDbContext.Employees
                .FirstAsync(e => e.EmployeeCode == "EMP-0400");

            subordinate.ManagerId.Should().Be(manager.Id);
        }
    }

    [Fact]
    public async Task Import_Without_Visible_Scope_Is_Denied()
    {
        var (env, service, _, _, _) = await CreateServiceAsync();
        await using (env)
        {
            // دامنه‌ی Own داده‌ی سازمانی قابل‌مشاهده‌ای ندارد (fail-closed).
            env.SetCurrentUser(orgUnitId: null, DataScope.Own, permissions: ModulePermissions);

            var stream = BuildSheet(Row("EMP-0500", "نام", "خانوادگی", "hq"));

            var result = await service.ImportAsync(stream, default);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("access_denied");
        }
    }

    [Fact]
    public async Task Import_Rejects_Broken_Workbook()
    {
        var (env, service, _, _, _) = await CreateServiceAsync();
        await using (env)
        {
            await using var stream = new MemoryStream([0x50, 0x4B, 0x03, 0x04, 0x00, 0x00]);

            var result = await service.ImportAsync(stream, default);

            result.IsFailure.Should().BeTrue();
            result.Error.Code.Should().Be("import_parse_failed");
        }
    }
}
