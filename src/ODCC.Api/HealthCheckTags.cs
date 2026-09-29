namespace ODCC.Api;

/// <summary>
/// برچسب‌های بررسی سلامت برای تفکیک پروب زنده‌بودن (liveness) و آماده‌بودن (readiness).
///
/// این فیلدها <c>static readonly</c> هستند تا تحلیل‌گر CA1861 (آرایه‌ی ثابت به‌عنوان
/// آرگومان متد) فعال نشود — الگوی یکسان با مهاجرت‌های EF Core پروژه.
/// </summary>
public static class HealthCheckTags
{
    /// <summary>
    /// برچسب پروب زنده‌بودن: فقط اجرای خود برنامه (بدون وابستگی خارجی).
    /// توسط <c>/health/live</c> استفاده می‌شود.
    /// </summary>
    public static readonly string[] Live = { "live" };

    /// <summary>
    /// برچسب پروب آماده‌بودن: وابستگی‌های واقعی (پایگاه داده‌ی هر ماژول).
    /// توسط <c>/health/ready</c> و <c>/health</c> استفاده می‌شود.
    /// </summary>
    public static readonly string[] Ready = { "ready" };
}
