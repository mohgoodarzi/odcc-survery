using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Application.Modules.Campaign.Abstractions;
using ODCC.Application.Modules.Campaign.Dtos;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Application.Modules.Notification.Dtos;
using ODCC.Application.Modules.QuestionBank.Abstractions;
using ODCC.Application.Modules.QuestionBank.Dtos;
using ODCC.Application.Modules.Questionnaire.Abstractions;
using ODCC.Application.Modules.Questionnaire.Dtos;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Application.Modules.Survey.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Enums;
using ODCC.Domain.Modules.Campaign.Events;
using ODCC.Domain.Modules.Notification.Entities;
using ODCC.Domain.Modules.Notification.Enums;
using ODCC.Domain.Modules.QuestionBank.Enums;
using ODCC.Infrastructure.Modules.Notification.Persistence;
using Xunit;
using NotificationEntity = ODCC.Domain.Modules.Notification.Entities.Notification;

namespace ODCC.Infrastructure.Tests.Modules.Notification;

/// <summary>
/// آزمون‌های ماژول اعلان‌ها: ارسال و تحویل چندکاناله، رندر قالب، ترجیحات
/// کاربر (انصراف)، صندوق ورودی و مرز حریم خصوصی (کاربر فقط اعلان‌های خودش)،
/// چرخه‌ی عمر امتحان مجدد، ممیزی و یکپارچگی با ماژول کمپین (دعوت‌نامه/یادآور).
///
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ
/// پایگاه‌داده‌ی واقعی یا سرویس بیرونی (SMTP/پیامک) را لمس نمی‌کنند.
/// </summary>
public class NotificationServiceTests
{
    // ستون‌های اندیس‌های چندستونی به‌صورت فیلدهای static readonly تعریف
    // می‌شوند تا تحلیل‌گر CA1861 (آرایه‌ی ثابت به‌عنوان آرگومان) فعال نشود.
    private static readonly string[] AuditedActions = ["send", "deliver", "read"];

    // --- ارسال و تحویل -----------------------------------------------------------

    [Fact]
    public async Task Send_InApp_Is_Delivered_Immediately()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.SendAsync(new SendNotificationRequest
        {
            TemplateCode = "system_notification",
            Channel = NotificationChannel.InApp,
            Recipient = new NotificationRecipientDto { UserId = userId, Name = "کاربر آزمون" },
            Properties = new Dictionary<string, string?>
            {
                ["subject"] = "اعلان سیستم",
                ["body"] = "بدنه‌ی اعلان"
            }
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(NotificationStatus.Delivered);
        result.Value.IsUnread.Should().BeTrue();

        var row = await env.NotificationDbContext.Notifications.SingleAsync();
        row.Status.Should().Be(NotificationStatus.Delivered);
        row.RecipientUserId.Should().Be(userId);
        row.Subject.Should().Be("اعلان سیستم");
        row.DeliveredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Send_Renders_Default_Template_And_Replaces_Variables()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.SendAsync(new SendNotificationRequest
        {
            TemplateCode = "campaign_invitation",
            Channel = NotificationChannel.InApp,
            Category = NotificationCategory.Campaign,
            Recipient = new NotificationRecipientDto { UserId = userId, Name = "کاران" },
            Properties = new Dictionary<string, string?>
            {
                ["recipient_name"] = "کاران",
                ["survey_title"] = "نظرسنجی رضایت",
                ["link"] = "/surveys/123"
            }
        });

        result.IsSuccess.Should().BeTrue();
        // قالب پیش‌فرض توکار استفاده می‌شود چون قالبی در پایگاه داده نیست.
        result.Value!.Subject.Should().Contain("نظرسنجی رضایت");
        result.Value.Body.Should().Contain("کاران عزیز");
        result.Value.Body.Should().Contain("/surveys/123");
        // متغیرهای حل‌نشده خالی می‌شوند، نه اینکه باقی بمانند.
        result.Value.Subject.Should().NotContain("{{");
    }

    [Fact]
    public async Task Send_Prefers_Active_Db_Template_Over_Builtin_Default()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var templateService = env.Services.GetRequiredService<INotificationTemplateService>();

        // قالب سفارشی با همان کد قالب توکار: اولویت با پایگاه داده است.
        var created = await templateService.CreateAsync(new SaveNotificationTemplateRequest
        {
            Code = "system_notification",
            Name = "قالب سفارشی",
            Channel = NotificationChannel.InApp,
            Localizations =
            [
                new SaveTemplateLocalizationRequest
                {
                    Language = Language.Fa,
                    Subject = "موضوع سفارشی",
                    Body = "بدنه‌ی سفارشی"
                }
            ]
        });

        created.IsSuccess.Should().BeTrue();

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.SendAsync(new SendNotificationRequest
        {
            TemplateCode = "system_notification",
            Channel = NotificationChannel.InApp,
            Recipient = new NotificationRecipientDto { UserId = Guid.NewGuid(), Name = "گیرنده" },
            Properties = new Dictionary<string, string?>
            {
                ["subject"] = "نباید استفاده شود",
                ["body"] = "نباید استفاده شود"
            }
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Subject.Should().Be("موضوع سفارشی");
        result.Value.Body.Should().Be("بدنه‌ی سفارشی");
    }

    [Fact]
    public async Task SendToMany_Creates_And_Delivers_One_Per_Recipient()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<INotificationService>();

        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var user3 = Guid.NewGuid();

        var result = await service.SendToManyAsync(new SendNotificationsRequest
        {
            TemplateCode = "system_notification",
            Channel = NotificationChannel.InApp,
            Recipients =
            [
                new NotificationRecipientDto { UserId = user1, Name = "گیرنده یک" },
                new NotificationRecipientDto { UserId = user2, Name = "گیرنده دو" },
                new NotificationRecipientDto { UserId = user3, Name = "گیرنده سه" }
            ]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);

        var rows = await env.NotificationDbContext.Notifications.ToListAsync();
        rows.Should().HaveCount(3);
        rows.Should().OnlyContain(n => n.Status == NotificationStatus.Delivered);
        rows.Select(n => n.RecipientUserId).Should().BeEquivalentTo(new[] { user1, user2, user3 });
    }

    [Fact]
    public async Task Send_Email_Without_Address_Fails_Permanently()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.SendAsync(new SendNotificationRequest
        {
            TemplateCode = "system_notification",
            Channel = NotificationChannel.Email,
            Recipient = new NotificationRecipientDto { Name = "بدون ایمیل" }
        });

        // تحویل ناموفق است، ولی خود درخواست ارسال ثبت شده و وضعیت روی ردیف ذخیره می‌شود.
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(NotificationStatus.Failed);
        result.Value.LastError.Should().NotBeNullOrEmpty();

        var row = await env.NotificationDbContext.Notifications.SingleAsync();
        row.Status.Should().Be(NotificationStatus.Failed);
        row.RetryCount.Should().Be(0, "شکست دائمی است و امتحان مجدد برنامه‌ریزی نمی‌شود");
    }

    [Fact]
    public async Task Send_Sms_Without_Phone_Fails_Permanently()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.SendAsync(new SendNotificationRequest
        {
            TemplateCode = "system_notification",
            Channel = NotificationChannel.Sms,
            Recipient = new NotificationRecipientDto { Name = "بدون شماره" }
        });

        result.Value!.Status.Should().Be(NotificationStatus.Failed);
    }

    [Fact]
    public async Task Send_With_Unregistered_Channel_Fails_Instead_Of_Throwing()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.SendAsync(new SendNotificationRequest
        {
            TemplateCode = "system_notification",
            Channel = (NotificationChannel)999,
            Recipient = new NotificationRecipientDto { UserId = Guid.NewGuid() }
        });

        // مسیر دفاعی: کانال ناشناخته نباید استثنا پرتاب کند، بلکه ثبت شکست می‌شود.
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(NotificationStatus.Failed);
    }

    // --- صندوق ورودی و حریم خصوصی -------------------------------------------------

    private static async Task<Guid> SeedOwnedNotificationAsync(TestEnvironment env, Guid recipientUserId, NotificationStatus status = NotificationStatus.Delivered)
    {
        var notification = new NotificationEntity
        {
            RecipientUserId = recipientUserId,
            RecipientName = "گیرنده",
            Channel = NotificationChannel.InApp,
            Category = NotificationCategory.General,
            Status = status,
            TemplateCode = "system_notification",
            Subject = "اعلان",
            Body = "بدنه",
            Language = Language.Fa,
            CreatedAt = DateTime.UtcNow
        };

        env.NotificationDbContext.Notifications.Add(notification);
        await env.NotificationDbContext.SaveChangesAsync();

        return notification.Id;
    }

    [Fact]
    public async Task Search_Returns_Only_Own_Notifications()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        await SeedOwnedNotificationAsync(env, userId);
        await SeedOwnedNotificationAsync(env, Guid.NewGuid());

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.SearchAsync(new NotificationSearchRequest { PageSize = 50 });

        result.TotalCount.Should().Be(1);
        result.Items.Single().RecipientUserId.Should().Be(userId);
    }

    [Fact]
    public async Task Search_Ignores_Requested_Other_User_Without_Manage_Permission()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        await SeedOwnedNotificationAsync(env, userId);
        await SeedOwnedNotificationAsync(env, Guid.NewGuid());

        var service = env.Services.GetRequiredService<INotificationService>();

        // تلاش برای دیدن اعلان‌های کاربر دیگر نباید کار کند.
        var result = await service.SearchAsync(new NotificationSearchRequest
        {
            RecipientUserId = Guid.NewGuid(),
            PageSize = 50
        });

        result.TotalCount.Should().Be(1);
        result.Items.Single().RecipientUserId.Should().Be(userId);
    }

    [Fact]
    public async Task Search_As_Manager_Returns_All()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(
            orgUnitId: null, DataScope.Company, permissions: Permissions.Notifications.Manage);

        var other = Guid.NewGuid();
        await SeedOwnedNotificationAsync(env, userId);
        await SeedOwnedNotificationAsync(env, other);

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.SearchAsync(new NotificationSearchRequest { PageSize = 50 });

        result.TotalCount.Should().Be(2);
        result.Items.Select(n => n.RecipientUserId).Should().BeEquivalentTo(new[] { userId, other });
    }

    [Fact]
    public async Task GetById_As_Non_Owner_Is_Rejected()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var otherId = Guid.NewGuid();
        var notificationId = await SeedOwnedNotificationAsync(env, otherId);

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.GetByIdAsync(notificationId);

        // پیام خطا عمداً «یافت نشد» است تا وجود اعلان کاربر دیگر فاش نشود.
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("notification_not_found");
    }

    [Fact]
    public async Task MarkRead_As_Non_Owner_Is_Rejected()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var notificationId = await SeedOwnedNotificationAsync(env, Guid.NewGuid());

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.MarkReadAsync(notificationId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("notification_not_owner");
    }

    [Fact]
    public async Task MarkRead_As_Owner_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var notificationId = await SeedOwnedNotificationAsync(env, userId);

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.MarkReadAsync(notificationId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ReadAt.Should().NotBeNull();
        result.Value.IsUnread.Should().BeFalse();

        var row = await env.NotificationDbContext.Notifications.SingleAsync();
        row.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAllRead_Clears_Unread_Count()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        await SeedOwnedNotificationAsync(env, userId);
        await SeedOwnedNotificationAsync(env, userId);

        var service = env.Services.GetRequiredService<INotificationService>();

        (await service.GetUnreadCountAsync()).Should().Be(2);

        var markAll = await service.MarkAllReadAsync(channel: null);

        markAll.IsSuccess.Should().BeTrue();
        (await service.GetUnreadCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task GetUnreadCount_Only_Counts_Unread_InApp()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        await SeedOwnedNotificationAsync(env, userId, NotificationStatus.Delivered); // خوانده‌نشده
        await SeedOwnedNotificationAsync(env, userId, NotificationStatus.Failed);     // شکست‌خورده نگه‌داشته نمی‌شود
        await SeedOwnedNotificationAsync(env, userId, NotificationStatus.Suppressed); // مسدودشده هم همین‌طور

        var service = env.Services.GetRequiredService<INotificationService>();

        (await service.GetUnreadCountAsync()).Should().Be(1);
    }

    // --- ترجیحات تحویل -----------------------------------------------------------

    [Fact]
    public async Task Opted_Out_Category_Is_Suppressed()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var preferenceService = env.Services.GetRequiredService<INotificationPreferenceService>();

        var optedOut = await preferenceService.UpdateAsync(new UpdateNotificationPreferenceRequest
        {
            Channel = NotificationChannel.InApp,
            Category = NotificationCategory.Campaign,
            IsEnabled = false
        });

        optedOut.IsSuccess.Should().BeTrue();

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.SendAsync(new SendNotificationRequest
        {
            TemplateCode = "campaign_invitation",
            Channel = NotificationChannel.InApp,
            Category = NotificationCategory.Campaign,
            Recipient = new NotificationRecipientDto { UserId = userId, Name = "گیرنده" }
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(NotificationStatus.Suppressed);
    }

    [Fact]
    public async Task General_Category_Is_Never_Suppressed()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var preferenceService = env.Services.GetRequiredService<INotificationPreferenceService>();

        // کاربر کل کانال درون‌برنامه‌ای را خاموش کرده است.
        await preferenceService.UpdateAsync(new UpdateNotificationPreferenceRequest
        {
            Channel = NotificationChannel.InApp,
            Category = null,
            IsEnabled = false
        });

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.SendAsync(new SendNotificationRequest
        {
            TemplateCode = "system_notification",
            Channel = NotificationChannel.InApp,
            Category = NotificationCategory.General,
            Recipient = new NotificationRecipientDto { UserId = userId }
        });

        // اعلان‌های سیستمی هرگز مسدود نمی‌شوند.
        result.Value!.Status.Should().Be(NotificationStatus.Delivered);
    }

    [Fact]
    public async Task Specific_Category_Preference_Takes_Precedence_Over_General()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var preferenceService = env.Services.GetRequiredService<INotificationPreferenceService>();

        await preferenceService.UpdateAsync(new UpdateNotificationPreferenceRequest
        {
            Channel = NotificationChannel.InApp,
            Category = null,
            IsEnabled = true
        });

        await preferenceService.UpdateAsync(new UpdateNotificationPreferenceRequest
        {
            Channel = NotificationChannel.InApp,
            Category = NotificationCategory.Campaign,
            IsEnabled = false
        });

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.SendAsync(new SendNotificationRequest
        {
            TemplateCode = "campaign_invitation",
            Channel = NotificationChannel.InApp,
            Category = NotificationCategory.Campaign,
            Recipient = new NotificationRecipientDto { UserId = userId }
        });

        result.Value!.Status.Should().Be(NotificationStatus.Suppressed);
    }

    [Fact]
    public async Task Preference_List_Returns_Defaults_And_Persists_Update()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var preferenceService = env.Services.GetRequiredService<INotificationPreferenceService>();

        var defaults = await preferenceService.ListAsync();

        // به ازای هر کانال یک ترجیب پیش‌فرض روشن وجود دارد.
        defaults.Should().HaveCount(Enum.GetValues<NotificationChannel>().Length);
        defaults.Should().OnlyContain(p => p.IsDefault && p.IsEnabled);

        await preferenceService.UpdateAsync(new UpdateNotificationPreferenceRequest
        {
            Channel = NotificationChannel.Email,
            Category = null,
            IsEnabled = false
        });

        var after = await preferenceService.ListAsync();

        var email = after.Single(p => p.Channel == NotificationChannel.Email && p.Category is null);
        email.IsEnabled.Should().BeFalse();
        email.IsDefault.Should().BeFalse();
    }

    // --- قالب‌ها -------------------------------------------------------------------

    [Fact]
    public async Task Template_Crud_Lifecycle_Works()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<INotificationTemplateService>();

        var created = await service.CreateAsync(new SaveNotificationTemplateRequest
        {
            Code = "TPL-01",
            Name = "قالب آزمون",
            Channel = NotificationChannel.Email,
            Localizations =
            [
                new SaveTemplateLocalizationRequest
                {
                    Language = Language.Fa,
                    Subject = "موضوع",
                    Body = "بدنه"
                }
            ]
        });

        created.IsSuccess.Should().BeTrue();
        created.Value!.Code.Should().Be("TPL-01");
        created.Value.IsActive.Should().BeTrue();

        var updated = await service.UpdateAsync(created.Value.Id, new SaveNotificationTemplateRequest
        {
            Code = "TPL-01",
            Name = "قالب ویرایش‌شده",
            Channel = NotificationChannel.Email,
            Localizations =
            [
                new SaveTemplateLocalizationRequest
                {
                    Language = Language.Fa,
                    Subject = "موضوع جدید",
                    Body = "بدنه جدید"
                }
            ]
        });

        updated.IsSuccess.Should().BeTrue();
        updated.Value!.Name.Should().Be("قالب ویرایش‌شده");

        var byId = await service.GetByIdAsync(created.Value.Id);
        byId.Value!.Localizations.Single().Subject.Should().Be("موضوع جدید");

        (await service.ArchiveAsync(created.Value.Id)).IsSuccess.Should().BeTrue();

        var active = await service.SearchAsync(new NotificationTemplateSearchRequest());
        active.TotalCount.Should().Be(0);

        var archived = await service.SearchAsync(new NotificationTemplateSearchRequest { IncludeArchived = true });
        archived.TotalCount.Should().Be(1);
        archived.Items.Single().IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Template_Duplicate_Code_Is_Rejected()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<INotificationTemplateService>();

        var request = new SaveNotificationTemplateRequest
        {
            Code = "TPL-DUP",
            Name = "قالب",
            Localizations =
            [
                new SaveTemplateLocalizationRequest { Language = Language.Fa, Subject = "موضوع", Body = "بدنه" }
            ]
        };

        (await service.CreateAsync(request)).IsSuccess.Should().BeTrue();

        var duplicate = await service.CreateAsync(request);

        duplicate.IsFailure.Should().BeTrue();
        duplicate.Error.Code.Should().Be("template_code_exists");
    }

    [Fact]
    public async Task Archived_Template_Falls_Back_To_Builtin_Default()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var templateService = env.Services.GetRequiredService<INotificationTemplateService>();

        var created = await templateService.CreateAsync(new SaveNotificationTemplateRequest
        {
            Code = "system_notification",
            Name = "قالب سفارشی",
            Localizations =
            [
                new SaveTemplateLocalizationRequest
                {
                    Language = Language.Fa,
                    Subject = "موضوع سفارشی",
                    Body = "بدنه‌ی سفارشی"
                }
            ]
        });
        (await templateService.ArchiveAsync(created.Value!.Id)).IsSuccess.Should().BeTrue();

        var service = env.Services.GetRequiredService<INotificationService>();

        var result = await service.SendAsync(new SendNotificationRequest
        {
            TemplateCode = "system_notification",
            Channel = NotificationChannel.InApp,
            Recipient = new NotificationRecipientDto { UserId = Guid.NewGuid() },
            Properties = new Dictionary<string, string?>
            {
                ["subject"] = "موضوع اصلی",
                ["body"] = "بدنه‌ی اصلی"
            }
        });

        // قالب بایگانی‌شده دیگر استفاده نمی‌شود؛ متن پیش‌فرض توکار جایگزین می‌شود.
        result.Value!.Subject.Should().Be("موضوع اصلی");
    }

    // --- موتور تحویل و امتحان مجدد -------------------------------------------------

    [Fact]
    public async Task ProcessPending_Delivers_Queued_Rows()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var dispatcher = env.Services.GetRequiredService<INotificationDispatcher>();

        await SeedOwnedNotificationAsync(env, Guid.NewGuid(), NotificationStatus.Pending);

        var processed = await dispatcher.ProcessPendingAsync(maxBatch: 10);

        processed.Should().Be(1);
        (await env.NotificationDbContext.Notifications.SingleAsync()).Status
            .Should().Be(NotificationStatus.Delivered);
    }

    [Fact]
    public async Task ProcessPending_Skips_Not_Yet_Due_Retry()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var dispatcher = env.Services.GetRequiredService<INotificationDispatcher>();

        var notification = new NotificationEntity
        {
            RecipientUserId = Guid.NewGuid(),
            Channel = NotificationChannel.InApp,
            Status = NotificationStatus.Pending,
            TemplateCode = "system_notification",
            Subject = "اعلان",
            Body = "بدنه",
            Language = Language.Fa,
            RetryCount = 1,
            NextTryAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow
        };

        env.NotificationDbContext.Notifications.Add(notification);
        await env.NotificationDbContext.SaveChangesAsync();

        var processed = await dispatcher.ProcessPendingAsync(maxBatch: 10);

        processed.Should().Be(0);
        (await env.NotificationDbContext.Notifications.SingleAsync()).Status
            .Should().Be(NotificationStatus.Pending, "زمان تلاش بعدی هنوز نرسیده است");
    }

    [Fact]
    public async Task Retry_State_Machine_Schedules_Exponential_Backoff()
    {
        // این یک آزمون از رفتار خود موجودیت است: شکست موقت، تلاش بعدی را با
        // تأخیر نمایی برنامه‌ریزی می‌کند و پس از اتمام سقف، تلاش مجدد نمی‌شود.
        var notification = new NotificationEntity
        {
            RecipientUserId = Guid.NewGuid(),
            Channel = NotificationChannel.Email,
            Status = NotificationStatus.Pending,
            TemplateCode = "system_notification",
            Subject = "اعلان",
            Body = "بدنه",
            MaxRetries = 3
        };

        notification.ScheduleRetry("خطای گذرا");

        notification.RetryCount.Should().Be(1);
        notification.Status.Should().Be(NotificationStatus.Pending);
        notification.NextTryAt.Should().BeAfter(DateTime.UtcNow);
        notification.CanRetry.Should().BeTrue();

        notification.ScheduleRetry("خطای گذرا");
        notification.ScheduleRetry("خطای گذرا");

        notification.RetryCount.Should().Be(3);
        notification.CanRetry.Should().BeFalse();

        // پس از اتمام سقف، شکست دائمی می‌شود.
        notification.MarkFailed("دیگر تلاشی نمانده");
        notification.Status.Should().Be(NotificationStatus.Failed);
    }

    // --- ممیزی ---------------------------------------------------------------------

    [Fact]
    public async Task Send_Delivery_And_Read_Are_Audited()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company);

        var service = env.Services.GetRequiredService<INotificationService>();

        var sent = await service.SendAsync(new SendNotificationRequest
        {
            TemplateCode = "system_notification",
            Channel = NotificationChannel.InApp,
            Recipient = new NotificationRecipientDto { UserId = userId, Name = "کاربر" },
            Properties = new Dictionary<string, string?>
            {
                ["subject"] = "اعلان ممیزی",
                ["body"] = "بدنه"
            }
        });

        await service.MarkReadAsync(sent.Value!.Id);

        var auditService = env.Services.GetRequiredService<IAuditService>();

        var entries = await auditService.SearchAsync(new AuditSearchRequest(
            EntityType: "notification", Action: null, UserId: null,
            FromUtc: null, ToUtc: null, Page: 1, PageSize: 100));

        // شنونده‌ی ممیزی باید ایجاد، تحویل و خوانده‌شدن را ثبت کرده باشد.
        entries.Select(e => e.Action).Should().Contain(AuditedActions);
    }

    // --- یکپارچگی با ماژول کمپین ---------------------------------------------------

    /// <summary>ساخت و انتشار یک پرسشنامه و سپس یک نظرسنجی‌ی فعال.</summary>
    private static async Task<SurveyDto> SeedActiveSurveyAsync(TestEnvironment env)
    {
        var questionService = env.Services.GetRequiredService<IQuestionService>();
        var questionnaireService = env.Services.GetRequiredService<IQuestionnaireService>();
        var surveyService = env.Services.GetRequiredService<ISurveyService>();

        var question = (await questionService.CreateAsync(new SaveQuestionRequest
        {
            Code = "QB-N-01",
            Type = QuestionType.SingleChoice,
            Localizations = [ new QuestionLocalizationDto { Language = Language.Fa, Text = "سؤال نمونه" } ],
            Options =
            [
                new SaveQuestionOptionRequest
                {
                    Code = "A", DisplayOrder = 0,
                    Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "کم" } ]
                },
                new SaveQuestionOptionRequest
                {
                    Code = "B", DisplayOrder = 1,
                    Localizations = [ new QuestionOptionLocalizationDto { Language = Language.Fa, Text = "زیاد" } ]
                }
            ]
        })).Value!;

        var questionnaire = (await questionnaireService.CreateAsync(new SaveQuestionnaireRequest
        {
            Code = "QS-N-01",
            Localizations = [ new QuestionnaireLocalizationDto { Language = Language.Fa, Title = "پرسشنامه" } ],
            Sections =
            [
                new SaveSectionRequest
                {
                    Localizations = [ new SectionLocalizationDto { Language = Language.Fa, Title = "بخش اول" } ],
                    Items = [ new SaveItemRequest { QuestionId = question.Id, IsRequired = true } ]
                }
            ]
        })).Value!;

        (await questionnaireService.PublishAsync(questionnaire.Id)).IsSuccess.Should().BeTrue();

        var survey = (await surveyService.CreateAsync(new SaveSurveyRequest
        {
            Code = "SV-N-01",
            QuestionnaireId = questionnaire.Id,
            Localizations = [ new SurveyLocalizationDto { Language = Language.Fa, Title = "نظرسنجی نمونه" } ]
        })).Value!;

        (await surveyService.PublishAsync(survey.Id)).IsSuccess.Should().BeTrue();

        return survey;
    }

    private static SaveCampaignRequest CreateCampaignRequest(
        Guid surveyId,
        IReadOnlyList<SaveReminderRequest>? reminders = null) => new()
        {
            Code = "CMP-NOTIFY",
            SurveyId = surveyId,
            AudienceType = TargetAudienceType.AllCompany,
            Channel = DistributionChannel.Email,
            TargetOrgUnitIds = [],
            TargetEmployeeIds = [],
            Reminders = reminders ?? [],
            Localizations =
            [
                new CampaignLocalizationDto { Language = Language.Fa, Title = "کمپین اعلان" }
            ]
        };

    private static SaveReminderRequest CreateReminder(DateTime sendAt) => new()
    {
        Id = Guid.CreateVersion7(),
        SendAt = sendAt,
        Localizations =
        [
            new ReminderLocalizationDto { Language = Language.Fa, Subject = "یادآوری", Body = "لطفاً پاسخ دهید." }
        ]
    };

    /// <summary>۳ کارمند (یک فعال، یک مرخصی، یک ترک‌کار) → ۲ گیرنده‌ی قابل‌تحویل.</summary>
    private static async Task SeedCampaignAudienceAsync(TestEnvironment env)
    {
        var units = await env.SeedOrgHierarchyAsync();
        await env.SeedEmployeesAsync(new Dictionary<Guid, int> { { units.companyId, 3 } });
    }

    [Fact]
    public async Task Campaign_Launch_Creates_Invitation_Notifications()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var survey = await SeedActiveSurveyAsync(env);
        await SeedCampaignAudienceAsync(env);

        var campaignService = env.Services.GetRequiredService<ICampaignService>();

        var created = (await campaignService.CreateAsync(CreateCampaignRequest(survey.Id))).Value!;
        var launch = await campaignService.LaunchAsync(created.Id);
        launch.IsSuccess.Should().BeTrue();

        var notifications = await env.NotificationDbContext.Notifications.ToListAsync();

        // کارمندان ایمیل کاری دارند و کاربر سامانه ندارند → کانال ایمیل.
        notifications.Should().HaveCount(2);
        notifications.Should().OnlyContain(n => n.TemplateCode == "campaign_invitation");
        notifications.Should().OnlyContain(n => n.Category == NotificationCategory.Campaign);
        notifications.Should().OnlyContain(n => n.Channel == NotificationChannel.Email);
        notifications.Should().OnlyContain(n => n.Subject.Contains("کمپین اعلان"));
        notifications.Select(n => n.SourceId).Should().NotContainNulls();
    }

    [Fact]
    public async Task Reminder_Due_Creates_Reminder_Notifications()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var survey = await SeedActiveSurveyAsync(env);
        await SeedCampaignAudienceAsync(env);

        var campaignService = env.Services.GetRequiredService<ICampaignService>();

        var created = (await campaignService.CreateAsync(CreateCampaignRequest(
            survey.Id,
            reminders: [ CreateReminder(DateTime.UtcNow.AddDays(-1)) ]))).Value!;

        (await campaignService.LaunchAsync(created.Id)).IsSuccess.Should().BeTrue();

        var processed = await campaignService.ProcessDueRemindersAsync();

        processed.IsSuccess.Should().BeTrue();
        processed.Value!.RemindersProcessed.Should().Be(1);

        // یادآور برای گیرندگانی که هنوز پاسخ نداده‌اند ارسال می‌شود.
        var reminders = await env.NotificationDbContext.Notifications
            .Where(n => n.TemplateCode == "campaign_reminder")
            .ToListAsync();

        reminders.Should().HaveCount(2);
        reminders.Should().OnlyContain(n => n.Category == NotificationCategory.Campaign);
    }

    [Fact]
    public async Task Campaign_Notification_Listener_Is_Idempotent()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company);
        var survey = await SeedActiveSurveyAsync(env);
        await SeedCampaignAudienceAsync(env);

        var campaignService = env.Services.GetRequiredService<ICampaignService>();

        var created = (await campaignService.CreateAsync(CreateCampaignRequest(survey.Id))).Value!;
        (await campaignService.LaunchAsync(created.Id)).IsSuccess.Should().BeTrue();

        var afterLaunch = await env.NotificationDbContext.Notifications.CountAsync();

        // پردازش مجدد رویداد راه‌اندازی نباید اعلان مضاعف بسازد.
        var dispatcher = env.Services.GetRequiredService<IDomainEventDispatcher>();
        await dispatcher.DispatchAsync(new CampaignLaunchedEvent(created.Id, created.Code, recipientCount: 2, actorUserId: null));

        var afterRedispatch = await env.NotificationDbContext.Notifications.CountAsync();

        afterRedispatch.Should().Be(afterLaunch);
    }
}
