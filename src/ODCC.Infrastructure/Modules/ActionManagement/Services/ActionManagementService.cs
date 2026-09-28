using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.ActionManagement.Abstractions;
using ODCC.Application.Modules.ActionManagement.Dtos;
using ODCC.Application.Modules.Identity.Abstractions;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.ActionManagement.Entities;
using ODCC.Domain.Modules.ActionManagement.Enums;

namespace ODCC.Infrastructure.Modules.ActionManagement.Services;

/// <summary>
/// سرویس مدیریت اقدامات و پیگیری.
///
/// <b>مرز سازمانی:</b> همه‌ی خواندن‌ها با دامنه‌ی سازمانی کاربر جاری
/// (fail-closed) فیلتر می‌شوند. استثناء‌ی مستند: کاربر همیشه آیتم‌های منتسب
/// به خودش را می‌بیند و می‌تواند وضعیت آن‌ها را تغییر دهد.
///
/// <b>مجوزها:</b> نوشتن‌ها نیازمند <c>actions.manage</c> هستند. یک استثناء‌ی
/// عملی وجود دارد: مسئول یک آیتم می‌تواند آن را شروع/تکمیل/ازسرگیری کند بدون
/// مجوز مدیریت (انجام کار واگذاری‌شده). لغو و ارزیابی اثربخشی نیازمند مدیریت است.
///
/// <b>حریم خصوصی:</b> این سرویس هرگز شناسه‌ی پاسخ‌گوی نظرسنجی را ذخیره نمی‌کند.
/// برنامه‌های خودکار فقط شاخص‌های تجمعی را به‌عنوان زمینه نگه می‌دارند.
/// </summary>
public sealed class ActionManagementService(
    IActionPlanRepository planRepository,
    IActionItemRepository itemRepository,
    IActionEvidenceStore evidenceStore,
    IOrgScopeProvider orgScopeProvider,
    IOrgUnitRepository orgUnitRepository,
    ISurveyService surveyService,
    IUserLookupService userLookupService,
    ICurrentUserService currentUserService,
    IActionManagementUnitOfWork unitOfWork,
    ILogger<ActionManagementService> logger) : IActionManagementService
{
    private readonly IActionPlanRepository _planRepository = planRepository;
    private readonly IActionItemRepository _itemRepository = itemRepository;
    private readonly IActionEvidenceStore _evidenceStore = evidenceStore;
    private readonly IOrgScopeProvider _orgScopeProvider = orgScopeProvider;
    private readonly IOrgUnitRepository _orgUnitRepository = orgUnitRepository;
    private readonly ISurveyService _surveyService = surveyService;
    private readonly IUserLookupService _userLookupService = userLookupService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IActionManagementUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ActionManagementService> _logger = logger;

    private static readonly Action<ILogger, Guid, Exception> FollowUpFailed = LoggerMessage.Define<Guid>(
        LogLevel.Error,
        new EventId(1, "ActionFollowUpFailed"),
        "پیگیری آیتم اقدام {ItemId} شکست خورد.");

    private static readonly Action<ILogger, Guid, Exception> UploadFailed = LoggerMessage.Define<Guid>(
        LogLevel.Error,
        new EventId(2, "ActionEvidenceUploadFailed"),
        "آپلود پیوست آیتم اقدام {ItemId} شکست خورد.");

    // --- برنامه‌ها ---------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<PagedResult<ActionPlanDto>> SearchPlansAsync(
        ActionPlanSearchRequest request, CancellationToken ct = default)
    {
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        var totalCount = await _planRepository.CountAsync(request, scope, ct);
        var plans = await _planRepository.SearchAsync(request, scope, ct);

        // بارگذاری آیتم‌های هر برنامه برای محاسبه‌ی پیشرفت. از Include در
        // مخزن استفاده نشده تا صفحه‌بندی روی برنامه‌ها حفظ شود.
        var items = await LoadItemsForPlansAsync(plans, ct);

        var currentUserId = _currentUserService.UserId;

        return new PagedResult<ActionPlanDto>
        {
            Items = plans.Select(p => ToPlanDto(p, items.GetValueOrDefault(p.Id))).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<ActionPlanDto>> GetPlanByIdAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, ct);

        if (plan is null)
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        // مرز سازمانی: کاربر باید بتواند به برنامه دسترسی داشته باشد.
        if (!IsPlanVisible(plan, scope))
        {
            // پیام خطا عمداً «یافت نشد» است تا وجود برنامه‌ی خارج از دامنه فاش نشود.
            return Result.Failure<ActionPlanDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        var items = plan.Items.Count > 0 ? plan.Items : await _itemRepository.ListByPlanAsync(plan.Id, ct);

        return Result.Success(ToPlanDto(plan, items));
    }

    /// <inheritdoc/>
    public async Task<Result<ActionPlanDto>> CreatePlanAsync(
        SaveActionPlanRequest request, CancellationToken ct = default)
    {
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        var orgScope = await ResolveOrgScopeAsync(request.OrgUnitId, scope, ct);

        if (orgScope is null)
        {
            return Result.Failure<ActionPlanDto>(
                "action_org_unit_out_of_scope",
                "واحد سازمانی انتخاب‌شده خارج از دامنه‌ی قابل‌مشاهده‌ی شما است.");
        }

        var survey = await ResolveSurveySnapshotAsync(request.SurveyId, ct);

        var ownerName = await ResolveUserNameAsync(request.OwnerUserId, ct);

        var plan = new ActionPlan
        {
            Title = request.Title.Trim(),
            Description = request.Description,
            Source = ActionSource.Manual,
            SurveyId = request.SurveyId,
            SurveyCode = survey?.Code,
            SurveyTitle = survey?.Title,
            CampaignId = request.CampaignId,
            OrgUnitId = orgScope.OrgUnitId,
            OrgUnitPath = orgScope.OrgUnitPath,
            OwnerUserId = request.OwnerUserId,
            OwnerUserName = ownerName,
            Priority = request.Priority,
            Status = ActionPlanStatus.Draft,
            DueDate = request.DueDate,
            CreatedByUserId = _currentUserService.UserId,
            CreatedByUserName = _currentUserService.UserName
        };

        if (request.ActivateImmediately)
        {
            plan.Activate();
        }

        // آیتم‌های اولیه.
        if (request.Items is { Count: > 0 } items)
        {
            foreach (var itemRequest in items)
            {
                plan.Items.Add(CreateItemFromRequest(plan.Id, itemRequest, await ResolveUserNameAsync(itemRequest.AssigneeUserId, ct)));
            }
        }

        plan.RaiseCreatedEvent();

        await _planRepository.AddAsync(plan, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToPlanDto(plan, plan.Items));
    }

    /// <inheritdoc/>
    public async Task<Result<ActionPlanDto>> UpdatePlanAsync(
        Guid id, SaveActionPlanRequest request, CancellationToken ct = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, ct);

        if (plan is null)
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        if (plan.Status == ActionPlanStatus.Archived)
        {
            return Result.Failure<ActionPlanDto>("action_plan_archived", "برنامه‌ی بایگانی‌شده قابل ویرایش نیست.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (!IsPlanVisible(plan, scope))
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        var orgScope = await ResolveOrgScopeAsync(request.OrgUnitId, scope, ct);

        if (orgScope is null)
        {
            return Result.Failure<ActionPlanDto>(
                "action_org_unit_out_of_scope",
                "واحد سازمانی انتخاب‌شده خارج از دامنه‌ی قابل‌مشاهده‌ی شما است.");
        }

        var survey = await ResolveSurveySnapshotAsync(request.SurveyId, ct);

        var wasActive = plan.Status == ActionPlanStatus.Active;

        plan.Title = request.Title.Trim();
        plan.Description = request.Description;
        plan.SurveyId = request.SurveyId;
        plan.SurveyCode = survey?.Code ?? plan.SurveyCode;
        plan.SurveyTitle = survey?.Title ?? plan.SurveyTitle;
        plan.CampaignId = request.CampaignId;
        plan.OrgUnitId = orgScope.OrgUnitId;
        plan.OrgUnitPath = orgScope.OrgUnitPath;
        plan.OwnerUserId = request.OwnerUserId;
        plan.OwnerUserName = await ResolveUserNameAsync(request.OwnerUserId, ct) ?? plan.OwnerUserName;
        plan.Priority = request.Priority;
        plan.DueDate = request.DueDate;

        // وضعیت فعال‌بودن پس از ویرایش حفظ می‌شود.
        if (wasActive && plan.Status == ActionPlanStatus.Draft)
        {
            plan.Activate();
        }

        plan.RaiseUpdatedEvent();

        _planRepository.Update(plan);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToPlanDto(plan, await _itemRepository.ListByPlanAsync(plan.Id, ct)));
    }

    /// <inheritdoc/>
    public async Task<Result<ActionPlanDto>> ActivatePlanAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, ct);

        if (plan is null)
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        if (plan.Status == ActionPlanStatus.Archived)
        {
            return Result.Failure<ActionPlanDto>("action_plan_archived", "برنامه‌ی بایگانی‌شده قابل فعال‌سازی نیست.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (!IsPlanVisible(plan, scope))
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        plan.Activate();
        plan.RaiseActivatedEvent();

        _planRepository.Update(plan);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToPlanDto(plan, await _itemRepository.ListByPlanAsync(plan.Id, ct)));
    }

    /// <inheritdoc/>
    public async Task<Result<ActionPlanDto>> CompletePlanAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, ct);

        if (plan is null)
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        if (plan.Status != ActionPlanStatus.Active)
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_active", "فقط یک برنامه‌ی فعال قابل تکمیل است.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (!IsPlanVisible(plan, scope))
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        plan.Complete();
        plan.RaiseCompletedEvent();

        _planRepository.Update(plan);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToPlanDto(plan, await _itemRepository.ListByPlanAsync(plan.Id, ct)));
    }

    /// <inheritdoc/>
    public async Task<Result<ActionPlanDto>> CancelPlanAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, ct);

        if (plan is null)
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        if (plan.Status is not (ActionPlanStatus.Draft or ActionPlanStatus.Active))
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_cancellable", "این برنامه قابل لغو نیست.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (!IsPlanVisible(plan, scope))
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        plan.Cancel();
        plan.RaiseCancelledEvent();

        _planRepository.Update(plan);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToPlanDto(plan, await _itemRepository.ListByPlanAsync(plan.Id, ct)));
    }

    /// <inheritdoc/>
    public async Task<Result> ArchivePlanAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, ct);

        if (plan is null)
        {
            return Result.Failure("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        if (plan.Status == ActionPlanStatus.Archived)
        {
            return Result.Success();
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (!IsPlanVisible(plan, scope))
        {
            return Result.Failure("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        plan.Archive();
        plan.RaiseArchivedEvent();

        _planRepository.Update(plan);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<ActionPlanDto>> RecordPlanOutcomeAsync(
        Guid id, RecordOutcomeRequest request, CancellationToken ct = default)
    {
        var plan = await _planRepository.GetByIdAsync(id, ct);

        if (plan is null)
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (!IsPlanVisible(plan, scope))
        {
            return Result.Failure<ActionPlanDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        plan.RecordOutcome(request.Value);
        plan.RaiseUpdatedEvent();

        _planRepository.Update(plan);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToPlanDto(plan, await _itemRepository.ListByPlanAsync(plan.Id, ct)));
    }

    // --- آیتم‌ها ------------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<PagedResult<ActionItemDto>> SearchItemsAsync(
        ActionItemSearchRequest request, CancellationToken ct = default)
    {
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        var totalCount = await _itemRepository.CountAsync(request, scope, currentUserId, ct);
        var items = await _itemRepository.SearchAsync(request, scope, currentUserId, ct);

        return new PagedResult<ActionItemDto>
        {
            Items = items.Select(i => ToItemDto(i, currentUserId)).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<ActionItemDto>> GetItemByIdAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _itemRepository.GetByIdAsync(id, ct);

        if (item is null)
        {
            return Result.Failure<ActionItemDto>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        if (!IsItemVisible(item, scope, currentUserId))
        {
            return Result.Failure<ActionItemDto>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        return Result.Success(ToItemDto(item, currentUserId));
    }

    /// <inheritdoc/>
    public async Task<Result<ActionItemDto>> CreateItemAsync(
        Guid planId, SaveActionItemRequest request, CancellationToken ct = default)
    {
        var plan = await _planRepository.GetByIdAsync(planId, ct);

        if (plan is null)
        {
            return Result.Failure<ActionItemDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);

        if (!IsPlanVisible(plan, scope))
        {
            return Result.Failure<ActionItemDto>("action_plan_not_found", "برنامه‌ی اقدام یافت نشد.");
        }

        if (plan.Status is ActionPlanStatus.Archived or ActionPlanStatus.Cancelled)
        {
            return Result.Failure<ActionItemDto>(
                "action_plan_closed", "به برنامه‌ی بایگانی/لغوشده نمی‌توان آیتم اضافه کرد.");
        }

        var assigneeName = await ResolveUserNameAsync(request.AssigneeUserId, ct);

        var item = CreateItemFromRequest(planId, request, assigneeName);
        item.RaiseCreatedEvent();

        await _itemRepository.AddAsync(item, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToItemDto(item, _currentUserService.UserId ?? Guid.Empty));
    }

    /// <inheritdoc/>
    public async Task<Result<ActionItemDto>> UpdateItemAsync(
        Guid id, SaveActionItemRequest request, CancellationToken ct = default)
    {
        var item = await _itemRepository.GetByIdAsync(id, ct);

        if (item is null)
        {
            return Result.Failure<ActionItemDto>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        if (!IsItemVisible(item, scope, currentUserId))
        {
            return Result.Failure<ActionItemDto>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        // ویرایش کامل آیتم نیازمند مجوز مدیریت است (برخلاف تغییر وضعیت).
        if (!_currentUserService.HasPermission(Permissions.Actions.Manage))
        {
            return Result.Failure<ActionItemDto>("action_not_authorized", "ویرایش آیتم نیازمند مجوز مدیریت اقدامات است.");
        }

        item.Title = request.Title.Trim();
        item.Description = request.Description;
        item.Assign(request.AssigneeUserId, await ResolveUserNameAsync(request.AssigneeUserId, ct));
        item.Priority = request.Priority;
        item.DisplayOrder = request.DisplayOrder;
        item.DueDate = request.DueDate;
        item.RemindAt = request.RemindAt;

        item.RaiseUpdatedEvent();

        _itemRepository.Update(item);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToItemDto(item, currentUserId));
    }

    /// <inheritdoc/>
    public async Task<Result<ActionItemDto>> TransitionItemAsync(
        Guid id, TransitionActionItemRequest request, CancellationToken ct = default)
    {
        var item = await _itemRepository.GetByIdAsync(id, ct);

        if (item is null)
        {
            return Result.Failure<ActionItemDto>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        if (!IsItemVisible(item, scope, currentUserId))
        {
            return Result.Failure<ActionItemDto>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var canManage = _currentUserService.HasPermission(Permissions.Actions.Manage);
        var isAssignee = item.AssigneeUserId is not null && item.AssigneeUserId == _currentUserService.UserId;

        // لغو کردن و باز کردن آیتم لغوشده نیازمند مجوز مدیریت است.
        if (request.NewStatus == ActionItemStatus.Cancelled && !canManage)
        {
            return Result.Failure<ActionItemDto>(
                "action_not_authorized", "لغو آیتم نیازمند مجوز مدیریت اقدامات است.");
        }

        // تغییر وضعیت توسط مسئول آیتم مجاز است (انجام کار واگذاری‌شده)؛
        // در غیر این صورت نیازمند مجوز مدیریت است.
        if (!canManage && !isAssignee)
        {
            return Result.Failure<ActionItemDto>(
                "action_not_authorized", "تغییر وضعیت این آیتم به شما مجاز نیست.");
        }

        var oldStatus = item.Status;

        if (!TryTransition(item, request.NewStatus, oldStatus, out var error))
        {
            return Result.Failure<ActionItemDto>(error);
        }

        item.RaiseStatusChangedEvent(oldStatus, request.NewStatus);

        if (item.Status == ActionItemStatus.Done)
        {
            item.RaiseCompletedEvent();
        }

        _itemRepository.Update(item);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToItemDto(item, currentUserId));
    }

    /// <inheritdoc/>
    public async Task<Result<ActionItemDto>> AssessItemEffectivenessAsync(
        Guid id, AssessEffectivenessRequest request, CancellationToken ct = default)
    {
        var item = await _itemRepository.GetByIdAsync(id, ct);

        if (item is null)
        {
            return Result.Failure<ActionItemDto>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        if (!IsItemVisible(item, scope, currentUserId))
        {
            return Result.Failure<ActionItemDto>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        // ارزیابی اثربخشی یک قضاوت مدیریتی است.
        if (!_currentUserService.HasPermission(Permissions.Actions.Manage))
        {
            return Result.Failure<ActionItemDto>(
                "action_not_authorized", "ارزیابی اثربخشی نیازمند مجوز مدیریت اقدامات است.");
        }

        if (item.Status is not (ActionItemStatus.Done or ActionItemStatus.Cancelled))
        {
            return Result.Failure<ActionItemDto>(
                "action_item_not_finished", "اثربخشی فقط برای آیتم‌های تکمیل/لغوشده قابل ارزیابی است.");
        }

        item.AssessEffectiveness(request.Rating, request.Note, _currentUserService.UserId);
        item.RaiseUpdatedEvent();

        _itemRepository.Update(item);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToItemDto(item, currentUserId));
    }

    // --- دیدگاه‌ها ----------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<ActionCommentDto>>> ListCommentsAsync(
        Guid itemId, CancellationToken ct = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, ct);

        if (item is null)
        {
            return Result.Failure<IReadOnlyList<ActionCommentDto>>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        if (!IsItemVisible(item, scope, currentUserId))
        {
            return Result.Failure<IReadOnlyList<ActionCommentDto>>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var comments = await _itemRepository.ListCommentsAsync(itemId, ct);

        return Result.Success<IReadOnlyList<ActionCommentDto>>(
            comments.Select(ToCommentDto).ToList());
    }

    /// <inheritdoc/>
    public async Task<Result<ActionCommentDto>> AddCommentAsync(
        Guid itemId, AddActionCommentRequest request, CancellationToken ct = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, ct);

        if (item is null)
        {
            return Result.Failure<ActionCommentDto>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        if (!IsItemVisible(item, scope, currentUserId))
        {
            return Result.Failure<ActionCommentDto>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var comment = new ActionComment
        {
            ActionItemId = itemId,
            AuthorUserId = _currentUserService.UserId,
            AuthorUserName = _currentUserService.DisplayName ?? _currentUserService.UserName,
            Body = request.Body.Trim()
        };

        await _itemRepository.AddCommentAsync(comment, ct);

        // رویداد روی آیتم منتشر می‌شود تا شنونده‌ی اعلان‌ها شرکت‌کنندگان را مطلع کند.
        item.RaiseCommentAddedEvent(comment.Id, comment.AuthorUserId ?? Guid.Empty, comment.AuthorUserName ?? string.Empty);

        _itemRepository.Update(item);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToCommentDto(comment));
    }

    // --- پیوست‌ها -----------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<Result<IReadOnlyList<ActionEvidenceDto>>> ListEvidenceAsync(
        Guid itemId, CancellationToken ct = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, ct);

        if (item is null)
        {
            return Result.Failure<IReadOnlyList<ActionEvidenceDto>>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        if (!IsItemVisible(item, scope, currentUserId))
        {
            return Result.Failure<IReadOnlyList<ActionEvidenceDto>>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var evidence = await _itemRepository.ListEvidenceAsync(itemId, ct);

        return Result.Success<IReadOnlyList<ActionEvidenceDto>>(evidence.Select(ToEvidenceDto).ToList());
    }

    /// <inheritdoc/>
    public async Task<Result<ActionEvidenceDto>> UploadEvidenceAsync(
        Guid itemId, UploadActionEvidenceRequest request, CancellationToken ct = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, ct);

        if (item is null)
        {
            return Result.Failure<ActionEvidenceDto>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        if (!IsItemVisible(item, scope, currentUserId))
        {
            return Result.Failure<ActionEvidenceDto>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        try
        {
            var stored = await _evidenceStore.SaveAsync(request.Content, request.FileName, request.ContentType, ct);

            var evidence = new ActionEvidence
            {
                ActionItemId = itemId,
                FileName = request.FileName,
                ContentType = request.ContentType,
                FileSizeBytes = stored.LengthBytes,
                StoragePath = stored.RelativePath,
                UploadedByUserId = _currentUserService.UserId,
                UploadedByUserName = _currentUserService.DisplayName ?? _currentUserService.UserName
            };

            await _itemRepository.AddEvidenceAsync(evidence, ct);

            item.RaiseEvidenceUploadedEvent(evidence.Id, evidence.FileName, evidence.FileSizeBytes, evidence.UploadedByUserId);

            _itemRepository.Update(item);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success(ToEvidenceDto(evidence));
        }
        catch (Exception ex)
        {
            // خطای ذخیره‌سازی فایل نباید آیتم را نابود کند؛ لاگ می‌شود.
            UploadFailed(_logger, itemId, ex);
            return Result.Failure<ActionEvidenceDto>("action_evidence_upload_failed", "ذخیره‌سازی پیوست ناموفق بود.");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<ActionEvidenceContent>> DownloadEvidenceAsync(
        Guid evidenceId, CancellationToken ct = default)
    {
        var evidence = await _itemRepository.GetEvidenceByIdAsync(evidenceId, ct);

        if (evidence is null)
        {
            return Result.Failure<ActionEvidenceContent>("action_evidence_not_found", "پیوست یافت نشد.");
        }

        var item = await _itemRepository.GetByIdAsync(evidence.ActionItemId, ct);

        if (item is null)
        {
            return Result.Failure<ActionEvidenceContent>("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        if (!IsItemVisible(item, scope, currentUserId))
        {
            return Result.Failure<ActionEvidenceContent>("action_evidence_not_found", "پیوست یافت نشد.");
        }

        if (!_evidenceStore.Exists(evidence.StoragePath))
        {
            return Result.Failure<ActionEvidenceContent>(
                "action_evidence_missing", "فایل پیوست یافت نشد (ممکن است پاک شده باشد).");
        }

        var stream = await _evidenceStore.OpenReadAsync(evidence.StoragePath, ct);

        return Result.Success(new ActionEvidenceContent
        {
            Content = stream,
            FileName = evidence.FileName,
            ContentType = evidence.ContentType,
            SizeBytes = evidence.FileSizeBytes
        });
    }

    /// <inheritdoc/>
    public async Task<Result> DeleteEvidenceAsync(Guid evidenceId, CancellationToken ct = default)
    {
        var evidence = await _itemRepository.GetEvidenceByIdAsync(evidenceId, ct);

        if (evidence is null)
        {
            return Result.Failure("action_evidence_not_found", "پیوست یافت نشد.");
        }

        var item = await _itemRepository.GetByIdAsync(evidence.ActionItemId, ct);

        if (item is null)
        {
            return Result.Failure("action_item_not_found", "آیتم اقدام یافت نشد.");
        }

        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        if (!IsItemVisible(item, scope, currentUserId))
        {
            return Result.Failure("action_evidence_not_found", "پیوست یافت نشد.");
        }

        var canManage = _currentUserService.HasPermission(Permissions.Actions.Manage);
        var isUploader = evidence.UploadedByUserId is not null && evidence.UploadedByUserId == _currentUserService.UserId;

        // حذف پیوست: مدیر یا آپلودکننده.
        if (!canManage && !isUploader)
        {
            return Result.Failure("action_not_authorized", "حذف این پیوست به شما مجاز نیست.");
        }

        // حذف نرم متادیتا؛ فایل فیزیکی هم (در صورت امکان) پاک می‌شود.
        _itemRepository.RemoveEvidence(evidence);

        if (_evidenceStore.Exists(evidence.StoragePath))
        {
            try
            {
                await _evidenceStore.DeleteAsync(evidence.StoragePath, ct);
            }
            catch
            {
                // حذف فایل ناموفق بود؛ ردیف همچنان نرم حذف می‌شود.
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // --- پیگیری خودکار -------------------------------------------------------------

    /// <inheritdoc/>
    public async Task<int> ProcessDueRemindersAsync(DateTime asOf, CancellationToken ct = default)
    {
        var due = await _itemRepository.ListDueRemindersAsync(asOf, maxResults: 100, ct);

        if (due.Count == 0)
        {
            return 0;
        }

        var processed = 0;

        foreach (var item in due)
        {
            try
            {
                // رویداد قبل از پاک‌سازی زمان یادآور تولید می‌شود. پس از ارسال،
                // زمان یادآور پاک می‌شود تا دوباره فعال نشود.
                item.RaiseReminderDueEvent();
                item.ClearReminder();

                _itemRepository.Update(item);
                await _unitOfWork.SaveChangesAsync(ct);

                processed++;
            }
            catch (Exception ex)
            {
                // یک آیتم نباید پردازش بقیه را متوقف کند.
                FollowUpFailed(_logger, item.Id, ex);
            }
        }

        return processed;
    }

    /// <inheritdoc/>
    public async Task<int> ProcessOverdueEscalationsAsync(DateTime asOf, CancellationToken ct = default)
    {
        var overdue = await _itemRepository.ListOverdueAsync(asOf, maxResults: 100, ct);

        if (overdue.Count == 0)
        {
            return 0;
        }

        var escalationInterval = TimeSpan.FromDays(OverdueEscalationIntervalDays);
        var escalated = 0;

        foreach (var item in overdue)
        {
            try
            {
                // اولین سررسیدی: بلافاصله یک درجه تشدید می‌شود.
                // درجات بعدی فقط پس از سپری‌شدن فاصله‌ی زمانی مجاز.
                var dueForEscalation = item.EscalationLevel == EscalationLevel.None
                    || (item.EscalatedAt.HasValue && asOf - item.EscalatedAt.Value >= escalationInterval);

                if (!dueForEscalation)
                {
                    continue;
                }

                if (item.Escalate())
                {
                    item.RaiseEscalatedEvent();

                    _itemRepository.Update(item);
                    await _unitOfWork.SaveChangesAsync(ct);

                    escalated++;
                }
            }
            catch (Exception ex)
            {
                FollowUpFailed(_logger, item.Id, ex);
            }
        }

        return escalated;
    }

    /// <summary>
    /// فاصله‌ی زمانی بین درجات تشدید (روز). قابل پیکربندی نیست تا رفتار
    /// پیگیری قابل پیش‌بینی بماند: سررسید → یادآور → ۳ روز → تشدید به مالک
    /// → ۳ روز → تشدید به مدیریت.
    /// </summary>
    private const int OverdueEscalationIntervalDays = 3;

    /// <inheritdoc/>
    public async Task<Result<ActionPlanDto?>> EnsurePlanFromAnalyticsAlertAsync(
        AnalyticsAlertRequest request, CancellationToken ct = default)
    {
        // کلید یکتای منبع: تضمین می‌کند محاسبه‌ی مجدد تحلیلات برنامه‌ی مضاعف نسازد.
        var sourceKey = $"analytics:{request.SurveyId}:{request.SegmentKey}:{request.MetricType}";

        var existing = await _planRepository.FindBySourceKeyAsync(sourceKey, ct);

        if (existing is not null)
        {
            // برنامه از قبل وجود دارد؛ مقدار شاخص فعلی به‌عنوان خروجی به‌روزرسانی
            // می‌شود تا سنجش اثربخشی زنده بماند (بدون ایجاد برنامه‌ی جدید).
            if (existing.IsOpen)
            {
                existing.RecordOutcome(request.MetricValue);
                existing.RaiseUpdatedEvent();

                _planRepository.Update(existing);
                await _unitOfWork.SaveChangesAsync(ct);
            }

            return Result.Success<ActionPlanDto?>(ToPlanDto(existing, await _itemRepository.ListByPlanAsync(existing.Id, ct)));
        }

        var metricLabel = request.MetricType switch
        {
            ActionMetricType.Csat => "CSAT",
            ActionMetricType.Ces => "CES",
            _ => "NPS"
        };

        // عنوان نظرسنجی از سمت شنونده‌ی تحلیلات در دسترس نیست (رویداد فقط کد
        // دارد)؛ اینجا از طریق قرارداد سرویس نظرسنجی حل می‌شود.
        var survey = await ResolveSurveySnapshotAsync(request.SurveyId, ct);
        var surveyTitle = !string.IsNullOrWhiteSpace(request.SurveyTitle)
            ? request.SurveyTitle
            : survey?.Title ?? request.SurveyCode;

        var plan = new ActionPlan
        {
            Title = request.SuggestedTitle.Trim(),
            Description = $"شاخص {metricLabel} نظرسنجی «{surveyTitle}» با مقدار {request.MetricValue} " +
                $"از هدف {request.TargetValue} فاصله دارد. این برنامه به‌صورت خودکار از هشدار تحلیلات ایجاد شده است.",
            Source = ActionSource.AnalyticsAlert,
            SourceKey = sourceKey,
            SurveyId = request.SurveyId,
            SurveyCode = !string.IsNullOrWhiteSpace(request.SurveyCode) ? request.SurveyCode : survey?.Code,
            SurveyTitle = surveyTitle,
            TriggerMetricType = request.MetricType,
            TriggerMetricValue = request.MetricValue,
            OrgUnitId = request.OrgUnitId,
            OrgUnitPath = request.OrgUnitPath,
            Priority = ActionPriority.High,
            Status = ActionPlanStatus.Active, // هشدارهای خودکار فوراً فعال‌اند تا پیگیری کار کند.
            DueDate = DateTime.UtcNow.AddDays(30),
            CreatedByUserId = request.ActorUserId
        };

        // یک آیتم شروع‌کننده: مالک برنامه بعداً آیتم‌های دقیق‌تر اضافه می‌کند.
        var starter = new ActionItem
        {
            ActionPlanId = plan.Id,
            Title = $"بررسی افت شاخص {metricLabel} و تعیین اقدامات اصلاحی",
            Description = "ریشه‌ی افت شاخص را تحلیل کنید و اقدامات لازم را به این برنامه اضافه کنید.",
            Priority = ActionPriority.High,
            Status = ActionItemStatus.Open,
            DisplayOrder = 0,
            DueDate = plan.DueDate
        };

        plan.Items.Add(starter);

        plan.RaiseCreatedEvent();
        starter.RaiseCreatedEvent();

        await _planRepository.AddAsync(plan, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success<ActionPlanDto?>(ToPlanDto(plan, plan.Items));
    }

    /// <inheritdoc/>
    public async Task<Result<ActionStatsDto>> GetStatsAsync(CancellationToken ct = default)
    {
        var scope = await _orgScopeProvider.GetCurrentScopeAsync(ct);
        var currentUserId = _currentUserService.UserId ?? Guid.Empty;

        var plans = await _planRepository.SearchAsync(new ActionPlanSearchRequest
        {
            PageSize = 200,
            IncludeArchived = false
        }, scope, ct);

        var myItems = await _itemRepository.SearchAsync(new ActionItemSearchRequest
        {
            AssignedToMe = true,
            PageSize = 200
        }, scope, currentUserId, ct);

        var openItems = await _itemRepository.SearchAsync(new ActionItemSearchRequest
        {
            PageSize = 200
        }, scope, currentUserId, ct);

        var activePlans = plans.Where(p => p.Status == ActionPlanStatus.Active).ToList();

        // ارزیابی اثربخشی: درصد آیتم‌های تکمیل‌شده با ارزیابی موثر.
        var assessedItems = openItems
            .Where(i => i.Effectiveness is not EffectivenessRating.NotAssessed)
            .ToList();

        decimal? effectivenessScore = assessedItems.Count == 0
            ? null
            : Math.Round((decimal)assessedItems.Count(i => i.Effectiveness == EffectivenessRating.Effective) / assessedItems.Count * 100, 1);

        var now = DateTime.UtcNow;

        return Result.Success(new ActionStatsDto
        {
            TotalOpenPlans = plans.Count(p => p.IsOpen),
            TotalActivePlans = activePlans.Count,
            TotalCompletedPlans = plans.Count(p => p.Status == ActionPlanStatus.Completed),
            MyOpenItems = myItems.Count(i => i.IsOpen),
            MyOverdueItems = myItems.Count(i => i.IsOverdue),
            OpenItems = openItems.Count(i => i.IsOpen),
            OverdueItems = openItems.Count(i => i.IsOverdue),
            EscalatedItems = openItems.Count(i => i.EscalationLevel != EscalationLevel.None && i.IsOpen),
            CompletedThisPeriod = openItems.Count(i =>
                i.Status == ActionItemStatus.Done
                && i.CompletedAt.HasValue
                && i.CompletedAt.Value >= now.AddDays(-30)),
            AveragePlanProgress = activePlans.Count == 0
                ? 0
                : Math.Round(activePlans.Average(p => p.ProgressPercentage), 1),
            EffectivenessScore = effectivenessScore
        });
    }

    // --- کمک‌کننده‌ها ---------------------------------------------------------------

    /// <summary>آیا کاربر جاری اجازه‌ی دیدن این برنامه را دارد؟</summary>
    private static bool IsPlanVisible(ActionPlan plan, OrgScope scope)
    {
        // برنامه‌های سراسری (بدون واحد سازمانی) برای دامنه‌ی شرکت قابل‌مشاهده‌اند.
        if (plan.OrgUnitId is null || plan.OrgUnitPath is null)
        {
            return scope.IsUnrestricted;
        }

        return scope.CanAccess(plan.OrgUnitPath);
    }

    /// <summary>
    /// آیا کاربر جاری اجازه‌ی دیدن این آیتم را دارد؟ مرز سازمانی برنامه‌ی مالک
    /// یا استثناء‌ی انتصاب به خود کاربر.
    /// </summary>
    private static bool IsItemVisible(ActionItem item, OrgScope scope, Guid currentUserId)
    {
        // استثناء‌ی مستند: کاربر آیتم‌های منتسب به خودش را می‌بیند.
        if (item.AssigneeUserId is not null && item.AssigneeUserId == currentUserId)
        {
            return true;
        }

        var plan = item.ActionPlan;

        if (plan is null)
        {
            // بدون بارگذاری ناوبری برنامه، فقط از طریق دامنه نمی‌توان تصمیم گرفت؛
            // fail-closed: فقط در صورت انتصاب به کاربر (بالا بررسی شد) مجاز است.
            return false;
        }

        return IsPlanVisible(plan, scope);
    }

    /// <summary>
    /// حل واحدهای سازمانی قابل‌مشاهده برای ساخت/ویرایش. برخلاف خواندن‌ها،
    /// اینجا مسیر نباید از کلاینت بیاید؛ مسیر از طریق قرارداد سازمان خوانده
    /// می‌شود و در دامنه‌ی کاربر بودن آن بررسی می‌شود.
    /// </summary>
    private async Task<ResolvedOrgScope?> ResolveOrgScopeAsync(
        Guid? requestedOrgUnitId, OrgScope scope, CancellationToken ct)
    {
        if (requestedOrgUnitId is null || requestedOrgUnitId == Guid.Empty)
        {
            // برنامه‌ی سراسری فقط با دامنه‌ی شرکت مجاز است.
            return scope.IsUnrestricted ? new ResolvedOrgScope(null, null) : null;
        }

        // fail-closed: اگر دامنه‌ی قابل‌مشاهده مشخص نیست، هیچ واحدی قابل انتخاب نیست.
        if (!scope.IsUnrestricted && string.IsNullOrWhiteSpace(scope.VisiblePathPrefix))
        {
            return null;
        }

        var path = await _orgUnitRepository.GetPathAsync(requestedOrgUnitId.Value, ct);

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (!scope.IsUnrestricted && !scope.CanAccess(path))
        {
            return null;
        }

        return new ResolvedOrgScope(requestedOrgUnitId, path);
    }

    /// <summary>بارگذاری snapshot نظرسنجی (کد/عنوان) برای ثبت در برنامه.</summary>
    private async Task<SurveySnapshot?> ResolveSurveySnapshotAsync(Guid? surveyId, CancellationToken ct)
    {
        if (surveyId is null || surveyId == Guid.Empty)
        {
            return null;
        }

        var result = await _surveyService.GetByIdAsync(surveyId.Value, ct);

        if (result.IsFailure || result.Value is null)
        {
            return null;
        }

        return new SurveySnapshot(result.Value.Code, result.Value.Title);
    }

    /// <summary>نام نمایشی یک کاربر (snapshot برای تاریخچه).</summary>
    private async Task<string?> ResolveUserNameAsync(Guid? userId, CancellationToken ct)
    {
        if (userId is null || userId == Guid.Empty)
        {
            return null;
        }

        return await _userLookupService.GetDisplayNameAsync(userId.Value, ct);
    }

    private async Task<Dictionary<Guid, IReadOnlyList<ActionItem>>> LoadItemsForPlansAsync(
        IReadOnlyList<ActionPlan> plans, CancellationToken ct)
    {
        var result = new Dictionary<Guid, IReadOnlyList<ActionItem>>();

        foreach (var plan in plans)
        {
            result[plan.Id] = await _itemRepository.ListByPlanAsync(plan.Id, ct);
        }

        return result;
    }

    private static ActionItem CreateItemFromRequest(
        Guid planId, SaveActionItemRequest request, string? assigneeName) => new()
        {
            ActionPlanId = planId,
            Title = request.Title.Trim(),
            Description = request.Description,
            AssigneeUserId = request.AssigneeUserId,
            AssigneeUserName = assigneeName,
            Priority = request.Priority,
            Status = ActionItemStatus.Open,
            DisplayOrder = request.DisplayOrder,
            DueDate = request.DueDate,
            RemindAt = request.RemindAt
        };

    /// <summary>
    /// اجرای گذار وضعیت با قوانین ماشین وضعیت. برگشت: آیا موفق بود؟
    /// </summary>
    private static bool TryTransition(
        ActionItem item, ActionItemStatus newStatus, ActionItemStatus oldStatus, out AppError error)
    {
        if (newStatus == oldStatus)
        {
            error = new AppError("action_same_status", "آیتم از قبل در این وضعیت است.");
            return false;
        }

        switch (newStatus)
        {
            case ActionItemStatus.InProgress when oldStatus == ActionItemStatus.Open:
                item.Start();
                break;

            case ActionItemStatus.Done when oldStatus is ActionItemStatus.Open or ActionItemStatus.InProgress:
                item.Complete();
                break;

            case ActionItemStatus.InProgress when oldStatus == ActionItemStatus.Done:
                item.Reopen();
                break;

            case ActionItemStatus.Cancelled when oldStatus is ActionItemStatus.Open or ActionItemStatus.InProgress:
                item.Cancel();
                break;

            case ActionItemStatus.Open:
                error = new AppError("action_invalid_transition", "باز کردن آیتم از این وضعیت مجاز نیست؛ از «ازسرگیری» استفاده کنید.");
                return false;

            default:
                error = new AppError("action_invalid_transition", "گذار وضعیت درخواست‌شده مجاز نیست.");
                return false;
        }

        error = AppError.None;
        return true;
    }

    // --- نگاشت به DTO ---------------------------------------------------------------

    private static ActionPlanDto ToPlanDto(ActionPlan plan, IReadOnlyList<ActionItem>? items)
    {
        var itemList = items ?? plan.Items;

        var assessed = itemList.Where(i => i.Effectiveness is not EffectivenessRating.NotAssessed).ToList();

        decimal? effectivenessScore = assessed.Count == 0
            ? null
            : Math.Round((decimal)assessed.Count(i => i.Effectiveness == EffectivenessRating.Effective) / assessed.Count * 100, 1);

        return new ActionPlanDto
        {
            Id = plan.Id,
            Title = plan.Title,
            Description = plan.Description,
            Source = plan.Source,
            Status = plan.Status,
            Priority = plan.Priority,
            SurveyId = plan.SurveyId,
            SurveyCode = plan.SurveyCode,
            SurveyTitle = plan.SurveyTitle,
            CampaignId = plan.CampaignId,
            TriggerMetricType = plan.TriggerMetricType,
            TriggerMetricValue = plan.TriggerMetricValue,
            OutcomeMetricValue = plan.OutcomeMetricValue,
            OutcomeMeasuredAt = plan.OutcomeMeasuredAt,
            OrgUnitId = plan.OrgUnitId,
            OrgUnitPath = plan.OrgUnitPath,
            OwnerUserId = plan.OwnerUserId,
            OwnerUserName = plan.OwnerUserName,
            CreatedByUserId = plan.CreatedByUserId,
            CreatedByUserName = plan.CreatedByUserName,
            DueDate = plan.DueDate,
            CompletedAt = plan.CompletedAt,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt,
            TotalItemCount = itemList.Count,
            CompletedItemCount = itemList.Count(i => i.Status == ActionItemStatus.Done),
            ProgressPercentage = itemList.Count == 0 ? 0 : Math.Round((decimal)itemList.Count(i => i.Status == ActionItemStatus.Done) / itemList.Count * 100, 1),
            EffectivenessScore = effectivenessScore,
            HasOverdueItems = itemList.Any(i => i.IsOverdue)
        };
    }

    private static ActionItemDto ToItemDto(ActionItem item, Guid currentUserId)
    {
        return new ActionItemDto
        {
            Id = item.Id,
            ActionPlanId = item.ActionPlanId,
            PlanTitle = item.ActionPlan?.Title,
            PlanStatus = item.ActionPlan?.Status,
            PlanPriority = item.ActionPlan?.Priority,
            Title = item.Title,
            Description = item.Description,
            AssigneeUserId = item.AssigneeUserId,
            AssigneeUserName = item.AssigneeUserName,
            Priority = item.Priority,
            Status = item.Status,
            DisplayOrder = item.DisplayOrder,
            DueDate = item.DueDate,
            RemindAt = item.RemindAt,
            EscalationLevel = item.EscalationLevel,
            EscalatedAt = item.EscalatedAt,
            StartedAt = item.StartedAt,
            CompletedAt = item.CompletedAt,
            Effectiveness = item.Effectiveness,
            EffectivenessNote = item.EffectivenessNote,
            EffectivenessAssessedAt = item.EffectivenessAssessedAt,
            CommentCount = item.Comments.Count,
            EvidenceCount = item.Evidence.Count,
            OrgUnitId = item.ActionPlan?.OrgUnitId,
            OrgUnitPath = item.ActionPlan?.OrgUnitPath,
            IsAssignedToMe = item.AssigneeUserId is not null && item.AssigneeUserId == currentUserId,
            IsOverdue = item.IsOverdue,
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    private static ActionCommentDto ToCommentDto(ActionComment comment) => new()
    {
        Id = comment.Id,
        ActionItemId = comment.ActionItemId,
        AuthorUserId = comment.AuthorUserId,
        AuthorUserName = comment.AuthorUserName,
        Body = comment.Body,
        CreatedAt = comment.CreatedAt
    };

    private static ActionEvidenceDto ToEvidenceDto(ActionEvidence evidence) => new()
    {
        Id = evidence.Id,
        ActionItemId = evidence.ActionItemId,
        FileName = evidence.FileName,
        ContentType = evidence.ContentType,
        FileSizeBytes = evidence.FileSizeBytes,
        UploadedByUserId = evidence.UploadedByUserId,
        UploadedByUserName = evidence.UploadedByUserName,
        UploadedAt = evidence.UploadedAt
    };

    /// <summary>Snapshot سبک نظرسنجی برای ثبت در زمان ایجاد برنامه.</summary>
    private sealed record SurveySnapshot(string Code, string Title);

    /// <summary>نتیجه‌ی حل واحد سازمانی مقصدِ قابل‌مشاهده.</summary>
    private sealed record ResolvedOrgScope(Guid? OrgUnitId, string? OrgUnitPath);
}
