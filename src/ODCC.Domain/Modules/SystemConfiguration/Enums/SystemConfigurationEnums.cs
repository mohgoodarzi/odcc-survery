namespace ODCC.Domain.Modules.SystemConfiguration.Enums;

/// <summary>
/// نوع مقدار یک تنظیم سامانه. نحوه‌ی تفسیر و اعتبارسنجی مقدار رشته‌ای را تعیین می‌کند.
/// </summary>
public enum SettingValueType
{
    /// <summary>رشته‌ی آزاد.</summary>
    Text = 0,

    /// <summary>عدد صحیح.</summary>
    WholeNumber = 1,

    /// <summary>عدد اعشاری.</summary>
    FractionalNumber = 2,

    /// <summary>درست/نادرست.</summary>
    TrueFalse = 3,

    /// <summary>تاریخ ( Jalali یا Miladi به‌صورت ISO).</summary>
    Date = 4,

    /// <summary>آدرس ایمیل.</summary>
    Email = 5,

    /// <summary>URL.</summary>
    Url = 6,

    /// <summary>مدت زمان (مثلاً «00:30:00»).</summary>
    Duration = 7,

    /// <summary>شیء JSON ساختاریافته.</summary>
    Json = 8
}

/// <summary>
/// دامنه‌ی اعمال یک تنظیم یا پرچم ویژگی.
/// </summary>
public enum ConfigurationScope
{
    /// <summary>تمام سامانه (تنها مدیر سامانه قابل تغییر است).</summary>
    System = 0,

    /// <summary>مخصوص یک سازمان (در آینده قابل توسعه).</summary>
    Organization = 1
}

/// <summary>
/// چرخه‌ی عمر یک پرچم ویژگی (Feature Flag).
/// </summary>
public enum FeatureFlagState
{
    /// <summary>غیرفعال برای همه.</summary>
    Off = 0,

    /// <summary>فعال برای همه.</summary>
    On = 1,

    /// <summary>فقط برای درصدی از کاربران (رخداد تصادفی).</summary>
    Percentage = 2,

    /// <summary>فقط برای کاربران/نقش‌های فهرست‌شده.</summary>
    AllowList = 3
}

/// <summary>
/// نوع سیاست سیستمی قابل پیکربندی.
/// </summary>
public enum SystemPolicyType
{
    /// <summary>سیاست‌های رمز عبور (طول حداقل، پیچیدگی، انقضا).</summary>
    Password = 0,

    /// <summary>سیاست‌های نشست (طول عمر، تعداد نشست‌های همزمان).</summary>
    Session = 1,

    /// <summary>سیاست‌های حریم خصوصی پاسخ‌های ناشناس.</summary>
    ResponsePrivacy = 2,

    /// <summary>سیاست‌های نگهداری و بایگانی داده.</summary>
    DataRetention = 3,

    /// <summary>سیاست‌های ایمنی ورود (قفل شدن حساب، محدودیت تلاش).</summary>
    LoginSecurity = 4,

    /// <summary>سیاست عمومی/سفارشی.</summary>
    Custom = 99
}
