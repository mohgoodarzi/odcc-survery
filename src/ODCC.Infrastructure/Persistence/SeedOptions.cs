namespace ODCC.Infrastructure.Persistence;

/// <summary>
/// تنظیمات داده‌ی اولیه (bootstrap) که در زمان راه‌اندازی پایگاه داده
/// ایجاد می‌شوند: نقش مدیر کل، کاربر مدیر کل و واحد سازمانی ریشه.
///
/// <b>امنیت:</b> رمز عبور کاربر مدیر کل باید از طریق user secrets یا متغیر
/// محیطی (<c>Seed:AdminPassword</c>) تامین شود. هرگز رمز واقعی را در
/// مخزن کد یا <c>appsettings.json</c> قرار ندهید. در صورت نبودن رمز،
/// ایجاد کاربر مدیر کل به‌صورت ایمن رد می‌شود (رشد برنامه متوقف نمی‌شود).
/// </summary>
public sealed class SeedOptions
{
    public const string SectionName = "Seed";

    /// <summary>
    /// آیا داده‌ی اولیه اصلاً ایجاد شود؟
    /// در محیط‌های خصوصی/توسعه می‌توان آن را خاموش کرد.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>نام کاربری کاربر مدیر کل.</summary>
    public string AdminUserName { get; set; } = "admin";

    /// <summary>ایمیل کاربر مدیر کل.</summary>
    public string AdminEmail { get; set; } = "admin@odcc.local";

    /// <summary>
    /// رمز عبور اولیه‌ی کاربر مدیر کل.
    /// باید حداقل ۸ کاراکتر و شامل حروف بزرگ، حروف کوچک و رقم باشد.
    /// خالی بودن یعنی «تامین نشده» و کاریر مدیر کل ایجاد نمی‌شود.
    /// </summary>
    public string AdminPassword { get; set; } = string.Empty;

    /// <summary>نام کوچک کاربر مدیر کل.</summary>
    public string AdminFirstName { get; set; } = "مدیر";

    /// <summary>نام خانوادگی کاربر مدیر کل.</summary>
    public string AdminLastName { get; set; } = "سامانه";

    /// <summary>نام نقش مدیر کل (سیستمی و غیرقابل حذف).</summary>
    public string AdminRoleName { get; set; } = "Admin";

    /// <summary>نام نمایشی نقش مدیر کل.</summary>
    public string AdminRoleDisplayName { get; set; } = "مدیر سامانه";

    /// <summary>کد واحد سازمانی ریشه (شرکت).</summary>
    public string RootOrgUnitCode { get; set; } = "HQ";

    /// <summary>نام واحد سازمانی ریشه (شرکت).</summary>
    public string RootOrgUnitName { get; set; } = "سازمان مرکزی";
}
