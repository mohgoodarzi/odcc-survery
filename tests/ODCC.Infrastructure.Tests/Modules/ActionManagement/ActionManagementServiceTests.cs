using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.ActionManagement.Abstractions;
using ODCC.Application.Modules.ActionManagement.Dtos;
using ODCC.Application.Modules.Notification.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.ActionManagement.Entities;
using ODCC.Domain.Modules.ActionManagement.Enums;
using ODCC.Domain.Modules.Analytics.Enums;
using ODCC.Domain.Modules.Analytics.Events;
using ODCC.Domain.Modules.Notification.Enums;
using ODCC.Infrastructure.Modules.ActionManagement.Persistence;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.ActionManagement;

/// <summary>
/// آزمون‌های ماژول مدیریت اقدامات و پیگیری: چرخه‌ی عمر برنامه‌ها و آیتم‌ها،
/// مرز سازمانی (fail-closed) با استثناء‌ی آیتم‌های منتسب به کاربر، انتصاب و
/// مجوزدهی، دیدگاه‌ها و پیوست‌ها (با جلوگیری از path traversal)، پیگیری
/// خودکار (یادآور/تشدید)، تولید خودکار برنامه از هشدار تحلیلات و سنجش اثربخشی.
///
/// <b>حریم خصوصی:</b> آزمون می‌شود که این ماژول هرگز شناسه‌ی پاسخ‌گوی نظرسنجی
/// را ذخیره نمی‌کند — فقط شاخص‌های تجمعی.
///
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ پایگاه‌داده‌ی
/// واقعی یا سرویس بیرونی را لمس نمی‌کنند.
/// </summary>
public class ActionManagementServiceTests
{
    private static readonly string[] ManagePermissions = [Permissions.Actions.Manage, Permissions.Actions.View];

    // --- چرخه‌ی عمر برنامه‌ها ---------------------------------------------------

    [Fact]
    public async Task CreatePlan_WithItems_CreatesDraftWithItems()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IActionManagementService>();

        var result = await service.CreatePlanAsync(new SaveActionPlanRequest
        {
            Title = "بهبود NPS واحد مالی",
            Description = "افت NPS در نظرسنجی تابستانه",
            Priority = ActionPriority.High,
            Items =
            [
                new SaveActionItemRequest { Title = "بررسی دلایل افت", DisplayOrder = 0 },
                new SaveActionItemRequest { Title = "اقدام اصلاحی", DisplayOrder = 1 }
            ]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ActionPlanStatus.Draft);
        result.Value.Source.Should().Be(ActionSource.Manual);
        result.Value.CreatedByUserId.Should().Be(userId);
        result.Value.TotalItemCount.Should().Be(2);
        result.Value.CompletedItemCount.Should().Be(0);
        result.Value.ProgressPercentage.Should().Be(0m);

        var plan = await env.ActionManagementDbContext.ActionPlans
            .Include(p => p.Items)
            .SingleAsync();

        plan.Title.Should().Be("بهبود NPS واحد مالی");
        plan.Items.Should().HaveCount(2);
        plan.Items.All(i => i.Status == ActionItemStatus.Open).Should().BeTrue();
    }

    [Fact]
    public async Task CreatePlan_ActivateImmediately_SetsActive()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IActionManagementService>();

        var result = await service.CreatePlanAsync(new SaveActionPlanRequest
        {
            Title = "برنامه فوری",
            ActivateImmediately = true
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ActionPlanStatus.Active);
    }

    [Fact]
    public async Task PlanLifecycle_Activate_Then_Complete_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IActionManagementService>();
        var planId = await CreatePlanAsync(env, service);

        var activated = await service.ActivatePlanAsync(planId);
        activated.IsSuccess.Should().BeTrue();
        activated.Value!.Status.Should().Be(ActionPlanStatus.Active);

        // تکمیل بدون آیتمِ انجام‌شده هم مجاز است (مدیر تصمیم می‌گیرد).
        var completed = await service.CompletePlanAsync(planId);
        completed.IsSuccess.Should().BeTrue();
        completed.Value!.Status.Should().Be(ActionPlanStatus.Completed);
        completed.Value.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task CompletePlan_FromDraft_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IActionManagementService>();
        var planId = await CreatePlanAsync(env, service);

        var result = await service.CompletePlanAsync(planId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("action_plan_not_active");
    }

    [Fact]
    public async Task CancelPlan_FromActive_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IActionManagementService>();
        var planId = await CreatePlanAsync(env, service, activateImmediately: true);

        var result = await service.CancelPlanAsync(planId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ActionPlanStatus.Cancelled);
    }

    [Fact]
    public async Task ArchivePlan_HidesFromSearchByDefault()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IActionManagementService>();
        var planId = await CreatePlanAsync(env, service);

        var archive = await service.ArchivePlanAsync(planId);
        archive.IsSuccess.Should().BeTrue();

        var search = await service.SearchPlansAsync(new ActionPlanSearchRequest());
        search.Items.Should().NotContain(p => p.Id == planId);

        var withArchived = await service.SearchPlansAsync(new ActionPlanSearchRequest { IncludeArchived = true });
        withArchived.Items.Should().Contain(p => p.Id == planId);
    }

    [Fact]
    public async Task RecordOutcome_StoresValueAndTimestamp()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IActionManagementService>();
        var planId = await CreatePlanAsync(env, service);

        var result = await service.RecordPlanOutcomeAsync(planId, new RecordOutcomeRequest { Value = 42.5m });

        result.IsSuccess.Should().BeTrue();
        result.Value!.OutcomeMetricValue.Should().Be(42.5m);
        result.Value.OutcomeMeasuredAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdatePlan_OnArchivedPlan_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IActionManagementService>();
        var planId = await CreatePlanAsync(env, service);
        await service.ArchivePlanAsync(planId);

        var result = await service.UpdatePlanAsync(planId, new SaveActionPlanRequest { Title = "تغییر" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("action_plan_archived");
    }

    // --- مرز سازمانی (fail-closed) ----------------------------------------------

    [Fact]
    public async Task GetPlan_OutsideOrgScope_ReturnsNotFound()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (company, division, _, _, otherDivision, _) = await env.SeedOrgHierarchyAsync();

        // کاربر دامنه‌اش «امور مالی» است؛ برنامه‌ای در «عملیات» می‌سازد.
        env.SetCurrentUser(division, DataScope.Division, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();

        var created = await service.CreatePlanAsync(new SaveActionPlanRequest
        {
            Title = "برنامه داخلی مالی",
            OrgUnitId = division
        });
        created.IsSuccess.Should().BeTrue();

        // کاربر دیگر با دامنه‌ی «عملیات» نباید برنامه‌ی مالی را ببیند.
        env.SetCurrentUser(otherDivision, DataScope.Division, permissions: ManagePermissions);

        var result = await service.GetPlanByIdAsync(created.Value!.Id);

        result.IsFailure.Should().BeTrue();
        // وجود برنامه‌ی خارج از دامنه نباید فاش شود.
        result.Error.Code.Should().Be("action_plan_not_found");
    }

    [Fact]
    public async Task CreatePlan_WithOrgUnitOutsideScope_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, division, _, _, otherDivision, _) = await env.SeedOrgHierarchyAsync();

        env.SetCurrentUser(division, DataScope.Division, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();

        var result = await service.CreatePlanAsync(new SaveActionPlanRequest
        {
            Title = "نامجرا",
            OrgUnitId = otherDivision
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("action_org_unit_out_of_scope");

        (await env.ActionManagementDbContext.ActionPlans.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SearchPlans_ScopedUser_OnlySeesOwnSubtree()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (company, division, _, _, otherDivision, _) = await env.SeedOrgHierarchyAsync();

        env.SetCurrentUser(division, DataScope.Division, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();

        var inScope = await service.CreatePlanAsync(new SaveActionPlanRequest { Title = "مالی", OrgUnitId = division });
        var outScope = await service.CreatePlanAsync(new SaveActionPlanRequest { Title = "سراسری" });

        inScope.IsSuccess.Should().BeTrue();
        // برنامه‌ی سراسری فقط با دامنه‌ی شرکت مجاز است.
        outScope.IsFailure.Should().BeTrue();

        var search = await service.SearchPlansAsync(new ActionPlanSearchRequest());
        search.Items.Should().ContainSingle(p => p.Title == "مالی");
    }

    // --- آیتم‌ها -------------------------------------------------------------------

    [Fact]
    public async Task CreateItem_OnActivePlan_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var planId = await CreatePlanAsync(env, service, activateImmediately: true);

        var result = await service.CreateItemAsync(planId, new SaveActionItemRequest
        {
            Title = "اقدام اول",
            AssigneeUserId = userId,
            DueDate = DateTime.UtcNow.AddDays(7),
            RemindAt = DateTime.UtcNow.AddDays(6)
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ActionItemStatus.Open);
        result.Value.AssigneeUserId.Should().Be(userId);
        result.Value.ActionPlanId.Should().Be(planId);
    }

    [Fact]
    public async Task CreateItem_OnArchivedPlan_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var planId = await CreatePlanAsync(env, service);
        await service.ArchivePlanAsync(planId);

        var result = await service.CreateItemAsync(planId, new SaveActionItemRequest { Title = "غیرممکن" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("action_plan_closed");
    }

    [Fact]
    public async Task TransitionItem_StartThenComplete_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var (planId, itemId) = await CreatePlanWithItemAsync(env, service, assigneeId: userId);

        var started = await service.TransitionItemAsync(itemId, new TransitionActionItemRequest { NewStatus = ActionItemStatus.InProgress });
        started.IsSuccess.Should().BeTrue();
        started.Value!.Status.Should().Be(ActionItemStatus.InProgress);
        started.Value.StartedAt.Should().NotBeNull();

        var completed = await service.TransitionItemAsync(itemId, new TransitionActionItemRequest { NewStatus = ActionItemStatus.Done });
        completed.IsSuccess.Should().BeTrue();
        completed.Value!.Status.Should().Be(ActionItemStatus.Done);
        completed.Value.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task TransitionItem_Reopen_CompletedItem()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var (planId, itemId) = await CreatePlanWithItemAsync(env, service, assigneeId: userId);

        await service.TransitionItemAsync(itemId, new TransitionActionItemRequest { NewStatus = ActionItemStatus.Done });
        var reopened = await service.TransitionItemAsync(itemId, new TransitionActionItemRequest { NewStatus = ActionItemStatus.InProgress });

        reopened.IsSuccess.Should().BeTrue();
        reopened.Value!.Status.Should().Be(ActionItemStatus.InProgress);
        reopened.Value.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task TransitionItem_SameStatus_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var (planId, itemId) = await CreatePlanWithItemAsync(env, service, assigneeId: userId);

        var result = await service.TransitionItemAsync(itemId, new TransitionActionItemRequest { NewStatus = ActionItemStatus.Open });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("action_same_status");
    }

    [Fact]
    public async Task TransitionItem_Cancel_RequiresManagePermission()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: [Permissions.Actions.View]);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var (planId, itemId) = await CreatePlanWithItemAsync(env, service, assigneeId: userId);

        // مسئول بدون مجوز مدیریت نباید آیتم را لغو کند.
        var result = await service.TransitionItemAsync(itemId, new TransitionActionItemRequest { NewStatus = ActionItemStatus.Cancelled });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("action_not_authorized");

        // ولی شروع کار را می‌تواند انجام دهد.
        var started = await service.TransitionItemAsync(itemId, new TransitionActionItemRequest { NewStatus = ActionItemStatus.InProgress });
        started.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task TransitionItem_ByNonAssigneeWithoutManage_Forbidden()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: [Permissions.Actions.View]);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var (planId, itemId) = await CreatePlanWithItemAsync(env, service, assigneeId: Guid.NewGuid());

        var result = await service.TransitionItemAsync(itemId, new TransitionActionItemRequest { NewStatus = ActionItemStatus.InProgress });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("action_not_authorized");
    }

    [Fact]
    public async Task AssessEffectiveness_OnlyAfterCompletion()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var (planId, itemId) = await CreatePlanWithItemAsync(env, service, assigneeId: userId);

        var before = await service.AssessItemEffectivenessAsync(itemId, new AssessEffectivenessRequest
        {
            Rating = EffectivenessRating.Effective
        });
        before.IsFailure.Should().BeTrue();
        before.Error.Code.Should().Be("action_item_not_finished");

        await service.TransitionItemAsync(itemId, new TransitionActionItemRequest { NewStatus = ActionItemStatus.Done });

        var after = await service.AssessItemEffectivenessAsync(itemId, new AssessEffectivenessRequest
        {
            Rating = EffectivenessRating.PartiallyEffective,
            Note = "بخشی از مشکل حل شد"
        });
        after.IsSuccess.Should().BeTrue();
        after.Value!.Effectiveness.Should().Be(EffectivenessRating.PartiallyEffective);
        after.Value.EffectivenessNote.Should().Be("بخشی از مشکل حل شد");
    }

    [Fact]
    public async Task SearchItems_AssignedToMe_VisibleOutsideOrgScope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, division, _, _, otherDivision, _) = await env.SeedOrgHierarchyAsync();

        // کاربر «عملیات» که قرار است گیرنده‌ی آیتم باشد.
        var (assigneeService, assigneeId) = env.SetCurrentUser(otherDivision, DataScope.Division, userName: "assignee");

        // مدیر مالی یک برنامه و آیتم می‌سازد و به کاربر «عملیات» انتصاب می‌کند.
        env.SetCurrentUser(division, DataScope.Division, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();

        var plan = await service.CreatePlanAsync(new SaveActionPlanRequest
        {
            Title = "برنامه مالی",
            OrgUnitId = division,
            ActivateImmediately = true
        });
        plan.IsSuccess.Should().BeTrue();

        var item = await service.CreateItemAsync(plan.Value!.Id, new SaveActionItemRequest
        {
            Title = "کار برای عملیات",
            AssigneeUserId = assigneeId
        });
        item.IsSuccess.Should().BeTrue();

        // کاربر منتسب در «عملیات» آیتم را در فهرست «کارهای من» می‌بیند، هرچند
        // برنامه در واحد سازمانی دیگری است (استثناء‌ی مستند).
        env.SetCurrentUser(otherDivision, DataScope.Division, userName: "assignee");
        assigneeService.UserId = assigneeId; // نگه‌داشتن شناسه‌ی یکسان گیرنده.

        var mine = await service.SearchItemsAsync(new ActionItemSearchRequest { AssignedToMe = true });
        mine.Items.Should().ContainSingle(i => i.Id == item.Value!.Id);
        mine.Items[0].IsAssignedToMe.Should().BeTrue();
    }

    // --- دیدگاه‌ها -----------------------------------------------------------------

    [Fact]
    public async Task AddComment_Succeeds_AndListsOrdered()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var (planId, itemId) = await CreatePlanWithItemAsync(env, service);

        var first = await service.AddCommentAsync(itemId, new AddActionCommentRequest { Body = "اولین دیدگاه" });
        var second = await service.AddCommentAsync(itemId, new AddActionCommentRequest { Body = "دومین دیدگاه" });

        first.IsSuccess.Should().BeTrue();
        first.Value!.AuthorUserId.Should().Be(userId);

        var list = await service.ListCommentsAsync(itemId);

        list.IsSuccess.Should().BeTrue();
        list.Value.Should().HaveCount(2);
        list.Value[0].Body.Should().Be("اولین دیدگاه");
        list.Value[1].Body.Should().Be("دومین دیدگاه");
    }

    // --- پیوست‌ها ------------------------------------------------------------------

    [Fact]
    public async Task UploadEvidence_ThenDownload_ReturnsSameContent()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var (planId, itemId) = await CreatePlanWithItemAsync(env, service);

        var bytes = "مدرک اجرای اقدام"u8.ToArray();

        var uploaded = await service.UploadEvidenceAsync(itemId, new UploadActionEvidenceRequest
        {
            Content = new MemoryStream(bytes),
            FileName = "report.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = bytes.Length
        });

        uploaded.IsSuccess.Should().BeTrue();
        uploaded.Value!.FileName.Should().Be("report.pdf");
        uploaded.Value.FileSizeBytes.Should().Be(bytes.Length);

        var downloaded = await service.DownloadEvidenceAsync(uploaded.Value.Id);

        downloaded.IsSuccess.Should().BeTrue();
        downloaded.Value!.FileName.Should().Be("report.pdf");
        downloaded.Value.ContentType.Should().Be("application/pdf");

        using var reader = new StreamReader(downloaded.Value.Content);
        (await reader.ReadToEndAsync()).Should().Be("مدرک اجرای اقدام");
    }

    [Fact]
    public async Task DeleteEvidence_ByOtherUser_Forbidden()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (current, uploaderId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var (planId, itemId) = await CreatePlanWithItemAsync(env, service);

        var uploaded = await service.UploadEvidenceAsync(itemId, new UploadActionEvidenceRequest
        {
            Content = new MemoryStream("x"u8.ToArray()),
            FileName = "a.txt",
            ContentType = "text/plain",
            FileSizeBytes = 1
        });

        // کاربر دیگر (بدون مجوز مدیریت) نباید پیوست را حذف کند.
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, userName: "other", permissions: [Permissions.Actions.View]);

        var result = await service.DeleteEvidenceAsync(uploaded.Value!.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("action_not_authorized");

        // آپلودکننده می‌تواند حذف کند.
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, userName: "uploader", permissions: ManagePermissions);
        current.UserId = uploaderId; // بازگرداندن شناسه‌ی آپلودکننده‌ی اصلی.

        var mine = await service.DeleteEvidenceAsync(uploaded.Value.Id);
        mine.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task EvidenceStore_PreventsPathTraversal()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var store = env.Services.GetRequiredService<IActionEvidenceStore>();

        var act = () => store.OpenReadAsync("../../../../outside.txt");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // --- پیگیری خودکار (یادآور/تشدید) ---------------------------------------------

    [Fact]
    public async Task ProcessDueReminders_PublishesEvent_AndClearsRemindAt()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var (planId, itemId) = await CreatePlanWithItemAsync(env, service, assigneeId: userId);

        // یادآور را مستقیماً در گذشته تنظیم می‌کنیم (دور از اعتبارسنجی سرویس).
        await SeedReminderInPastAsync(env, itemId, DateTime.UtcNow.AddMinutes(-5));

        var processed = await service.ProcessDueRemindersAsync(DateTime.UtcNow);

        processed.Should().Be(1);

        // یادآور پاک شده تا دوباره فعال نشود.
        var item = await env.ActionManagementDbContext.ActionItems.SingleAsync(i => i.Id == itemId);
        item.RemindAt.Should().BeNull();

        // شنونده‌ی اعلان‌ها باید یک اعلان درون‌برنامه‌ای برای مسئول ساخته باشد.
        // (اعلان انتصاب هم از قبل وجود دارد، پس روی قالب یادآور فیلتر می‌کنیم.)
        var notification = await env.NotificationDbContext.Notifications
            .Where(n => n.TemplateCode == "action_reminder")
            .SingleAsync();
        notification.RecipientUserId.Should().Be(userId);
    }

    [Fact]
    public async Task ProcessOverdueEscalations_EscalatesAndNotifies()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var planId = await CreatePlanAsync(env, service, activateImmediately: true);

        // آیتم سررسیده‌شده‌ی باز مستقیماً در دیتابیس (دور از اعتبارسنجی مهلت).
        var itemId = Guid.CreateVersion7();
        env.ActionManagementDbContext.ActionItems.Add(new ActionItem
        {
            Id = itemId,
            ActionPlanId = planId,
            Title = "کار سررسیده‌شده",
            Status = ActionItemStatus.Open,
            DueDate = DateTime.UtcNow.AddDays(-2),
            AssigneeUserId = userId
        });
        await env.ActionManagementDbContext.SaveChangesAsync();

        var escalated = await service.ProcessOverdueEscalationsAsync(DateTime.UtcNow);

        escalated.Should().Be(1);

        var item = await env.ActionManagementDbContext.ActionItems.SingleAsync(i => i.Id == itemId);
        item.EscalationLevel.Should().Be(EscalationLevel.Reminder);
        item.EscalatedAt.Should().NotBeNull();

        var notification = await env.NotificationDbContext.Notifications.SingleAsync();
        notification.RecipientUserId.Should().Be(userId);
        notification.TemplateCode.Should().Be("action_escalated");
    }

    [Fact]
    public async Task ProcessOverdueEscalations_DoesNotEscalateTwiceWithinInterval()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var planId = await CreatePlanAsync(env, service, activateImmediately: true);

        var itemId = Guid.CreateVersion7();
        env.ActionManagementDbContext.ActionItems.Add(new ActionItem
        {
            Id = itemId,
            ActionPlanId = planId,
            Title = "کار سررسیده‌شده",
            Status = ActionItemStatus.Open,
            DueDate = DateTime.UtcNow.AddDays(-2),
            AssigneeUserId = userId
        });
        await env.ActionManagementDbContext.SaveChangesAsync();

        await service.ProcessOverdueEscalationsAsync(DateTime.UtcNow);
        // بلافاصله دوباره: نباید دوباره تشدید شود (فاصله‌ی زمانی نگه داشته شده).
        var second = await service.ProcessOverdueEscalationsAsync(DateTime.UtcNow);

        second.Should().Be(0);
    }

    // --- هشدار تحلیلات → برنامه‌ی خودکار ------------------------------------------

    [Fact]
    public async Task EnsurePlanFromAnalyticsAlert_CreatesActivePlanWithStarterItem()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var surveyId = Guid.NewGuid();

        var result = await service.EnsurePlanFromAnalyticsAlertAsync(new AnalyticsAlertRequest
        {
            SurveyId = surveyId,
            SurveyCode = "SV-2026-01",
            SurveyTitle = "نظرسنجی تابستانه",
            SegmentKey = "survey",
            MetricType = ActionMetricType.Nps,
            MetricValue = -10m,
            TargetValue = 0m,
            SuggestedTitle = "افت NPS"
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Source.Should().Be(ActionSource.AnalyticsAlert);
        result.Value.Status.Should().Be(ActionPlanStatus.Active);
        result.Value.TriggerMetricType.Should().Be(ActionMetricType.Nps);
        result.Value.TriggerMetricValue.Should().Be(-10m);
        result.Value.TotalItemCount.Should().Be(1);

        var plan = await env.ActionManagementDbContext.ActionPlans
            .Include(p => p.Items)
            .SingleAsync();

        // کلید یکتای منبع برای idempotency.
        plan.SourceKey.Should().NotBeNullOrEmpty();
        plan.SurveyId.Should().Be(surveyId);
        plan.SurveyTitle.Should().Be("نظرسنجی تابستانه");
    }

    [Fact]
    public async Task EnsurePlanFromAnalyticsAlert_IsIdempotent()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();
        var surveyId = Guid.NewGuid();

        var request = new AnalyticsAlertRequest
        {
            SurveyId = surveyId,
            SurveyCode = "SV-2026-02",
            SurveyTitle = "نظرسنجی پاییزه",
            SegmentKey = "survey",
            MetricType = ActionMetricType.Csat,
            MetricValue = 50m,
            TargetValue = 70m,
            SuggestedTitle = "افت CSAT"
        };

        await service.EnsurePlanFromAnalyticsAlertAsync(request);
        var second = await service.EnsurePlanFromAnalyticsAlertAsync(request);

        second.IsSuccess.Should().BeTrue();

        // فقط یک برنامه باید وجود داشته باشد.
        (await env.ActionManagementDbContext.ActionPlans.CountAsync()).Should().Be(1);

        // مقدار شاخصِ خروجیِ برنامه‌ی موجود به‌روز شده باشد (سنجش اثربخشی زنده).
        var plan = await env.ActionManagementDbContext.ActionPlans.SingleAsync();
        plan.OutcomeMetricValue.Should().Be(50m);
    }

    [Fact]
    public async Task AnalyticsComputedEvent_WithLowNps_CreatesPlanViaListener()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var dispatcher = env.Services.GetRequiredService<IDomainEventDispatcher>();
        var surveyId = Guid.NewGuid();

        await dispatcher.DispatchAsync(new AnalyticsComputedEvent(
            surveyId,
            "SV-2026-03",
            AnalyticsSegment.Survey,
            Guid.NewGuid(),
            totalSessions: 20,
            completedSessions: 10,
            npsScore: -20m,
            csatScore: 80m,
            cesScore: 70m,
            actorUserId: null));

        var plan = await env.ActionManagementDbContext.ActionPlans
            .Include(p => p.Items)
            .SingleOrDefaultAsync();

        plan.Should().NotBeNull();
        plan!.Source.Should().Be(ActionSource.AnalyticsAlert);
        plan.TriggerMetricType.Should().Be(ActionMetricType.Nps);
        plan.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task AnalyticsComputedEvent_WithFewResponses_DoesNotCreatePlan()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var dispatcher = env.Services.GetRequiredService<IDomainEventDispatcher>();

        await dispatcher.DispatchAsync(new AnalyticsComputedEvent(
            Guid.NewGuid(),
            "SV-2026-04",
            AnalyticsSegment.Survey,
            Guid.NewGuid(),
            totalSessions: 2,
            completedSessions: 2,
            npsScore: -50m,
            csatScore: 10m,
            cesScore: 5m,
            actorUserId: null));

        (await env.ActionManagementDbContext.ActionPlans.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AnalyticsComputedEvent_OrgSegment_DoesNotCreatePlan()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var dispatcher = env.Services.GetRequiredService<IDomainEventDispatcher>();

        await dispatcher.DispatchAsync(new AnalyticsComputedEvent(
            Guid.NewGuid(),
            "SV-2026-05",
            AnalyticsSegment.OrgUnit,
            Guid.NewGuid(),
            totalSessions: 100,
            completedSessions: 100,
            npsScore: -50m,
            csatScore: 10m,
            cesScore: 5m,
            actorUserId: null));

        (await env.ActionManagementDbContext.ActionPlans.CountAsync()).Should().Be(0);
    }

    // --- حریم خصوصی ---------------------------------------------------------------

    // نام فیلدهایی که هرگز نباید روی موجودیت برنامه باشند (حفظ حریم خصوصی).
    private static readonly string[] ForbiddenRespondentFields =
        ["RespondentUserId", "ResponderId", "EmployeeId", "RespondentName"];

    [Fact]
    public async Task Plan_NeverStoresRespondentIdentity()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();

        var result = await service.EnsurePlanFromAnalyticsAlertAsync(new AnalyticsAlertRequest
        {
            SurveyId = Guid.NewGuid(),
            SurveyCode = "SV-P-01",
            SurveyTitle = "نظرسنجی ناشناس",
            SegmentKey = "survey",
            MetricType = ActionMetricType.Nps,
            MetricValue = -5m,
            TargetValue = 0m,
            SuggestedTitle = "برنامه از هشدار"
        });

        result.IsSuccess.Should().BeTrue();

        var plan = await env.ActionManagementDbContext.ActionPlans
            .Include(p => p.Items)
            .SingleAsync();

        // موجودیت برنامه هیچ فیلد پاسخ‌گو ندارد؛ فقط شاخص‌های تجمعی.
        plan.GetType().GetProperties()
            .Select(p => p.Name)
            .Should().NotContain(ForbiddenRespondentFields);

        plan.TriggerMetricValue.Should().Be(-5m);
    }

    // --- آمار ---------------------------------------------------------------------

    [Fact]
    public async Task GetStats_ReturnsScopedSummary()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        var service = env.Services.GetRequiredService<IActionManagementService>();

        var planId = await CreatePlanAsync(env, service, activateImmediately: true);
        await service.CreateItemAsync(planId, new SaveActionItemRequest
        {
            Title = "کار من",
            AssigneeUserId = userId,
            DueDate = DateTime.UtcNow.AddDays(7)
        });

        var stats = await service.GetStatsAsync();

        stats.IsSuccess.Should().BeTrue();
        stats.Value!.TotalActivePlans.Should().Be(1);
        stats.Value.OpenItems.Should().Be(1);
        stats.Value.MyOpenItems.Should().Be(1);
        stats.Value.OverdueItems.Should().Be(0);
    }

    // --- کمک‌کننده‌های آزمون ---------------------------------------------------------

    private static async Task<Guid> CreatePlanAsync(
        TestEnvironment env, IActionManagementService service, bool activateImmediately = false)
    {
        var result = await service.CreatePlanAsync(new SaveActionPlanRequest
        {
            Title = "برنامه‌ی آزمون",
            ActivateImmediately = activateImmediately
        });

        result.IsSuccess.Should().BeTrue();
        return result.Value!.Id;
    }

    private static async Task<(Guid planId, Guid itemId)> CreatePlanWithItemAsync(
        TestEnvironment env, IActionManagementService service, Guid? assigneeId = null)
    {
        var planId = await CreatePlanAsync(env, service, activateImmediately: true);

        var item = await service.CreateItemAsync(planId, new SaveActionItemRequest
        {
            Title = "آیتم آزمون",
            AssigneeUserId = assigneeId,
            DueDate = DateTime.UtcNow.AddDays(7)
        });

        item.IsSuccess.Should().BeTrue();
        return (planId, item.Value!.Id);
    }

    private static async Task SeedReminderInPastAsync(TestEnvironment env, Guid itemId, DateTime remindAt)
    {
        var item = await env.ActionManagementDbContext.ActionItems.SingleAsync(i => i.Id == itemId);
        item.RemindAt = remindAt;
        await env.ActionManagementDbContext.SaveChangesAsync();
    }
}
