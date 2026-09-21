namespace ODCC.Domain.Common;

/// <summary>
/// زبان‌های پشتیبانی‌شده در سامانه.
/// این مقدار به‌صورت <c>int</c> در پایگاه داده ذخیره می‌شود تا افزودن زبان جدید
/// در آینده فقط یک تغییر داده‌ای باشد و ردیف‌های موجود نامعتبر نشوند.
/// </summary>
public enum Language
{
    /// <summary>فارسی</summary>
    Fa = 1,

    /// <summary>انگلیسی</summary>
    En = 2
}
