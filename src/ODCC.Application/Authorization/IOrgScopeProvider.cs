namespace ODCC.Application.Authorization;

/// <summary>
/// تعیین دامنه‌ی سازمانی کاربر جاری. زیرساخت قابل‌استفاده‌ی مجدد برای تمام ماژول‌ها.
///
/// تمام اعضای این قرارداد ناهمگام (async) هستند؛ نسخه‌های همگام وجود ندارند تا
/// از الگوی خطرناک sync-over-async (مثل <c>GetAwaiter().GetResult()</c>) که می‌تواند
/// در request context باعث بن‌بست (deadlock) شود، جلوگیری شود. فراخوان باید
/// <see cref="GetCurrentScopeAsync"/> را یک‌بار صدا بزند و سپس از متدهای خالص
/// <see cref="OrgScope"/> برای بررسی دسترسی استفاده کند.
/// </summary>
public interface IOrgScopeProvider
{
    /// <summary>
    /// دامنه‌ی کاربر جاری را به همراه مسیر لنگر او برمی‌گرداند.
    /// این متد یک پرس‌وجوی دیتابیس انجام می‌دهد؛ نتیجه باید در هر درخواست
    /// فقط یک‌بار محاسبه و سپس cache شود.
    /// </summary>
    Task<OrgScope> GetCurrentScopeAsync(CancellationToken ct = default);
}
