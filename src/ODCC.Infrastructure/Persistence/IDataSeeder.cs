namespace ODCC.Infrastructure.Persistence;

/// <summary>
/// ایجاد داده‌ی اولیه‌ی سامانه (bootstrap).
///
/// این قرارداد نقش‌های سیستمی، کاربر مدیر کل و واحد سازمانی ریشه را
/// به‌صورت <b>خودتوان (idempotent)</b> ایجاد می‌کند. فراخوانی چندباره‌ی آن
/// امن است: داده‌ی از قبل موجود تغییر نمی‌کند.
///
/// توجه: این قرارداد فقط در زمان راه‌اندازی پایگاه داده (زیر نظر
/// <c>OdccDbInitializerHostedService</c> و با تأیید صریح <c>Database:AutoMigrate</c>)
/// اجرا می‌شود. اجرای آن روی پایگاه‌داده‌ی production نیازمند تأیید است.
/// </summary>
public interface IDataSeeder
{
    /// <summary>
    /// ایجاد داده‌ی اولیه در صورت نبودن.
    /// </summary>
    Task SeedAsync(CancellationToken ct = default);
}
