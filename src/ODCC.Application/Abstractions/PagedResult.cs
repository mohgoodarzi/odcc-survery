namespace ODCC.Application.Abstractions;

/// <summary>
/// نتیجه‌ی صفحه‌بندی‌شده: آیتم‌های صفحه به‌همراه تعداد کل رکوردهای مطابق.
/// کلاینت با این دو می‌تواند صفحات را بدون اجرای پرس‌وجوی اضافی رندر کند.
/// </summary>
public sealed record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
}
