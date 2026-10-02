<#
.SYNOPSIS
  داده‌ی نمونه‌ی ماژول پیکربندی سامانه (تنظیمات، پرچم‌های ویژگی، سیاست‌های سیستمی).

.DESCRIPTION
  این اسکریپت داده‌های نمونه‌ی واقعی برای چهار بخش پیکربندی سامانه از طریق
  API های موجود ایجاد می‌کند:

    * تنظیمات سامانه (System Settings)  — /api/{culture}/system/settings
    * تنظیمات (Settings)                 — همان صفحه با مسیر /settings
    * پرچم‌های ویژگی (Feature Flags)     — /api/{culture}/system/feature-flags
    * سیاست‌های سیستمی (System Policies) — /api/{culture}/system/policies

  توجه: مسیرهای «تنظیمات سامانه» و «تنظیمات» هر دو همان SystemSettingsPage را
  نمایش می‌دهند و از همان جدول system_settings می‌خوانند.

  هیچ مدل/جدول جدیدی ساخته نمی‌شود. اعتبارسنجی‌های FluentValidation و قوانین
  دامنه از طریق همان مسیرهای API اعمال می‌شوند که فرانت‌اند استفاده می‌کند.

  اسکریپت idempotent است: کلیدهایی که از قبل وجود دارند (409) را رد می‌کند.

.PARAMETER BaseUrl
  آدرس پایه‌ی API. پیش‌فرض: https://localhost:7208

.PARAMETER Culture
  فرهنگ مسیر API. پیش‌فرض: fa

.EXAMPLE
  .\seed\seed-system-configuration.ps1
#>

[CmdletBinding()]
param(
    [string]$BaseUrl = 'https://localhost:7208',
    [string]$Culture = 'fa',
    [string]$UserName = 'admin',
    [string]$Password = 'DevAdmin2026!StrongPass'
)

$ErrorActionPreference = 'Stop'

# --- چندجمله‌ای کمک‌ ----------------------------------------------------------------

function New-ApiSession {
    param([string]$BaseUrl, [string]$Culture, [string]$UserName, [string]$Password)

    $body = @{ userName = $UserName; password = $Password } | ConvertTo-Json -Compress

    $response = Invoke-RestMethod `
        -Uri "$BaseUrl/api/$Culture/auth/login" `
        -Method Post `
        -Body $body `
        -ContentType 'application/json' `
        -SkipCertificateCheck `
        -TimeoutSec 30

    return @{
        BaseUrl = $BaseUrl
        Culture = $Culture
        Token   = $response.tokens.accessToken
    }
}

function Invoke-Api {
    param(
        $Session,
        [string]$Path,
        [string]$Method = 'Get',
        $Body
    )

    $headers = @{ Authorization = "Bearer $($Session.Token)" }
    $params = @{
        Uri     = "$($Session.BaseUrl)/api/$($Session.Culture)$Path"
        Method  = $Method
        Headers = $headers
        TimeoutSec = 30
        SkipCertificateCheck = $true
    }

    if ($null -ne $Body) {
        $params.Body = ($Body | ConvertTo-Json -Compress -Depth 10)
        $params.ContentType = 'application/json'
    }

    return Invoke-RestMethod @params
}

# --- احراز هویت -------------------------------------------------------------------

$session = New-ApiSession -BaseUrl $BaseUrl -Culture $Culture -UserName $UserName -Password $Password
Write-Host "ورود موفق بود. توکن دریافت شد." -ForegroundColor Green

$created = @{ settings = 0; flags = 0; policies = 0 }
$skipped = @{ settings = 0; flags = 0; policies = 0 }
$failed  = @{ settings = 0; flags = 0; policies = 0 }

function Submit-Record {
    param(
        $Session,
        [string]$Path,
        $Payload,
        [string]$Label,
        [string]$Bucket
    )

    try {
        $result = Invoke-Api -Session $Session -Path $Path -Method Post -Body $Payload
        Write-Host "  + $Label" -ForegroundColor Green
        $script:created[$Bucket]++
        return $result
    }
    catch [System.Net.WebException] {
        $status = [int]$_.Exception.Response.StatusCode
        if ($status -eq 409) {
            Write-Host "  ~ $Label (از قبل وجود دارد — رد شد)" -ForegroundColor DarkYellow
            $script:skipped[$Bucket]++
        }
        else {
            Write-Host "  x $Label (HTTP $status)" -ForegroundColor Red
            $script:failed[$Bucket]++
        }
        return $null
    }
    catch {
        Write-Host "  x $Label ($($_.Exception.Message))" -ForegroundColor Red
        $script:failed[$Bucket]++
        return $null
    }
}

# === ۱. تنظیمات سامانه / تنظیمات ===================================================
# ValueType: Text=0 WholeNumber=1 FractionalNumber=2 TrueFalse=3 Date=4 Email=5 Url=6 Duration=7 Json=8
# هر دو مسیر /system/settings و /settings همان جدول system_settings را می‌خوانند.
Write-Host "`n[۱] تنظیمات سامانه (system_settings)" -ForegroundColor Cyan

$settings = @(
    @{ Key = 'analytics.min_responses';          Name = 'حداقل پاسخ برای تحلیل قابل‌اتکا'; Description = 'کمینه‌ی تعداد پاسخ لازم تا شاخص‌ها نمایش داده شوند'; ValueType = 1; Value = '30';  DefaultValue = '10'; Group = 'analytics';   IsSensitive = $false }
    @{ Key = 'analytics.confidence_level';       Name = 'سطح اطمینان آماری';               Description = 'سطح اطمینان محاسبه‌ی بازه‌ی خطا در داشبورد';         ValueType = 2; Value = '0.95'; DefaultValue = '0.9'; Group = 'analytics'; IsSensitive = $false }
    @{ Key = 'survey.default_anonymous';         Name = 'ناشناس بودن پیش‌فرض نظرسنجی';     Description = 'آیا نظرسنجی‌های جدید به‌صورت پیش‌فرض ناشناس ساخته می‌شوند'; ValueType = 3; Value = 'true'; DefaultValue = 'false'; Group = 'survey'; IsSensitive = $false }
    @{ Key = 'survey.max_questions_per_section'; Name = 'حداکثر سؤال در هر بخش';           Description = 'سقف تعداد سؤال برای جلوگیری از خستگی پاسخ‌دهنده';     ValueType = 1; Value = '25';  DefaultValue = '20'; Group = 'survey';  IsSensitive = $false }
    @{ Key = 'notification.email_from';          Name = 'آدرس ایمیل فرستنده';              Description = 'ایمیل ارسال‌کننده‌ی دعوت‌نامه‌ها و یادآوری‌ها';       ValueType = 5; Value = 'noreply@odcc.local'; DefaultValue = 'noreply@odcc.local'; Group = 'notification'; IsSensitive = $false }
    @{ Key = 'notification.reminder_interval';   Name = 'فاصله‌ی زمانی یادآوری';            Description = 'مدت زمان بین یادآوری‌های پاسخ به نظرسنجی';             ValueType = 7; Value = '24:00:00'; DefaultValue = '24:00:00'; Group = 'notification'; IsSensitive = $false }
    @{ Key = 'branding.portal_url';              Name = 'آدرس پورتال نظرسنجی';             Description = 'نشانی عمومی پورتال که در ایمیل‌ها قرار می‌گیرد';       ValueType = 6; Value = 'https://survey.odcc.local'; DefaultValue = 'https://survey.odcc.local'; Group = 'branding'; IsSensitive = $false }
    @{ Key = 'branding.company_name';            Name = 'نام سازمان در گزارش‌ها';          Description = 'نمای نمایشی سازمان در سرستار و خروجی‌ها';             ValueType = 0; Value = 'شرکت توسعه داده‌های سازمانی'; DefaultValue = 'ODCC'; Group = 'branding'; IsSensitive = $false }
    @{ Key = 'reporting.retention_days';         Name = 'دوره‌ی نگهداری گزارش‌ها';          Description = 'تعداد روز نگهداری فایل‌های گزارش تولیدشده';            ValueType = 1; Value = '365'; DefaultValue = '180'; Group = 'reporting'; IsSensitive = $false }
    @{ Key = 'security.audit_encryption_key';    Name = 'کلید رمزنگاری لاگ ممیزی';         Description = 'مقدار حساس — هرگز از API برگردانده نمی‌شود';            ValueType = 0; Value = 'S3CR3T-DEV-KEY-DO-NOT-USE-IN-PROD'; DefaultValue = ''; Group = 'security'; IsSensitive = $true }
    @{ Key = 'dashboard.widgets_config';         Name = 'پیکربندی ابزارک‌های داشبورد';      Description = 'چیدمان و بازخوانی ابزارک‌ها به‌صورت JSON';              ValueType = 8; Value = '{"layout":"grid","refreshSeconds":60,"widgets":["nps","csat","responseRate"]}'; DefaultValue = '{}'; Group = 'dashboard'; IsSensitive = $false }
)

foreach ($s in $settings) {
    Submit-Record -Session $session -Path '/system/settings' -Payload $s `
        -Label "تنظیم: $($s.Key)" -Bucket 'settings' | Out-Null
}

# === ۲. پرچم‌های ویژگی ==============================================================
# State: Off=0 On=1 Percentage=2 AllowList=3
Write-Host "`n[۲] پرچم‌های ویژگی (system_feature_flags)" -ForegroundColor Cyan

$adminUserId = 'f1c472ee-c4aa-4eb2-0cd1-08df1a521574'   # کاربر admin
$flags = @(
    @{ Key = 'new-analytics-dashboard'; Name = 'داشبورد جدید تحلیل ها';   Description = 'رابط کاربری بازنویسی‌شده‌ی داشبورد تحلیل ها'; State = 1; Percentage = $null; AllowedUserIds = @(); AllowedRoles = @(); ExpiresAt = $null }
    @{ Key = 'ai-question-generator';   Name = 'تولید هوشمند پرسشنامه'; Description = 'پیشنهاد خودکار سؤال با کمک هوش مصنوعی';     State = 0; Percentage = $null; AllowedUserIds = @(); AllowedRoles = @(); ExpiresAt = $null }
    @{ Key = 'beta-pdf-export';         Name = 'خروجی PDF نسخهٔ بتا';    Description = 'موتور جدید خروجی PDF — فقط برای درصدی از کاربران'; State = 2; Percentage = 25; AllowedUserIds = @(); AllowedRoles = @(); ExpiresAt = $null }
    @{ Key = 'advanced-segmentation';   Name = 'بخش‌بندی پیشرفته نتایج'; Description = 'فیلتر نتایج بر اساس واحد سازمانی و موقعیت';  State = 3; Percentage = $null; AllowedUserIds = @($adminUserId); AllowedRoles = @('Admin', 'Analyst'); ExpiresAt = $null }
    @{ Key = 'realtime-notifications';  Name = 'اعلان‌های بلادرنگ';       Description = 'تحویل آنی اعلان‌ها از طریق SignalR (آزمایشی)'; State = 1; Percentage = $null; AllowedUserIds = @(); AllowedRoles = @(); ExpiresAt = '2027-03-31T23:59:59Z' }
    @{ Key = 'legacy-report-viewer';    Name = 'نمایشگر قدیمی گزارش‌ها';  Description = 'نسخه‌ی قدیمی نمایشگر گزارش برای سازگاری';     State = 0; Percentage = $null; AllowedUserIds = @(); AllowedRoles = @(); ExpiresAt = $null }
)

foreach ($f in $flags) {
    Submit-Record -Session $session -Path '/system/feature-flags' -Payload $f `
        -Label "پرچم: $($f.Key)" -Bucket 'flags' | Out-Null
}

# === ۳. سیاست‌های سیستمی ============================================================
# Type: Password=0 Session=1 ResponsePrivacy=2 DataRetention=3 LoginSecurity=4 Custom=99
Write-Host "`n[۳] سیاست‌های سیستمی (system_policies)" -ForegroundColor Cyan

$policies = @(
    @{ Type = 0;  Key = 'password.min_length';         Name = 'حداقل طول رمز عبور';       Description = 'کمینه‌ی تعداد کاراکتر رمز عبور';            Value = '10';  DefaultValue = '8';   IsEnabled = $true }
    @{ Type = 0;  Key = 'password.require_complexity'; Name = 'الزام پیچیدگی رمز عبور';   Description = 'وجود حرف بزرگ، کوچک و عدد';                  Value = 'true'; DefaultValue = 'true'; IsEnabled = $true }
    @{ Type = 0;  Key = 'password.expiry_days';        Name = 'انقضای رمز عبور';          Description = 'تعداد روز تا الزام تغییر رمز عبور';          Value = '90';  DefaultValue = '180'; IsEnabled = $true }
    @{ Type = 1;  Key = 'session.timeout_minutes';     Name = 'مهلت نشست کاربری';         Description = 'دقیقه‌های بی‌فعالیتی تا نشست منقضی شود';      Value = '30';  DefaultValue = '60';  IsEnabled = $true }
    @{ Type = 1;  Key = 'session.max_concurrent';      Name = 'حداکثر نشست همزمان';       Description = 'سقف نشست‌های همزمان هر کاربر';               Value = '3';   DefaultValue = '2';   IsEnabled = $true }
    @{ Type = 4;  Key = 'login.max_failed_attempts';   Name = 'حداکثر تلاش ناموفق ورود';  Description = 'تعداد تلاش ناموفق تا قفل شدن حساب';          Value = '5';   DefaultValue = '5';   IsEnabled = $true }
    @{ Type = 4;  Key = 'login.lockout_minutes';       Name = 'مدت قفل شدن حساب';         Description = 'دقیقه‌های قفل بودن حساب پس از تلاش ناموفق';  Value = '15';  DefaultValue = '30';  IsEnabled = $true }
    @{ Type = 2;  Key = 'response.anonymize_threshold'; Name = 'آستانه ناشناس‌سازی پاسخ'; Description = 'کمینه‌ی پاسخ هر بخش تا گزارش ناشناس شود';   Value = '5';   DefaultValue = '10';  IsEnabled = $true }
    @{ Type = 3;  Key = 'retention.responses_days';    Name = 'نگهداری پاسخ‌های نظرسنجی'; Description = 'تعداد روز نگهداری پاسخ‌ها پیش از بایگانی';   Value = '730'; DefaultValue = '365'; IsEnabled = $true }
    @{ Type = 99; Key = 'custom.welcome_message';      Name = 'پیام خوش‌آمد پورتال';      Description = 'پیام نمایش‌داده‌شده در صفحه ورود (غیرفعال)';  Value = 'به سامانه نظرسنجی سازمانی خوش آمدید'; DefaultValue = 'خوش آمدید'; IsEnabled = $false }
)

foreach ($p in $policies) {
    Submit-Record -Session $session -Path '/system/policies' -Payload $p `
        -Label "سیاست: [$($p.Type)] $($p.Key)" -Bucket 'policies' | Out-Null
}

# === خلاصه ========================================================================

Write-Host "`nخلاصه‌ی داده‌ی نمونه:" -ForegroundColor Cyan
Write-Host ("  تنظیمات:     {0} ساخته شد / {1} رد شد / {2} ناموفق" -f $created.settings, $skipped.settings, $failed.settings)
Write-Host ("  پرچم‌ها:      {0} ساخته شد / {1} رد شد / {2} ناموفق" -f $created.flags, $skipped.flags, $failed.flags)
Write-Host ("  سیاست‌ها:     {0} ساخته شد / {1} رد شد / {2} ناموفق" -f $created.policies, $skipped.policies, $failed.policies)

# --- آمار نهایی از API --------------------------------------------------------------
Write-Host "`nآمار پیکربندی سامانه (از API):" -ForegroundColor Cyan
try {
    $stats = Invoke-Api -Session $session -Path '/system/configuration/stats'
    $stats | ConvertTo-Json
}
catch {
    Write-Warning "دریافت آمار ناموفق بود: $($_.Exception.Message)"
}

if (($failed.settings + $failed.flags + $failed.policies) -gt 0) {
    Write-Host "`nبرخی رکوردها ناموفق بودند." -ForegroundColor Red
    exit 1
}

Write-Host "`nداده‌ی نمونه با موفقیت ایجاد شد." -ForegroundColor Green
