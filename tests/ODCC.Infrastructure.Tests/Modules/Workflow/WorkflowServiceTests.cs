using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Workflow.Abstractions;
using ODCC.Application.Modules.Workflow.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Workflow.Entities;
using ODCC.Domain.Modules.Workflow.Enums;
using ODCC.Infrastructure.Modules.Workflow.Persistence;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Workflow;

/// <summary>
/// آزمون‌های ماژول گردش کار: چرخه‌ی عمر تعاریف (پیش‌نویس → فعال → بایگانی)،
/// چرخه‌ی عمر نمونه‌ها (شروع → گذار → تکمیل/لغو)، گذارهای نیازمند تأیید با
/// مجوز مشخص‌شده روی گذار، انقضای خودکار درخواست‌های تأیید و مرز سازمانی
/// (fail-closed) در مشاهده‌ی نمونه‌ها و درخواست‌های تأیید.
///
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ پایگاه‌داده‌ی
/// واقعی یا سرویس بیرونی را لمس نمی‌کنند.
/// </summary>
public class WorkflowServiceTests
{
    private static readonly string[] ManagePermissions =
        [Permissions.Workflows.Manage, Permissions.Workflows.View, Permissions.Workflows.Approve, "surveys.publish"];

    // --- تعاریف ---------------------------------------------------------------

    [Fact]
    public async Task CreateWorkflow_WithStatesAndTransitions_CreatesDraft()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (_, userId) = env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();

        var result = await service.CreateWorkflowAsync(NewSurveyApprovalRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(WorkflowStatus.Draft);
        result.Value.Version.Should().Be(1);
        result.Value.CreatedByUserId.Should().Be(userId);
        result.Value.States.Should().HaveCount(4);
        result.Value.Transitions.Should().HaveCount(3);
        result.Value.AvailableTransitions.Should().ContainKey("draft");
        result.Value.AvailableTransitions["draft"].Should().ContainSingle().Which.Should().Be("publish");

        var workflow = await env.WorkflowDbContext.Workflows
            .Include(w => w.States)
            .Include(w => w.Transitions)
            .SingleAsync();

        workflow.States.Should().HaveCount(4);
        workflow.Transitions.Should().HaveCount(3);
        workflow.States.Count(s => s.IsInitial).Should().Be(1);
        workflow.States.Single(s => s.IsInitial).Code.Should().Be("draft");
    }

    [Fact]
    public async Task CreateWorkflow_DuplicateCode_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();

        await service.CreateWorkflowAsync(NewSurveyApprovalRequest());

        var second = await service.CreateWorkflowAsync(NewSurveyApprovalRequest());

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("workflow_code_exists");
    }

    [Fact]
    public async Task CreateWorkflow_TransitionRequiringApprovalWithoutPermission_Throws()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();

        var request = NewSurveyApprovalRequest();
        request = request with
        {
            Transitions =
            [
                new WorkflowTransitionRequest
                {
                    Code = "publish", Name = "انتشار",
                    FromStateCode = "draft", ToStateCode = "active",
                    RequiresApproval = true
                    // ApproverPermission خالی است → این یک خطای ساختار است.
                }
            ]
        };

        // خطای ساختار (خطای برنامه‌نویس، نه ورودی کاربر) به‌صورت استثنا بالا
        // می‌آید. این توسط اعتبارسنج در مرز API پیشگیری می‌شود.
        var act = async () => await service.CreateWorkflowAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ActivateWorkflow_WithInitialState_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflowId = (await service.CreateWorkflowAsync(NewSurveyApprovalRequest())).Value!.Id;

        var result = await service.ActivateWorkflowAsync(workflowId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(WorkflowStatus.Active);
    }

    [Fact]
    public async Task ActivateWorkflow_WithoutInitialState_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();

        var request = NewSurveyApprovalRequest() with
        {
            States =
            [
                new WorkflowStateRequest { Code = "draft", Name = "پیش‌نویس", IsInitial = false },
                new WorkflowStateRequest { Code = "active", Name = "فعال", IsFinal = false }
            ],
            Transitions = []
        };

        var workflowId = (await service.CreateWorkflowAsync(request)).Value!.Id;

        var result = await service.ActivateWorkflowAsync(workflowId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workflow_no_initial_state");
    }

    [Fact]
    public async Task UpdateWorkflow_WhileRunningInstancesExist_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflow = await CreateAndActivateAsync(service);

        await service.StartInstanceAsync(new StartWorkflowInstanceRequest
        {
            WorkflowCode = workflow.Code,
            EntityType = WorkflowEntityType.Survey,
            EntityId = Guid.NewGuid()
        });

        var result = await service.UpdateWorkflowAsync(workflow.Id, NewSurveyApprovalRequest() with { Name = "تغییر یافته" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workflow_has_running_instances");
    }

    [Fact]
    public async Task ArchiveWorkflow_WhileRunningInstancesExist_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflow = await CreateAndActivateAsync(service);

        await service.StartInstanceAsync(new StartWorkflowInstanceRequest
        {
            WorkflowCode = workflow.Code,
            EntityType = WorkflowEntityType.Survey,
            EntityId = Guid.NewGuid()
        });

        var result = await service.ArchiveWorkflowAsync(workflow.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workflow_has_running_instances");
    }

    [Fact]
    public async Task UpdateWorkflow_WithoutRunningInstances_BumpsVersion()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflow = await CreateAndActivateAsync(service);

        var result = await service.UpdateWorkflowAsync(workflow.Id, NewSurveyApprovalRequest() with { Name = "تغییر یافته" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Version.Should().Be(2);
    }

    // --- نمونه‌ها ---------------------------------------------------------------

    [Fact]
    public async Task StartInstance_OnActiveWorkflow_StartsAtInitialState()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflow = await CreateAndActivateAsync(service);
        var entityId = Guid.NewGuid();

        var result = await service.StartInstanceAsync(new StartWorkflowInstanceRequest
        {
            WorkflowCode = workflow.Code,
            EntityType = WorkflowEntityType.Survey,
            EntityId = entityId
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentStateCode.Should().Be("draft");
        result.Value.Status.Should().Be(WorkflowInstanceState.Running);
        result.Value.WorkflowVersion.Should().Be(workflow.Version);
        result.Value.NextTransitions.Should().ContainSingle(t => t.Code == "publish");

        var instance = await env.WorkflowDbContext.WorkflowInstances.SingleAsync();
        instance.EntityId.Should().Be(entityId);
        // کاربر با دامنه‌ی Company لنگر سازمانی ثبت نمی‌کند (مسیر لنگر خالی است).
        instance.OrgUnitId.Should().BeNull();
        instance.OrgUnitPath.Should().BeNull();
    }

    [Fact]
    public async Task StartInstance_OnDraftWorkflow_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflow = (await service.CreateWorkflowAsync(NewSurveyApprovalRequest())).Value!;

        var result = await service.StartInstanceAsync(new StartWorkflowInstanceRequest
        {
            WorkflowCode = workflow.Code,
            EntityType = WorkflowEntityType.Survey,
            EntityId = Guid.NewGuid()
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workflow_not_active");
    }

    [Fact]
    public async Task StartInstance_TwiceForSameEntity_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflow = await CreateAndActivateAsync(service);
        var entityId = Guid.NewGuid();

        var first = await service.StartInstanceAsync(new StartWorkflowInstanceRequest
        {
            WorkflowCode = workflow.Code, EntityType = WorkflowEntityType.Survey, EntityId = entityId
        });

        first.IsSuccess.Should().BeTrue();

        var second = await service.StartInstanceAsync(new StartWorkflowInstanceRequest
        {
            WorkflowCode = workflow.Code, EntityType = WorkflowEntityType.Survey, EntityId = entityId
        });

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("workflow_instance_already_running");
    }

    [Fact]
    public async Task TransitionInstance_NotFromCurrentState_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var (workflow, instance) = await StartInstanceAsync(env, service);

        // «close» از «active» می‌رود، ولی نمونه در «draft» است.
        var result = await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "close"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workflow_transition_not_allowed");
    }

    [Fact]
    public async Task TransitionInstance_UnknownTransition_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var (_, instance) = await StartInstanceAsync(env, service);

        var result = await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "nonexistent"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workflow_transition_not_found");
    }

    [Fact]
    public async Task TransitionInstance_ToFinalState_CompletesInstance()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var (workflow, instance) = await StartInstanceAsync(env, service);

        // «publish» نیازمند تأیید است → ابتدا آن را تأیید می‌کنیم.
        var requested = await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "publish"
        });

        var approved = await service.ApproveAsync(
            requested.Value!.PendingApproval!.Id, new DecideWorkflowApprovalRequest());

        approved.IsSuccess.Should().BeTrue();
        approved.Value!.Status.Should().Be(ApprovalStatus.Approved);

        // نمونه حالا در «active» است.
        var active = await service.GetInstanceByIdAsync(instance.Id);
        active.Value!.CurrentStateCode.Should().Be("active");

        var close = await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "close"
        });

        close.IsSuccess.Should().BeTrue();
        close.Value!.CurrentStateCode.Should().Be("closed");
        close.Value.Status.Should().Be(WorkflowInstanceState.Completed);
        close.Value.CompletedAt.Should().NotBeNull();

        var stored = await env.WorkflowDbContext.WorkflowInstances.SingleAsync();
        stored.Status.Should().Be(WorkflowInstanceState.Completed);
        stored.TransitionCount.Should().Be(2);
    }

    // --- تأییدها ----------------------------------------------------------------

    [Fact]
    public async Task Transition_RequiringApproval_CreatesPendingRequestAndStaysInPlace()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var (workflow, instance) = await StartInstanceAsync(env, service);

        // «publish» در این تعریف نیازمند تأیید با مجوز surveys.publish است.
        var result = await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "publish",
            ApprovalExpiresAtUtc = DateTime.UtcNow.AddHours(24)
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.CurrentStateCode.Should().Be("draft");
        result.Value.PendingApproval.Should().NotBeNull();
        result.Value.PendingApproval!.ToStateCode.Should().Be("active");
        result.Value.PendingApproval.ExpiresAt.Should().NotBeNull();

        var approval = await env.WorkflowDbContext.WorkflowApprovalRequests.SingleAsync();
        approval.Status.Should().Be(ApprovalStatus.Pending);
        approval.ApproverPermission.Should().Be("surveys.publish");
    }

    [Fact]
    public async Task Transition_WhileApprovalPending_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var (workflow, instance) = await StartInstanceAsync(env, service);

        await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "publish"
        });

        var second = await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "publish"
        });

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("workflow_approval_pending");
    }

    [Fact]
    public async Task Approve_WithTransitionPermission_TransitionsInstance()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var (workflow, instance) = await StartInstanceAsync(env, service);

        var transitioned = await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "publish"
        });

        var approvalId = transitioned.Value!.PendingApproval!.Id;

        var result = await service.ApproveAsync(approvalId, new DecideWorkflowApprovalRequest { Note = "تأیید شد" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ApprovalStatus.Approved);
        result.Value.DecisionNote.Should().Be("تأیید شد");

        var stored = await env.WorkflowDbContext.WorkflowInstances.SingleAsync();
        stored.CurrentStateCode.Should().Be("active");
        stored.Status.Should().Be(WorkflowInstanceState.Running);
    }

    [Fact]
    public async Task Approve_WithoutTransitionPermission_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var (workflow, instance) = await StartInstanceAsync(env, service);

        var transitioned = await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "publish"
        });

        var approvalId = transitioned.Value!.PendingApproval!.Id;

        // کاربری WITHOUT مجوز surveys.publish.
        env.SetCurrentUser(orgUnitId: null, DataScope.Company,
            userName: "approver", Permissions.Workflows.Approve);

        var result = await service.ApproveAsync(approvalId, new DecideWorkflowApprovalRequest());

        result.IsFailure.Should().BeTrue();
        // پیام خطا عمداً «یافت نشد» است تا وجود درخواست فاش نشود.
        result.Error.Code.Should().Be("workflow_approval_not_found");

        var stored = await env.WorkflowDbContext.WorkflowApprovalRequests.SingleAsync();
        stored.Status.Should().Be(ApprovalStatus.Pending);
    }

    [Fact]
    public async Task Reject_WithTransitionPermission_KeepsInstanceInPlace()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var (workflow, instance) = await StartInstanceAsync(env, service);

        var transitioned = await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "publish"
        });

        var approvalId = transitioned.Value!.PendingApproval!.Id;

        var result = await service.RejectAsync(approvalId, new DecideWorkflowApprovalRequest { Note = "رد شد" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(ApprovalStatus.Rejected);

        var stored = await env.WorkflowDbContext.WorkflowInstances.SingleAsync();
        stored.CurrentStateCode.Should().Be("draft");
    }

    [Fact]
    public async Task Decide_Twice_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var (workflow, instance) = await StartInstanceAsync(env, service);

        var transitioned = await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "publish"
        });

        var approvalId = transitioned.Value!.PendingApproval!.Id;

        await service.ApproveAsync(approvalId, new DecideWorkflowApprovalRequest());

        var second = await service.ApproveAsync(approvalId, new DecideWorkflowApprovalRequest());

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("workflow_approval_already_decided");
    }

    [Fact]
    public async Task ExpireDueApprovals_MarksExpiredAndDoesNotTransition()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var (workflow, instance) = await StartInstanceAsync(env, service);

        await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "publish",
            ApprovalExpiresAtUtc = DateTime.UtcNow.AddHours(-1)
        });

        var expired = await service.ExpireDueApprovalsAsync(DateTime.UtcNow);

        expired.Should().Be(1);

        var approval = await env.WorkflowDbContext.WorkflowApprovalRequests.SingleAsync();
        approval.Status.Should().Be(ApprovalStatus.Expired);

        var stored = await env.WorkflowDbContext.WorkflowInstances.SingleAsync();
        stored.CurrentStateCode.Should().Be("draft");
    }

    [Fact]
    public async Task CancelInstance_CancelsPendingApproval()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var (workflow, instance) = await StartInstanceAsync(env, service);

        await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "publish"
        });

        var result = await service.CancelInstanceAsync(instance.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(WorkflowInstanceState.Cancelled);
        result.Value.CancelledAt.Should().NotBeNull();

        var approval = await env.WorkflowDbContext.WorkflowApprovalRequests.SingleAsync();
        approval.Status.Should().Be(ApprovalStatus.Cancelled);
    }

    // --- مرز سازمانی -------------------------------------------------------------

    [Fact]
    public async Task StartInstance_SnapshotsOrgScopeOfStarter()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (company, division, department, team, otherDivision, otherDepartment) = await env.SeedOrgHierarchyAsync();

        env.SetCurrentUser(division, DataScope.Division, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflow = await CreateAndActivateAsync(service);
        var (_, instance) = await StartInstanceAsync(env, service, workflow);

        var stored = await env.WorkflowDbContext.WorkflowInstances.SingleAsync();
        stored.OrgUnitPath.Should().NotBeNull();
        stored.OrgUnitPath.Should().StartWith("/hq/fin");
        stored.OrgUnitId.Should().Be(division);
    }

    [Fact]
    public async Task GetInstance_OutsideOrgScope_ReturnsNotFound()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (company, division, department, team, otherDivision, otherDepartment) = await env.SeedOrgHierarchyAsync();

        // کاربر در شاخه‌ی «fin» نمونه را شروع می‌کند.
        env.SetCurrentUser(division, DataScope.Division, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflow = await CreateAndActivateAsync(service);
        var (_, instance) = await StartInstanceAsync(env, service, workflow);

        // کاربر در شاخه‌ی دیگر («ops») نباید نمونه را ببیند.
        env.SetCurrentUser(otherDivision, DataScope.Division, permissions: ManagePermissions);

        var result = await service.GetInstanceByIdAsync(instance.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("workflow_instance_not_found");
    }

    [Fact]
    public async Task GetInstance_InsideOrgScope_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (company, division, department, team, otherDivision, otherDepartment) = await env.SeedOrgHierarchyAsync();

        // نمونه از یک واحد پایین‌تر (دپارتمان) شروع می‌شود.
        env.SetCurrentUser(department, DataScope.Department, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflow = await CreateAndActivateAsync(service);
        var (_, instance) = await StartInstanceAsync(env, service, workflow);

        // کاربر در واحد بالاتر (دایرکتی) باید نمونه‌ی زیرمجموعه را ببیند.
        env.SetCurrentUser(division, DataScope.Division, permissions: ManagePermissions);

        var result = await service.GetInstanceByIdAsync(instance.Id);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SearchInstances_AppliesOrgScope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (company, division, department, team, otherDivision, otherDepartment) = await env.SeedOrgHierarchyAsync();

        env.SetCurrentUser(division, DataScope.Division, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflow = await CreateAndActivateAsync(service);
        await StartInstanceAsync(env, service, workflow);

        // کاربر در شاخه‌ی دیگر نباید نمونه‌ای ببیند (fail-closed).
        env.SetCurrentUser(otherDivision, DataScope.Division, permissions: ManagePermissions);

        var result = await service.SearchInstancesAsync(new WorkflowInstanceSearchRequest());

        result.TotalCount.Should().Be(0);

        // ولی کاربر در شاخه‌ی خودش می‌بیند.
        env.SetCurrentUser(division, DataScope.Division, permissions: ManagePermissions);

        var own = await service.SearchInstancesAsync(new WorkflowInstanceSearchRequest());

        own.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchApprovals_AppliesOrgScope()
    {
        await using var env = await TestEnvironment.CreateAsync();
        var (company, division, department, team, otherDivision, otherDepartment) = await env.SeedOrgHierarchyAsync();

        env.SetCurrentUser(division, DataScope.Division, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflow = await CreateAndActivateAsync(service);
        var (_, instance) = await StartInstanceAsync(env, service, workflow);

        await service.TransitionInstanceAsync(instance.Id, new TransitionWorkflowInstanceRequest
        {
            TransitionCode = "publish"
        });

        // کاربر در شاخه‌ی دیگر نباید درخواست تأیید را ببیند.
        env.SetCurrentUser(otherDivision, DataScope.Division,
            userName: "approver", Permissions.Workflows.Approve);

        var result = await service.SearchApprovalsAsync(new WorkflowApprovalSearchRequest { PendingOnly = true });

        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetStats_ReflectsState()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IWorkflowService>();
        var workflow = await CreateAndActivateAsync(service);
        await StartInstanceAsync(env, service, workflow);

        var stats = await service.GetStatsAsync();

        stats.IsSuccess.Should().BeTrue();
        stats.Value!.ActiveWorkflows.Should().Be(1);
        stats.Value.RunningInstances.Should().Be(1);
        stats.Value.PendingApprovals.Should().Be(0);
    }

    // --- کمک‌ها -------------------------------------------------------------------

    private static SaveWorkflowRequest NewSurveyApprovalRequest() => new()
    {
        Name = "تأیید انتشار نظرسنجی",
        Code = "survey-approval",
        Description = "گردش کار انتشار و بستن نظرسنجی",
        EntityType = WorkflowEntityType.Survey,
        States =
        [
            new WorkflowStateRequest { Code = "draft", Name = "پیش‌نویس", IsInitial = true },
            new WorkflowStateRequest { Code = "active", Name = "فعال" },
            new WorkflowStateRequest { Code = "paused", Name = "متوقف‌شده" },
            new WorkflowStateRequest { Code = "closed", Name = "بسته‌شده", IsFinal = true }
        ],
        Transitions =
        [
            new WorkflowTransitionRequest
            {
                Code = "publish", Name = "انتشار",
                FromStateCode = "draft", ToStateCode = "active",
                RequiresApproval = true, ApproverPermission = "surveys.publish"
            },
            new WorkflowTransitionRequest
            {
                Code = "pause", Name = "توقف",
                FromStateCode = "active", ToStateCode = "paused"
            },
            new WorkflowTransitionRequest
            {
                Code = "close", Name = "بستن",
                FromStateCode = "active", ToStateCode = "closed"
            }
        ]
    };

    private static async Task<WorkflowDto> CreateAndActivateAsync(IWorkflowService service)
    {
        var created = await service.CreateWorkflowAsync(NewSurveyApprovalRequest());
        created.IsSuccess.Should().BeTrue($"create should succeed; got {created.Error.Code}: {created.Error.Message}");
        var activated = await service.ActivateWorkflowAsync(created.Value!.Id);
        activated.IsSuccess.Should().BeTrue($"activate should succeed; got {activated.Error.Code}: {activated.Error.Message}");
        return activated.Value!;
    }

    private static async Task<(WorkflowDto Workflow, WorkflowInstanceDto Instance)> StartInstanceAsync(
        TestEnvironment env, IWorkflowService service, WorkflowDto? workflow = null)
    {
        // اگر گردش کاری از قبل ساخته/فعال شده، از آن استفاده می‌کنیم تا یک کد
        // تکراری ایجاد نشود.
        workflow ??= await CreateAndActivateAsync(service);

        var instance = await service.StartInstanceAsync(new StartWorkflowInstanceRequest
        {
            WorkflowCode = workflow.Code,
            EntityType = WorkflowEntityType.Survey,
            EntityId = Guid.NewGuid()
        });

        return (workflow, instance.Value!);
    }
}
