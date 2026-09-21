namespace ODCC.Domain.Common;

/// <summary>
/// کلاس پایه‌ی هر ردیف ترجمه.
/// هر موجودیتِ قابل‌ترجمه یک جدول فرزند <c>*Localization</c> دارد که با <see cref="Language"/> کلید می‌خورد.
/// ستون‌های ترجمه هرگز روی جدول اصلی قرار نمی‌گیرند، بنابراین افزودن زبان جدید
/// فقط افزودن ردیف است و نیازی به مهاجرت طرح پایگاه داده ندارد.
/// </summary>
public abstract class Localization
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Language Language { get; set; }
}
