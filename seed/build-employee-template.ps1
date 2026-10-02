#Requires -Version 7.0
<#
  ساخت فایل اکسل نمونه‌ی کارمندان (seed/employees-sample.xlsx).

  فایل xlsx یک آرشیو ZIP از بخش‌های XML است؛ این اسکریپت آن بخش‌ها را
  دستی می‌سازد تا نیازی به وابستگی خارجی (ClosedXML و امثال آن) نباشد.
  داده‌ی این فایل دقیقاً همان ۲۰ ردیفی است که در seed/seed-part1.sql درج
  می‌شود، تا مسیر «اکسل ← API ← پایگاه داده» قابل بازتولید باشد.

  همه‌ی سلول‌ها به‌صورت رشته‌ی مشترک (shared string) نوشته می‌شوند؛ تاریخ‌ها
  هم ISO میلادی هستند که EmployeeExcelParser.ParseDate می‌فهمد.
#>

param(
    [string]$OutPath = "$PSScriptRoot\employees-sample.xlsx"
)

$ErrorActionPreference = 'Stop'

$columns = @(
    'کد پرسنلی', 'کد ملی', 'نام', 'نام خانوادگی', 'نام پدر',
    'کد واحد سازمانی', 'کد موقعیت شغلی', 'وضعیت', 'تاریخ شروع', 'تاریخ پایان',
    'ایمیل سازمانی', 'تلفن داخلی', 'کد پرسنلی مدیر'
)

# code, nationalCode, firstName, lastName, fatherName, unit, position,
# status, startDate, endDate, workEmail, internalPhone, managerCode
$rows = @(
    @('EMP-0007','1000000007','سارا','موسوی','محمود','IT','POS-IT-DEV','فعال','2019-04-01','','s.mousavi@odcc.local','1007','EMP-0002'),
    @('EMP-0008','1000000008','محمد','رحیمی','حسن','IT','POS-IT-DEV','فعال','2020-08-15','','m.rahimi@odcc.local','1008','EMP-0002'),
    @('EMP-0009','1000000009','فاطمه','نجفی','علی','IT','POS-IT-NET','فعال','2021-02-01','','f.najafi@odcc.local','1009','EMP-0002'),
    @('EMP-0010','1000000010','امیر','کاظمی','رضا','IT','POS-IT-NET','مرخصی','2018-11-20','','a.kazemi@odcc.local','1010','EMP-0002'),
    @('EMP-0011','1000000011','نگار','شیرازی','مهدی','IT','POS-IT-DEV','فعال','2023-06-01','','n.shirazi@odcc.local','1011','EMP-0002'),
    @('EMP-0024','1000000024','بابک','سلطانی','همایون','IT','POS-IT-DEV','فعال','2023-11-01','','b.soltani@odcc.local','1024','EMP-0002'),
    @('EMP-0012','1000000012','زینب','ابراهیمی','کریم','HR','POS-HR-PAY','فعال','2019-10-10','','z.ebrahimi@odcc.local','1012','EMP-0003'),
    @('EMP-0013','1000000013','مهدی','صادقی','جواد','HR','POS-HR-SPC','فعال','2022-03-15','','m.sadeghi@odcc.local','1013','EMP-0003'),
    @('EMP-0014','1000000014','الهام','رستمی','فرهاد','HR','POS-HR-PAY','معلق','2020-12-01','','e.rostami@odcc.local','1014','EMP-0003'),
    @('EMP-0025','1000000025','رویا','احمدزاده','پرویز','HR','POS-HR-SPC','فعال','2024-01-15','','r.ahmadzadeh@odcc.local','1025','EMP-0003'),
    @('EMP-0015','1000000015','بهمن','قاسمی','نادر','SAL','POS-SAL-MGR','فعال','2017-05-01','','b.ghasemi@odcc.local','1015','EMP-0001'),
    @('EMP-0016','1000000016','سمیرا','یوسفی','اصغر','SAL','POS-SAL-REP','فعال','2021-09-01','','s.yousefi@odcc.local','1016','EMP-0015'),
    @('EMP-0017','1000000017','کاوه','عباسی','منصور','SAL','POS-SAL-REP','فعال','2022-01-10','','k.abbasi@odcc.local','1017','EMP-0015'),
    @('EMP-0018','1000000018','مریم','علوی','سعید','SAL','POS-SAL-REP','خاتمه‌یافته','2019-04-15','2023-08-31','m.alavi@odcc.local','1018','EMP-0015'),
    @('EMP-0019','1000000019','آرش','مقدم','یدالله','SAL','POS-SAL-REP','فعال','2023-02-20','','a.moghadam@odcc.local','1019','EMP-0015'),
    @('EMP-0020','1000000020','هما','جعفری','میرزا','OPS','POS-OPS-SPC','فعال','2020-05-12','','h.jafari@odcc.local','1020','EMP-0006'),
    @('EMP-0021','1000000021','کامران','فرهادی','بهمن','OPS','POS-OPS-LOG','فعال','2021-07-05','','k.farhadi@odcc.local','1021','EMP-0006'),
    @('EMP-0022','1000000022','لیلا','حسن‌زاده','علی‌اکبر','OPS','POS-OPS-LOG','فعال','2022-10-01','','l.hassanzadeh@odcc.local','1022','EMP-0006'),
    @('EMP-0023','1000000023','پرویز','نوری','غلام','OPS','POS-OPS-SPC','مرخصی','2019-01-18','','p.nouri@odcc.local','1023','EMP-0006'),
    @('EMP-0026','1000000026','نیما','گلی','فرید','OPS','POS-OPS-LOG','فعال','2024-03-01','','n.goli@odcc.local','1026','EMP-0006')
)

if ($rows.Count -ne 20) {
    throw "Expected 20 sample rows, found $($rows.Count)."
}

function ConvertTo-XmlText([string]$value) {
    return $value.Replace('&', '&amp;').Replace('<', '&lt;').Replace('>', '&gt;')
}

# --- ساخت جدول رشته‌های مشترک ---------------------------------------------
$sharedStrings = [System.Collections.Generic.List[string]]::new()
$indexByValue = @{}

function Get-SharedStringIndex([string]$value) {
    if (-not $indexByValue.ContainsKey($value)) {
        $indexByValue[$value] = $sharedStrings.Count
        $sharedStrings.Add($value) | Out-Null
    }
    return $indexByValue[$value]
}

function Convert-ColumnNumberToLetter([int]$number) {
    $letter = ''
    while ($number -gt 0) {
        $remainder = ($number - 1) % 26
        $letter = [char]($remainder + 65) + $letter
        $number = [int](($number - $remainder) / 26)
    }
    return $letter
}

# --- ساخت کاربرگ -----------------------------------------------------------
# توجه: از «+» آرایه‌ای پرهیز می‌کنیم چون PowerShell آرایه‌های تو در تو را
# تخت می‌کند و هر ردیف به یک رشته‌ی تبدیل می‌شود.
$allRows = [System.Collections.Generic.List[string[]]]::new()
$allRows.Add([string[]]$columns)
foreach ($sampleRow in $rows) {
    $allRows.Add([string[]]$sampleRow)
}

$sheetBuilder = [System.Text.StringBuilder]::new()
[void]$sheetBuilder.Append('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
[void]$sheetBuilder.Append('<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">')
[void]$sheetBuilder.Append('<sheetData>')

for ($rowIndex = 0; $rowIndex -lt $allRows.Count; $rowIndex++) {
    $rowNumber = $rowIndex + 1
    [void]$sheetBuilder.Append("<row r=`"$rowNumber`">")

    $values = $allRows[$rowIndex]
    for ($colIndex = 0; $colIndex -lt $values.Length; $colIndex++) {
        $reference = "$(Convert-ColumnNumberToLetter ($colIndex + 1))$rowNumber"
        $sharedIndex = Get-SharedStringIndex $values[$colIndex]
        [void]$sheetBuilder.Append("<c r=`"$reference`" t=`"s`"><v>$sharedIndex</v></c>")
    }

    [void]$sheetBuilder.Append('</row>')
}

[void]$sheetBuilder.Append('</sheetData></worksheet>')
$sheetXml = $sheetBuilder.ToString()

# --- ساخت sharedStrings.xml -------------------------------------------------
$stringsBuilder = [System.Text.StringBuilder]::new()
[void]$stringsBuilder.Append('<?xml version="1.0" encoding="UTF-8" standalone="yes"?>')
[void]$stringsBuilder.Append("<sst xmlns=`"http://schemas.openxmlformats.org/spreadsheetml/2006/main`" count=`"$($sharedStrings.Count)`" uniqueCount=`"$($sharedStrings.Count)`">")
foreach ($value in $sharedStrings) {
    [void]$stringsBuilder.Append("<si><t>$(ConvertTo-XmlText $value)</t></si>")
}
[void]$stringsBuilder.Append('</sst>')
$sharedStringsXml = $stringsBuilder.ToString()

$workbookXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="کارمندان" sheetId="1" r:id="rId1" />
  </sheets>
</workbook>
'@

$workbookRelsXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml" />
</Relationships>
'@

$rootRelsXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml" />
</Relationships>
'@

$contentTypesXml = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
  <Default Extension="xml" ContentType="application/xml" />
  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" />
  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
  <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml" />
</Types>
'@

# --- نوشتن آرشیو ZIP --------------------------------------------------------
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

if (Test-Path -LiteralPath $OutPath) {
    Remove-Item -LiteralPath $OutPath -Force
}

$parts = [ordered]@{
    '[Content_Types].xml'      = $contentTypesXml
    '_rels/.rels'              = $rootRelsXml
    'xl/workbook.xml'          = $workbookXml
    'xl/_rels/workbook.xml.rels' = $workbookRelsXml
    'xl/sharedStrings.xml'     = $sharedStringsXml
    'xl/worksheets/sheet1.xml' = $sheetXml
}

$stream = [System.IO.File]::Create($OutPath)
try {
    $archive = [System.IO.Compression.ZipArchive]::new(
        $stream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        foreach ($part in $parts.GetEnumerator()) {
            $entry = $archive.CreateEntry($part.Key, [System.IO.Compression.CompressionLevel]::Optimal)
            $entryStream = $entry.Open()
            try {
                $bytes = $utf8NoBom.GetBytes($part.Value)
                $entryStream.Write($bytes, 0, $bytes.Length)
            }
            finally {
                $entryStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}
finally {
    $stream.Dispose()
}

Write-Host "Wrote $OutPath ($($rows.Count) employee rows, $($sharedStrings.Count) shared strings)."
