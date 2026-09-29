using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Workflow.Abstractions;
using ODCC.Application.Modules.Workflow.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Workflow.Entities;
using ODCC.Domain.Modules.Workflow.Enums;
using ODCC.Domain.Modules.Workflow.Events;
using ODCC.Infrastructure.Modules.Workflow.Scheduled;

namespace ODCC.Infrastructure.Modules.Workflow.Services;

/// <summary>
/// سرویس گردش کار.
///
/// <b>مجوزها:</b> مدیریت تعاریف نیازمند <c>workflows.manage</c> است (در کنترلر
/// اعمال می‌شود). تأیید گذارها نیازمند مجوزی است که روی گذار مشخص شده؛ این
/// بررسی در این سرویس اعمال می‌شود (نه در کنترلر) تا هیچ مسیری دور نزند.
///
/// <b>مرز سازمانی (fail-closed):</b> نمونه‌ها و درخواست‌های تأیید فقط در صورتی
/// قابل مشاهده/تغییر هستند که داخل دامنه‌ی سازمانی کاربر باشند. مسیر سازمانی
/// نمونه در زمان شروع snapshot می‌شود و هرگز از کلاینت نمی‌آید. کاربران با
/// دامنه‌ی <c>Company</c> محدودیتی ندارند؛ سایر دامنه‌ها در صورت نبودن لنگر
/// معتبر هیچ داده‌ای نمی‌بینند.
///
/// <b>طراحی:</b> گذارها توسط ماشین وضعیت سمت سرور اعتبارسنجی می‌شوند تا
/// کلاینت نتواند گذار غیرمجاز بزند. گذارهای نیازمند تأیید تا تصمیم‌گیری در
/// وضعیت قبلی متوقف می‌شوند.
/// </summary>
public sealed class WorkflowService(
    IWorkflowRepository workflowRepository,
    IWorkflowInstanceRepository instanceRepository,
    IWorkflowApprovalRepository approvalRepository,
    ICurrentUserService currentUserService,
    IOrgScopeProvider orgScopeProvider,
    IWorkflowUnitOfWork unitOfWork,
    IOptions<WorkflowSchedulerOptions> schedulerOptions,
    ILogger<WorkflowService> logger) : IWorkflowService
{
    private readonly IWorkflowRepository _workflowRepository = workflowRepository;
    private readonly IWorkflowInstanceRepository _instanceRepository = instanceRepository;
    private readonly IWorkflowApprovalRepository _approvalRepository = approvalRepository;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IOrgScopeProvider _orgScopeProvider = orgScopeProvider;
    private readonly IWorkflowUnitOfWork _unitOfWork = unitOfWork;
    private readonly WorkflowSchedulerOptions _schedulerOptions = schedulerOptions.Value;
    private readonly ILogger<WorkflowService> _logger = logger;

    private static readonly Action<ILogger, Guid, Exception> TransitionFailed = LoggerMessage.Define<Guid>(
        LogLevel.Error,
        new EventId(1, "WorkflowTransitionFailed"),
        "انجام گذار روی نمونه‌ی گردش کار {InstanceId} شکست خورد.");

    // --- تعاریف ----------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<PagedResult<WorkflowDto>> SearchWorkflowsAsync(
        WorkflowSearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _workflowRepository.CountAsync(request, ct);
        var workflows = await _workflowRepository.SearchAsync(request, ct);

        return new PagedResult<WorkflowDto>
        {
            Items = workflows.Select(ToWorkflowDto).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowDto>> GetWorkflowByIdAsync(Guid id, CancellationToken ct = default)
    {
        var workflow = await _workflowRepository.GetByIdAsync(id, ct);

        if (workflow is null)
        {
            return Result.Failure<WorkflowDto>("workflow_not_found", "تعریف گردش کار یافت نشد.");
        }

        return Result.Success(ToWorkflowDto(workflow));
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowDto>> FindActiveWorkflowAsync(string code, CancellationToken ct = default)
    {
        var workflow = await _workflowRepository.FindActiveByCodeAsync(code, ct);

        if (workflow is null)
        {
            return Result.Failure<WorkflowDto>("workflow_not_active", "گردش کار فعالی با این کد وجود ندارد.");
        }

        return Result.Success(ToWorkflowDto(workflow));
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowDto>> CreateWorkflowAsync(SaveWorkflowRequest request, CancellationToken ct = default)
    {
        var existing = await _workflowRepository.FindByCodeAsync(request.Code, ct);

        if (existing is not null)
        {
            return Result.Failure<WorkflowDto>("workflow_code_exists", "گردش کاری با این کد از قبل وجود دارد.");
        }

        var workflow = new WorkflowDefinition
        {
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            Description = request.Description,
            EntityType = request.EntityType,
            Status = WorkflowStatus.Draft,
            Version = 1,
            CreatedByUserId = _currentUserService.UserId,
            CreatedByUserName = _currentUserService.UserName
        };

        ApplyStates(workflow, request.States);
        ApplyTransitions(workflow, request.Transitions);

        if (request.ActivateImmediately)
        {
            workflow.Activate();
        }

        workflow.RaiseCreatedEvent();

        await _workflowRepository.AddAsync(workflow, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToWorkflowDto(workflow));
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowDto>> UpdateWorkflowAsync(Guid id, SaveWorkflowRequest request, CancellationToken ct = default)
    {
        var workflow = await _workflowRepository.GetByIdAsync(id, ct);

        if (workflow is null)
        {
            return Result.Failure<WorkflowDto>("workflow_not_found", "تعریف گردش کار یافت نشد.");
        }

        if (workflow.Status == WorkflowStatus.Archived)
        {
            return Result.Failure<WorkflowDto>("workflow_archived", "گردش کار بایگانی‌شده قابل ویرایش نیست.");
        }

        // تغییر کد یک تعریف فعال خطرناک است (نمونه‌های جدید از کد استفاده می‌کنند).
        if (workflow.Status == WorkflowStatus.Active
            && !string.Equals(workflow.Code, request.Code.Trim(), StringComparison.Ordinal))
        {
            return Result.Failure<WorkflowDto>("workflow_code_immutable", "کد یک گردش کار فعال قابل تغییر نیست.");
        }

        var conflict = await _workflowRepository.FindByCodeAsync(request.Code, ct);

        if (conflict is not null && conflict.Id != workflow.Id)
        {
            return Result.Failure<WorkflowDto>("workflow_code_exists", "گردش کاری با این کد از قبل وجود دارد.");
        }

        // اگر تعریف فعال است و نمونه‌های در حال اجرا دارد، تغییر ساختار
        // حالت‌ها می‌تواند نمونه‌ها را بشکند. نسخه‌ی جدید الزامی است.
        var hasRunningInstances = await _instanceRepository.CountAsync(
            new WorkflowInstanceSearchRequest { WorkflowId = workflow.Id, RunningOnly = true }, scope: null, ct) > 0;

        if (workflow.Status == WorkflowStatus.Active && hasRunningInstances)
        {
            return Result.Failure<WorkflowDto>(
                "workflow_has_running_instances",
                "این گردش کار نمونه‌های در حال اجرا دارد و ساختار آن قابل تغییر نیست. ابتدا نمونه‌ها را تکمیل کنید.");
        }

        workflow.Name = request.Name.Trim();
        workflow.Code = request.Code.Trim();
        workflow.Description = request.Description;
        workflow.EntityType = request.EntityType;

        // جایگزینی ساختار تعریف. حذف صریح فرزندان قدیمی ضروری است: گذارها با
        // کلید خارجی غیرنال و DeleteBehavior.Restrict به وضعیت‌ها اشاره می‌کنند
        // و اتکا به Collection.Clear در زمان ذخیره‌سازی خطا می‌دهد.
        var newStates = BuildStates(request.States);
        var stateIdByCode = newStates.ToDictionary(s => s.Code, s => s.Id, StringComparer.Ordinal);
        var newTransitions = BuildTransitions(request.Transitions, stateIdByCode);

        // اعتبارسنجی یکپارچه‌سازی: گذارها باید به وضعیت‌های جدید اشاره کنند.
        // اگر کد وضعیت مبدا/مقصد در تعریف نباشد، شناسه‌ی آن پیدا نمی‌شود.
        var dangling = newTransitions.FirstOrDefault(t =>
            t.FromStateId == Guid.Empty || t.ToStateId == Guid.Empty);

        if (dangling is not null)
        {
            return Result.Failure<WorkflowDto>("workflow_invalid_transition",
                $"گذار «{dangling.Code}» به وضعیتی اشاره می‌کند که در این تعریف وجود ندارد.");
        }

        _workflowRepository.ReplaceStructure(workflow, newStates, newTransitions);

        // ساختار تغییر کرد → نسخه‌ی جدید. نمونه‌ها نسخه را در زمان شروع snapshot
        // می‌کنند تا تاریخچه قابل تفسیر بماند. ویرایش زمانی که نمونه‌ی در حال
        // اجرا وجود دارد مسدود است، پس اینجا امن است.
        workflow.Version++;

        workflow.RaiseUpdatedEvent();

        _workflowRepository.Update(workflow);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToWorkflowDto(workflow));
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowDto>> ActivateWorkflowAsync(Guid id, CancellationToken ct = default)
    {
        var workflow = await _workflowRepository.GetByIdAsync(id, ct);

        if (workflow is null)
        {
            return Result.Failure<WorkflowDto>("workflow_not_found", "تعریف گردش کار یافت نشد.");
        }

        if (workflow.Status == WorkflowStatus.Archived)
        {
            return Result.Failure<WorkflowDto>("workflow_archived", "گردش کار بایگانی‌شده قابل فعال‌سازی نیست.");
        }

        if (workflow.InitialState is null)
        {
            return Result.Failure<WorkflowDto>("workflow_no_initial_state", "گردش کار باید حداقل یک وضعیت اولیه داشته باشد.");
        }

        workflow.Activate();
        workflow.RaiseActivatedEvent();

        _workflowRepository.Update(workflow);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToWorkflowDto(workflow));
    }

    /// <inheritdoc/>
    public async Task<Result> ArchiveWorkflowAsync(Guid id, CancellationToken ct = default)
    {
        var workflow = await _workflowRepository.GetByIdAsync(id, ct);

        if (workflow is null)
        {
            return Result.Failure("workflow_not_found", "تعریف گردش کار یافت نشد.");
        }

        var hasRunningInstances = await _instanceRepository.CountAsync(
            new WorkflowInstanceSearchRequest { WorkflowId = workflow.Id, RunningOnly = true }, scope: null, ct) > 0;

        if (hasRunningInstances)
        {
            return Result.Failure("workflow_has_running_instances",
                "این گردش کار نمونه‌های در حال اجرا دارد و قابل بایگانی نیست.");
        }

        workflow.Archive();
        workflow.RaiseArchivedEvent();

        _workflowRepository.Update(workflow);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // --- نمونه‌ها --------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<PagedResult<WorkflowInstanceDto>> SearchInstancesAsync(
        WorkflowInstanceSearchRequest request, CancellationToken ct = default)
    {
        // دامنه‌ی سازمانی کاربر: فیلتر در کوئری پایگاه داده اعمال می‌شود.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        var totalCount = await _instanceRepository.CountAsync(request, scope, ct);
        var instances = await _instanceRepository.SearchAsync(request, scope, ct);

        // بارگذاری تعاریف برای محاسبه‌ی گذارهای بعدی و درخواست‌های باز.
        var workflowIds = instances.Select(i => i.WorkflowId).Distinct().ToList();
        var workflows = new Dictionary<Guid, WorkflowDefinition>();

        foreach (var workflowId in workflowIds)
        {
            var workflow = await _workflowRepository.GetByIdAsync(workflowId, ct);
            if (workflow is not null)
            {
                workflows[workflow.Id] = workflow;
            }
        }

        var dtos = new List<WorkflowInstanceDto>();

        foreach (var instance in instances)
        {
            var workflow = workflows.GetValueOrDefault(instance.WorkflowId);
            var pendingApproval = await _approvalRepository.FindPendingByInstanceAsync(instance.Id, ct);
            var actorPermissions = _currentUserService.Permissions;

            dtos.Add(ToInstanceDto(instance, workflow, pendingApproval, actorPermissions));
        }

        return new PagedResult<WorkflowInstanceDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowInstanceDto>> GetInstanceByIdAsync(Guid id, CancellationToken ct = default)
    {
        var instance = await _instanceRepository.GetByIdAsync(id, ct);

        if (instance is null)
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_instance_not_found", "نمونه‌ی گردش کار یافت نشد.");
        }

        // مرز سازمانی: کاربر فقط نمونه‌های داخل دامنه‌ی خودش را می‌بیند.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (!scope.CanAccess(instance.OrgUnitId, instance.OrgUnitPath))
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_instance_not_found", "نمونه‌ی گردش کار یافت نشد.");
        }

        var workflow = await _workflowRepository.GetByIdAsync(instance.WorkflowId, ct);
        var pendingApproval = await _approvalRepository.FindPendingByInstanceAsync(instance.Id, ct);

        return Result.Success(ToInstanceDto(instance, workflow, pendingApproval, _currentUserService.Permissions));
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowInstanceDto>> FindActiveInstanceAsync(
        WorkflowEntityType entityType, Guid entityId, CancellationToken ct = default)
    {
        var instance = await _instanceRepository.FindActiveAsync(entityType, entityId, ct);

        if (instance is null)
        {
            return Result.Failure<WorkflowInstanceDto>(
                "workflow_instance_not_found", "نمونه‌ی گردش کار فعالی برای این موجودیت وجود ندارد.");
        }

        // مرز سازمانی (fail-closed).
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (!scope.CanAccess(instance.OrgUnitId, instance.OrgUnitPath))
        {
            return Result.Failure<WorkflowInstanceDto>(
                "workflow_instance_not_found", "نمونه‌ی گردش کار فعالی برای این موجودیت وجود ندارد.");
        }

        var workflow = await _workflowRepository.GetByIdAsync(instance.WorkflowId, ct);
        var pendingApproval = await _approvalRepository.FindPendingByInstanceAsync(instance.Id, ct);

        return Result.Success(ToInstanceDto(instance, workflow, pendingApproval, _currentUserService.Permissions));
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowInstanceDto>> StartInstanceAsync(
        StartWorkflowInstanceRequest request, CancellationToken ct = default)
    {
        var workflow = await _workflowRepository.FindActiveByCodeAsync(request.WorkflowCode, ct);

        if (workflow is null)
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_not_active", "گردش کار فعالی با این کد وجود ندارد.");
        }

        if (workflow.InitialState is null)
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_no_initial_state", "گردش کار فاقد وضعیت اولیه است.");
        }

        if (workflow.EntityType != request.EntityType)
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_entity_type_mismatch",
                "نوع موجودیت با نوع تعریف گردش کار همخوانی ندارد.");
        }

        // فقط یک نمونه‌ی فعال به ازای هر موجودیت.
        var existing = await _instanceRepository.FindActiveAsync(request.EntityType, request.EntityId, ct);

        if (existing is not null)
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_instance_already_running",
                "این موجودیت از قبل یک نمونه‌ی گردش کار در حال اجرا دارد.");
        }

        var instance = new WorkflowInstance
        {
            WorkflowId = workflow.Id,
            WorkflowCode = workflow.Code,
            WorkflowVersion = workflow.Version,
            EntityType = request.EntityType,
            EntityId = request.EntityId,
            CurrentStateCode = workflow.InitialState.Code,
            Status = WorkflowInstanceState.Running,
            StartedByUserId = _currentUserService.UserId,
            StartedByUserName = _currentUserService.UserName,
            ContextJson = request.ContextJson
        };

        // snapshot مرز سازمانی برای فیلتر کردن بعدی (fail-closed). مسیر هرگز
        // از کلاینت نمی‌آید؛ لنگر کاربر جاری در زمان شروع ثبت می‌شود.
        var startScope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        instance.OrgUnitId = startScope.AnchorOrgUnitId;
        instance.OrgUnitPath = startScope.AnchorPath;

        instance.RaiseStartedEvent();

        await _instanceRepository.AddAsync(instance, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToInstanceDto(instance, workflow, null, _currentUserService.Permissions));
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowInstanceDto>> TransitionInstanceAsync(
        Guid instanceId, TransitionWorkflowInstanceRequest request, CancellationToken ct = default)
    {
        var instance = await _instanceRepository.GetByIdAsync(instanceId, ct);

        if (instance is null)
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_instance_not_found", "نمونه‌ی گردش کار یافت نشد.");
        }

        if (!instance.IsRunning)
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_instance_not_running",
                "این نمونه تکمیل/لغو شده و قابل گذار نیست.");
        }

        // مرز سازمانی: کاربر فقط نمونه‌های داخل دامنه‌ی خودش را تغییر می‌دهد.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (!scope.CanAccess(instance.OrgUnitId, instance.OrgUnitPath))
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_instance_not_found", "نمونه‌ی گردش کار یافت نشد.");
        }

        var workflow = await _workflowRepository.GetByIdAsync(instance.WorkflowId, ct);

        if (workflow is null)
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_not_found", "تعریف گردش کار یافت نشد.");
        }

        var transition = workflow.FindTransition(request.TransitionCode);

        if (transition is null)
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_transition_not_found",
                $"گذاری با کد «{request.TransitionCode}» در این گردش کار وجود ندارد.");
        }

        var fromState = workflow.States.FirstOrDefault(s => s.Id == transition.FromStateId);
        var toState = workflow.States.FirstOrDefault(s => s.Id == transition.ToStateId);

        if (fromState is null || toState is null)
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_invalid_transition", "گذار نامعتبر است.");
        }

        // ماشین وضعیت: گذار باید از وضعیت جاری شروع شود.
        if (!string.Equals(instance.CurrentStateCode, fromState.Code, StringComparison.Ordinal))
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_transition_not_allowed",
                $"از وضعیت «{instance.CurrentStateCode}» نمی‌توان گذار «{transition.Code}» را انجام داد.");
        }

        // اگر گذار نیازمند تأیید است، درخواست تأیید ایجاد می‌کنیم و نمونه در
        // وضعیت قبلی می‌ماند تا تصمیم گرفته شود.
        if (transition.RequiresApproval)
        {
            var existingApproval = await _approvalRepository.FindPendingByInstanceAsync(instance.Id, ct);

            if (existingApproval is not null)
            {
                return Result.Failure<WorkflowInstanceDto>("workflow_approval_pending",
                    "این نمونه یک درخواست تأیید در انتظار دارد. ابتدا باید تصمیم گرفته شود.");
            }

            var approval = new WorkflowApprovalRequest
            {
                InstanceId = instance.Id,
                TransitionId = transition.Id,
                TransitionCode = transition.Code,
                FromStateCode = fromState.Code,
                ToStateCode = toState.Code,
                ApproverPermission = transition.ApproverPermission,
                Status = ApprovalStatus.Pending,
                RequestedById = _currentUserService.UserId,
                DecisionNote = request.Note,
                ExpiresAt = request.ApprovalExpiresAtUtc
            };

            approval.RaiseRequestedEvent();

            await _approvalRepository.AddAsync(approval, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // نمونه در وضعیت قبلی می‌ماند — درخواست تأیید برمی‌گردد.
            return Result.Success(ToInstanceDto(instance, workflow, approval, _currentUserService.Permissions));
        }

        // گذار مستقیم (بدون تأیید).
        var wasFinal = toState.IsFinal;
        var previousCode = instance.CurrentStateCode;

        instance.TransitionTo(toState.Code, wasFinal);
        instance.RaiseTransitionedEvent(previousCode, toState.Code, _currentUserService.UserId, approved: false);

        if (wasFinal)
        {
            instance.RaiseCompletedEvent();
        }

        _instanceRepository.Update(instance);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToInstanceDto(instance, workflow, null, _currentUserService.Permissions));
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowInstanceDto>> CancelInstanceAsync(Guid instanceId, CancellationToken ct = default)
    {
        var instance = await _instanceRepository.GetByIdAsync(instanceId, ct);

        if (instance is null)
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_instance_not_found", "نمونه‌ی گردش کار یافت نشد.");
        }

        if (!instance.IsRunning)
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_instance_not_running",
                "این نمونه از قبل تکمیل/لغو شده است.");
        }

        // مرز سازمانی: لغو نمونه خارج از دامنه مجاز نیست.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (!scope.CanAccess(instance.OrgUnitId, instance.OrgUnitPath))
        {
            return Result.Failure<WorkflowInstanceDto>("workflow_instance_not_found", "نمونه‌ی گردش کار یافت نشد.");
        }

        // لغو درخواست تأیید باز.
        var pendingApproval = await _approvalRepository.FindPendingByInstanceAsync(instance.Id, ct);

        if (pendingApproval is not null)
        {
            pendingApproval.Cancel();
            _approvalRepository.Update(pendingApproval);
        }

        instance.Cancel();
        instance.RaiseCancelledEvent();

        _instanceRepository.Update(instance);
        await _unitOfWork.SaveChangesAsync(ct);

        var workflow = await _workflowRepository.GetByIdAsync(instance.WorkflowId, ct);

        return Result.Success(ToInstanceDto(instance, workflow, null, _currentUserService.Permissions));
    }

    // --- تأییدها ---------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<PagedResult<WorkflowApprovalRequestDto>> SearchApprovalsAsync(
        WorkflowApprovalSearchRequest request, CancellationToken ct = default)
    {
        // دامنه‌ی سازمانی کاربر: درخواست‌هایی که نمونه‌شان داخل دامنه است.
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        var totalCount = await _approvalRepository.CountAsync(request, scope, ct);
        var approvals = await _approvalRepository.SearchAsync(request, scope, ct);

        return new PagedResult<WorkflowApprovalRequestDto>
        {
            Items = approvals.Select(a => ToApprovalDto(a, CanDecide(a))).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowApprovalRequestDto>> GetApprovalByIdAsync(Guid id, CancellationToken ct = default)
    {
        var approval = await _approvalRepository.GetByIdAsync(id, ct);

        if (approval is null)
        {
            return Result.Failure<WorkflowApprovalRequestDto>("workflow_approval_not_found", "درخواست تأیید یافت نشد.");
        }

        // مرز سازمانی از طریق نمونه‌ی مرتبط (fail-closed).
        if (!await CanAccessApprovalAsync(approval, ct))
        {
            return Result.Failure<WorkflowApprovalRequestDto>("workflow_approval_not_found", "درخواست تأیید یافت نشد.");
        }

        return Result.Success(ToApprovalDto(approval, CanDecide(approval)));
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowApprovalRequestDto>> ApproveAsync(
        Guid approvalRequestId, DecideWorkflowApprovalRequest request, CancellationToken ct = default)
    {
        return await DecideAsync(approvalRequestId, approved: true, request, ct);
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowApprovalRequestDto>> RejectAsync(
        Guid approvalRequestId, DecideWorkflowApprovalRequest request, CancellationToken ct = default)
    {
        return await DecideAsync(approvalRequestId, approved: false, request, ct);
    }

    /// <inheritdoc/>
    public async Task<int> ExpireDueApprovalsAsync(DateTime asOf, CancellationToken ct = default)
    {
        // سقف پردازش از پیکربندی (Workflows:MaxPerCycle).
        var maxPerCycle = Math.Clamp(_schedulerOptions.MaxPerCycle, 1, 500);

        var expired = await _approvalRepository.ListExpiredAsync(asOf, maxPerCycle, ct);

        foreach (var approval in expired)
        {
            approval.Expire();
            approval.RaiseDomainEvent(new WorkflowApprovalExpiredEvent(approval.Id, approval.InstanceId, approval.TransitionCode, approval.DecidedAt));
            _approvalRepository.Update(approval);
        }

        if (expired.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return expired.Count;
    }

    /// <inheritdoc/>
    public async Task<Result<WorkflowStatsDto>> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = new WorkflowStatsDto
        {
            TotalWorkflows = await _workflowRepository.CountAsync(ct),
            ActiveWorkflows = await _workflowRepository.CountActiveAsync(ct),
            RunningInstances = await _instanceRepository.CountRunningAsync(ct),
            CompletedInstances = await _instanceRepository.CountCompletedAsync(ct),
            PendingApprovals = await _approvalRepository.CountPendingAsync(ct)
        };

        return Result.Success(stats);
    }

    // --- کمک‌ها -----------------------------------------------------------------

    private async Task<Result<WorkflowApprovalRequestDto>> DecideAsync(
        Guid approvalRequestId, bool approved, DecideWorkflowApprovalRequest request, CancellationToken ct)
    {
        var approval = await _approvalRepository.GetByIdAsync(approvalRequestId, ct);

        if (approval is null)
        {
            return Result.Failure<WorkflowApprovalRequestDto>("workflow_approval_not_found", "درخواست تأیید یافت نشد.");
        }

        if (!approval.IsPending)
        {
            return Result.Failure<WorkflowApprovalRequestDto>("workflow_approval_already_decided",
                "این درخواست تأیید از قبل تصمیم‌گیری شده است.");
        }

        // مرز امنیتی: کاربر باید مجوز مشخص‌شده روی گذار را داشته باشد.
        if (!CanDecide(approval))
        {
            // پیام خطا عمداً «یافت نشد» است تا وجود درخواست فاش نشود.
            return Result.Failure<WorkflowApprovalRequestDto>("workflow_approval_not_found", "درخواست تأیید یافت نشد.");
        }

        var instance = await _instanceRepository.GetByIdAsync(approval.InstanceId, ct);

        if (instance is null || !instance.IsRunning)
        {
            approval.Cancel();
            _approvalRepository.Update(approval);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Failure<WorkflowApprovalRequestDto>("workflow_instance_not_running",
                "نمونه‌ی گردش کار دیگر در حال اجرا نیست.");
        }

        // مرز سازمانی از طریق نمونه‌ی مرتبط (fail-closed).
        if (!await CanAccessApprovalAsync(approval, ct))
        {
            return Result.Failure<WorkflowApprovalRequestDto>("workflow_approval_not_found", "درخواست تأیید یافت نشد.");
        }

        if (approved)
        {
            // بررسی موجود بودن وضعیت مقصد پیش از تغییر وضعیت درخواست. اگر
            // تعریف تغییر کرده و وضعیت مقصد حذف شده، درخواست لغو و ذخیره
            // می‌شود تا نمونه روی یک درخواست هرگز‌تکمیل‌نشدنی قفل نشود.
            var workflow = await _workflowRepository.GetByIdAsync(instance.WorkflowId, ct);
            var toState = workflow?.FindState(approval.ToStateCode);

            if (toState is null)
            {
                approval.Cancel();
                approval.RaiseDecidedEvent(approved: false);
                _approvalRepository.Update(approval);
                await _unitOfWork.SaveChangesAsync(ct);

                return Result.Failure<WorkflowApprovalRequestDto>("workflow_invalid_transition", "وضعیت مقصد یافت نشد.");
            }

            approval.Approve(_currentUserService.UserId, _currentUserService.UserName, request.Note);

            var previousCode = instance.CurrentStateCode;

            instance.TransitionTo(toState.Code, toState.IsFinal);
            instance.RaiseTransitionedEvent(previousCode, toState.Code, _currentUserService.UserId, approved: true);

            if (toState.IsFinal)
            {
                instance.RaiseCompletedEvent();
            }

            _instanceRepository.Update(instance);
        }
        else
        {
            approval.Reject(_currentUserService.UserId, _currentUserService.UserName, request.Note);
        }

        approval.RaiseDecidedEvent(approved);

        _approvalRepository.Update(approval);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToApprovalDto(approval, CanDecide(approval)));
    }

    /// <summary>
    /// آیا کاربر جاری می‌تواند روی این درخواست تأیید تصمیم بگیرد؟
    /// اگر گذار مجوزی مشخص کرده باشد، کاربر باید آن مجوز را داشته باشد؛
    /// در غیر این صورت هر کاربر احراز هویت‌شده با مجوز مدیریت گردش کار می‌تواند.
    /// </summary>
    private bool CanDecide(WorkflowApprovalRequest approval)
    {
        if (!_currentUserService.IsAuthenticated)
        {
            return false;
        }

        // مجوز مشخص‌شده روی گذار، مرز واقعی است.
        if (!string.IsNullOrWhiteSpace(approval.ApproverPermission))
        {
            return _currentUserService.HasPermission(approval.ApproverPermission);
        }

        // بدون مجوز مشخص‌شده: نیاز به مجوز مدیریت گردش کار.
        return _currentUserService.HasPermission(ODCC.Application.Authorization.Permissions.Workflows.Manage);
    }

    /// <summary>
    /// مرز سازمانی یک درخواست تأیید از طریق نمونه‌ی مرتبط با آن. fail-closed:
    /// در صورت نبودن اطلاعات کافی، <c>false</c> برمی‌گرداند.
    /// </summary>
    private async Task<bool> CanAccessApprovalAsync(WorkflowApprovalRequest approval, CancellationToken ct)
    {
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var instance = await _instanceRepository.GetByIdAsync(approval.InstanceId, ct);

        if (instance is null)
        {
            return false;
        }

        return scope.CanAccess(instance.OrgUnitId, instance.OrgUnitPath);
    }

    private static void ApplyStates(WorkflowDefinition workflow, IReadOnlyList<WorkflowStateRequest> states)
    {
        var order = 0;

        foreach (var state in states.OrderBy(s => s.DisplayOrder))
        {
            workflow.AddState(state.Code.Trim(), state.Name.Trim(), state.IsInitial, state.IsFinal, order);
            order++;
        }
    }

    private static void ApplyTransitions(WorkflowDefinition workflow, IReadOnlyList<WorkflowTransitionRequest>? transitions)
    {
        if (transitions is null || transitions.Count == 0)
        {
            return;
        }

        var order = 0;

        foreach (var transition in transitions.OrderBy(t => t.DisplayOrder))
        {
            workflow.AddTransition(
                transition.Code.Trim(),
                transition.Name.Trim(),
                transition.FromStateCode.Trim(),
                transition.ToStateCode.Trim(),
                transition.RequiresApproval,
                string.IsNullOrWhiteSpace(transition.ApproverPermission) ? null : transition.ApproverPermission.Trim(),
                order);

            order++;
        }
    }

    /// <summary>
    /// ساخت موجودیت‌های وضعیتِ جدید بدون افزودن به تعریف (افزودن توسط مخزن انجام
    /// می‌شود تا یک مسیر واحد برای جایگزینی ساختار وجود داشته باشد).
    /// </summary>
    private static List<WorkflowState> BuildStates(IReadOnlyList<WorkflowStateRequest> states)
    {
        var order = 0;
        var hasInitial = states.Any(s => s.IsInitial);

        return states.OrderBy(s => s.DisplayOrder).Select(state => new WorkflowState
        {
            // شناسه‌ها همینجا اختصاص می‌شوند تا نگاشت «کد وضعیت → شناسه» برای
            // ساخت گذارها قبل از ذخیره‌سازی در دسترس باشد.
            Id = Guid.CreateVersion7(),
            Code = state.Code.Trim(),
            Name = state.Name.Trim(),
            IsInitial = hasInitial && state.IsInitial,
            IsFinal = state.IsFinal,
            DisplayOrder = order++
        }).ToList();
    }

    /// <summary>
    /// ساخت موجودیت‌های گذارِ جدید. کدهای وضعیت مبدا/مقصد به شناسه‌ی وضعیت‌های
    /// جدید نگاشت می‌شوند.
    /// </summary>
    private static List<WorkflowTransition> BuildTransitions(
        IReadOnlyList<WorkflowTransitionRequest>? transitions, IReadOnlyDictionary<string, Guid>? stateIdByCode = null)
    {
        if (transitions is null || transitions.Count == 0)
        {
            return [];
        }

        var order = 0;

        return transitions.OrderBy(t => t.DisplayOrder).Select(transition =>
        {
            var fromStateId = stateIdByCode?.GetValueOrDefault(transition.FromStateCode.Trim()) ?? Guid.Empty;
            var toStateId = stateIdByCode?.GetValueOrDefault(transition.ToStateCode.Trim()) ?? Guid.Empty;

            return new WorkflowTransition
            {
                Code = transition.Code.Trim(),
                Name = transition.Name.Trim(),
                FromStateId = fromStateId,
                ToStateId = toStateId,
                RequiresApproval = transition.RequiresApproval,
                ApproverPermission = string.IsNullOrWhiteSpace(transition.ApproverPermission)
                    ? null
                    : transition.ApproverPermission.Trim(),
                DisplayOrder = order++
            };
        }).ToList();
    }

    private static WorkflowDto ToWorkflowDto(WorkflowDefinition workflow)
    {
        var stateDtos = workflow.States
            .OrderBy(s => s.DisplayOrder)
            .Select(s => new WorkflowStateDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name,
                IsInitial = s.IsInitial,
                IsFinal = s.IsFinal,
                DisplayOrder = s.DisplayOrder
            }).ToList();

        var transitionDtos = workflow.Transitions
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new WorkflowTransitionDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                FromStateCode = workflow.States.FirstOrDefault(s => s.Id == t.FromStateId)?.Code ?? string.Empty,
                ToStateCode = workflow.States.FirstOrDefault(s => s.Id == t.ToStateId)?.Code ?? string.Empty,
                RequiresApproval = t.RequiresApproval,
                ApproverPermission = t.ApproverPermission,
                DisplayOrder = t.DisplayOrder
            }).ToList();

        // نقشه‌ی گذارهای مجاز از هر وضعیت.
        var available = transitionDtos
            .GroupBy(t => t.FromStateCode)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(t => t.Code).ToList(),
                StringComparer.Ordinal);

        return new WorkflowDto
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Code = workflow.Code,
            Description = workflow.Description,
            EntityType = workflow.EntityType,
            Status = workflow.Status,
            Version = workflow.Version,
            CreatedByUserId = workflow.CreatedByUserId,
            CreatedByUserName = workflow.CreatedByUserName,
            CreatedAt = workflow.CreatedAt,
            States = stateDtos,
            Transitions = transitionDtos,
            AvailableTransitions = available
        };
    }

    private static WorkflowInstanceDto ToInstanceDto(
        WorkflowInstance instance,
        WorkflowDefinition? workflow,
        WorkflowApprovalRequest? pendingApproval,
        IReadOnlyCollection<string> actorPermissions)
    {
        var nextTransitions = new List<WorkflowTransitionDto>();

        if (workflow is not null && instance.IsRunning)
        {
            var stateIds = workflow.States.ToDictionary(s => s.Code, s => s.Id);

            if (stateIds.TryGetValue(instance.CurrentStateCode, out var currentStateId))
            {
                nextTransitions = workflow.Transitions
                    .Where(t => t.FromStateId == currentStateId)
                    .OrderBy(t => t.DisplayOrder)
                    .Select(t => new WorkflowTransitionDto
                    {
                        Id = t.Id,
                        Code = t.Code,
                        Name = t.Name,
                        FromStateCode = instance.CurrentStateCode,
                        ToStateCode = workflow.States.FirstOrDefault(s => s.Id == t.ToStateId)?.Code ?? string.Empty,
                        RequiresApproval = t.RequiresApproval,
                        ApproverPermission = t.ApproverPermission,
                        DisplayOrder = t.DisplayOrder
                    }).ToList();
            }
        }

        return new WorkflowInstanceDto
        {
            Id = instance.Id,
            WorkflowId = instance.WorkflowId,
            WorkflowCode = instance.WorkflowCode,
            WorkflowVersion = instance.WorkflowVersion,
            EntityType = instance.EntityType,
            EntityId = instance.EntityId,
            CurrentStateCode = instance.CurrentStateCode,
            Status = instance.Status,
            StartedAt = instance.StartedAt,
            CompletedAt = instance.CompletedAt,
            CancelledAt = instance.CancelledAt,
            StartedByUserId = instance.StartedByUserId,
            StartedByUserName = instance.StartedByUserName,
            ContextJson = instance.ContextJson,
            TransitionCount = instance.TransitionCount,
            NextTransitions = nextTransitions,
            PendingApproval = pendingApproval is null ? null : ToApprovalDto(pendingApproval, CanDecideStatic(pendingApproval, actorPermissions))
        };
    }

    private static WorkflowApprovalRequestDto ToApprovalDto(WorkflowApprovalRequest approval, bool canCurrentUserDecide) => new()
    {
        Id = approval.Id,
        InstanceId = approval.InstanceId,
        TransitionId = approval.TransitionId,
        TransitionCode = approval.TransitionCode,
        FromStateCode = approval.FromStateCode,
        ToStateCode = approval.ToStateCode,
        ApproverPermission = approval.ApproverPermission,
        Status = approval.Status,
        RequestedAt = approval.RequestedAt,
        RequestedById = approval.RequestedById,
        DecidedAt = approval.DecidedAt,
        DecidedById = approval.DecidedById,
        DecidedByUserName = approval.DecidedByUserName,
        DecisionNote = approval.DecisionNote,
        ExpiresAt = approval.ExpiresAt,
        CanCurrentUserDecide = canCurrentUserDecide
    };

    private static bool CanDecideStatic(WorkflowApprovalRequest approval, IReadOnlyCollection<string> actorPermissions)
    {
        if (string.IsNullOrWhiteSpace(approval.ApproverPermission))
        {
            // بدون مجوز مشخص‌شده: مجوز مدیریت گردش کار لازم است.
            return actorPermissions.Contains(ODCC.Application.Authorization.Permissions.Workflows.Manage);
        }

        return actorPermissions.Contains(approval.ApproverPermission);
    }
}
