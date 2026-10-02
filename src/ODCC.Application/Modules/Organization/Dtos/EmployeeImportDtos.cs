namespace ODCC.Application.Modules.Organization.Dtos;

/// <summary>
/// نتیجه‌ی پردازش یک ردیف فایل اکسل.
/// </summary>
public sealed record EmployeeImportRowResultDto
{
    /// <summary>شماره‌ی ردیف در فایل اکسل (شامل ردیف عنوان‌ها).</summary>
    public int RowNumber { get; init; }

    /// <summary>کد پرسنلی ردیف (در صورت استخراج).</summary>
    public string EmployeeCode { get; init; } = string.Empty;

    /// <summary>نام و نام خانوادگی ردیف (در صورت استخراج).</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>
    /// نتیجه‌ی ردیف: «created» (ایجاد شد)، «skipped» (نادیده گرفته شد:
    /// کد پرسنلی تکراری) یا «failed» (خطای اعتبارسنجی).
    /// </summary>
    public string Outcome { get; set; } = string.Empty;

    /// <summary>پیام توضیحی (علت نادیده‌گرفتن یا رد شدن).</summary>
    public string? Message { get; set; }
}

/// <summary>
/// خلاصه‌ی ورود اطلاعات کارمندان از فایل اکسل.
/// </summary>
public sealed record EmployeeImportResultDto
{
    /// <summary>کل ردیف‌های داده موجود در فایل.</summary>
    public int TotalRows { get; init; }

    /// <summary>تعداد کارمندانی که با موفقیت ایجاد شدند.</summary>
    public int CreatedCount { get; init; }

    /// <summary>تعداد ردیف‌هایی که به دلیل تکراری بودن کد پرسنلی نادیده گرفته شدند.</summary>
    public int SkippedCount { get; init; }

    /// <summary>تعداد ردیف‌هایی که به دلیل خطای اعتبارسنجی رد شدند.</summary>
    public int FailedCount { get; init; }

    /// <summary>نتیجه‌ی دقیق هر ردیف (ایجاد/نادیده/رد).</summary>
    public IReadOnlyList<EmployeeImportRowResultDto> Rows { get; init; } = [];
}
