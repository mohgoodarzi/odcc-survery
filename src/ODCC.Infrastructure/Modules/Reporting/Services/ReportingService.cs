using Microsoft.Extensions.Logging;
using ODCC.Application.Abstractions;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Reporting.Abstractions;
using ODCC.Application.Modules.Reporting.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Reporting.Entities;
using ODCC.Domain.Modules.Reporting.Enums;

namespace ODCC.Infrastructure.Modules.Reporting.Services;

/// <summary>
/// سرویس مدیریت گزارش‌ها: تعاریف، زمان‌بندی، اجرا و دانلود خروجی.
///
/// **اجرای یک گزارش** سه مرحله دارد که هر کدام در یک ذخیره‌سازی جداگانه
/// ثبت می‌شوند تا تاریخچه حتی در صورت شکست وسط کار باقی بماند:
/// <list type="number">
///   <item>ایجاد ردیف اجرا با وضعیت <c>Pending</c> و ذخیره (صف).</item>
///   <item>گذر به <c>Running</c> و ذخیره.</item>
///   <item>جمع‌آوری داده، رندر، ذخیره‌ی فایل، ثبت موفقیت/شکست و ذخیره.</item>
/// </list>
///
/// **حریم خصوصی:** داده‌ی گزارش فقط از طریق <see cref="IReportDataAssembler"/>
/// از تجمع‌های تحلیلات جمع می‌شود. هیچ شناسه‌ی پاسخ‌گو در خروجی نیست.
/// </summary>
public sealed class ReportingService(
    IReportDefinitionRepository definitionRepository,
    IReportExecutionRepository executionRepository,
    IReportDataAssembler dataAssembler,
    IEnumerable<IReportRenderer> renderers,
    IReportArtifactStore artifactStore,
    IOrgUnitRepository orgUnitRepository,
    ICurrentUserService currentUserService,
    IReportingUnitOfWork unitOfWork,
    ILogger<ReportingService> logger) : IReportingService
{
    private readonly IReportDefinitionRepository _definitionRepository = definitionRepository;
    private readonly IReportExecutionRepository _executionRepository = executionRepository;
    private readonly IReportDataAssembler _dataAssembler = dataAssembler;
    private readonly Dictionary<ReportFormat, IReportRenderer> _renderers = renderers
        .ToDictionary(r => r.Format);
    private readonly IReportArtifactStore _artifactStore = artifactStore;
    private readonly IOrgUnitRepository _orgUnitRepository = orgUnitRepository;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly IReportingUnitOfWork _unitOfWork = unitOfWork;
    private readonly ILogger<ReportingService> _logger = logger;

    private static readonly Action<ILogger, Guid, Exception> ExecutionFailed = LoggerMessage.Define<Guid>(
        LogLevel.Error,
        new EventId(1, "ReportExecutionFailed"),
        "اجرای گزارش {ReportDefinitionId} شکست خورد.");

    /// <inheritdoc/>
    public async Task<PagedResult<ReportDefinitionDto>> SearchAsync(
        ReportSearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _definitionRepository.CountAsync(request, ct);
        var definitions = await _definitionRepository.SearchAsync(request, ct);

        var recentExecutions = await LoadRecentSuccessAsync(definitions, ct);

        var items = definitions
            .Select(d => ToDto(d, recentExecutions.GetValueOrDefault(d.Id)))
            .ToList();

        return new PagedResult<ReportDefinitionDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<ReportDefinitionDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var definition = await _definitionRepository.GetByIdAsync(id, ct);
        if (definition is null)
        {
            return Result.Failure<ReportDefinitionDto>("report_not_found", "گزارش یافت نشد.");
        }

        return Result.Success(ToDto(definition, await LoadLastSuccessAsync(definition.Id, ct)));
    }

    /// <inheritdoc/>
    public async Task<Result<ReportDefinitionDto>> CreateAsync(
        SaveReportRequest request, CancellationToken ct = default)
    {
        var scope = await ResolveOrgScopeAsync(request.OrgUnitId, ct);

        // گزارش خلاصه‌ی داشبورد به نظرسنجی خاصی محدود نیست؛ حتی اگر کلاینت
        // مقداری فرستاده باشد، اینجا به‌جای تکیه بر اعتبارسنجی API خنثی می‌شود.
        var surveyId = NormalizeSurveyId(request.Type, request.SurveyId);

        var definition = new ReportDefinition
        {
            Name = request.Name,
            Description = request.Description,
            Type = request.Type,
            Format = request.Format,
            Schedule = request.Schedule,
            Status = ReportStatus.Draft,
            SurveyId = surveyId,
            From = request.From,
            To = request.To,
            OrgUnitId = scope.OrgUnitId,
            OrgUnitPath = scope.OrgUnitPath,
            IncludeDescendants = request.IncludeDescendants,
            OwnerUserId = _currentUserService.UserId,
            OwnerUserName = _currentUserService.UserName,
            RetentionCount = request.RetentionCount
        };

        // فعال‌سازی زمان‌بندی را محاسبه می‌کند. بدون این مرحله یک گزارش
        // زمان‌بندی‌شده‌ی تازه‌ساز هیچ NextRunAtی نداشت و زمان‌بند هرگز آن
        // را اجرا نمی‌کرد.
        if (request.ActivateImmediately)
        {
            definition.Activate();
        }

        definition.RaiseCreatedEvent();

        await _definitionRepository.AddAsync(definition, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(definition, null));
    }

    /// <inheritdoc/>
    public async Task<Result<ReportDefinitionDto>> UpdateAsync(
        Guid id, SaveReportRequest request, CancellationToken ct = default)
    {
        var definition = await _definitionRepository.GetByIdAsync(id, ct);
        if (definition is null)
        {
            return Result.Failure<ReportDefinitionDto>("report_not_found", "گزارش یافت نشد.");
        }

        if (definition.Status == ReportStatus.Archived)
        {
            return Result.Failure<ReportDefinitionDto>("report_archived", "گزارش بایگانی‌شده قابل ویرایش نیست.");
        }

        var scope = await ResolveOrgScopeAsync(request.OrgUnitId, ct);

        var surveyId = NormalizeSurveyId(request.Type, request.SurveyId);

        var wasActive = definition.Status == ReportStatus.Active;

        definition.Update(
            request.Name,
            request.Description,
            request.Type,
            request.Format,
            request.Schedule,
            surveyId,
            surveyCode: definition.SurveyCode,
            surveyTitle: definition.SurveyTitle,
            request.From,
            request.To,
            scope.OrgUnitId,
            scope.OrgUnitPath,
            request.IncludeDescendants,
            request.RetentionCount);

        // وضعیت فعال‌بودن پس از ویرایش حفظ می‌شود مگر آنکه صریحاً خاموش شده باشد.
        if (wasActive && !request.ActivateImmediately && definition.Status != ReportStatus.Archived)
        {
            definition.Activate();
        }
        else if (!wasActive && request.ActivateImmediately)
        {
            definition.Activate();
        }

        definition.RaiseUpdatedEvent();

        _definitionRepository.Update(definition);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(definition, await LoadLastSuccessAsync(definition.Id, ct)));
    }

    /// <inheritdoc/>
    public async Task<Result<ReportDefinitionDto>> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var definition = await _definitionRepository.GetByIdAsync(id, ct);
        if (definition is null)
        {
            return Result.Failure<ReportDefinitionDto>("report_not_found", "گزارش یافت نشد.");
        }

        if (definition.Status == ReportStatus.Archived)
        {
            return Result.Failure<ReportDefinitionDto>("report_archived", "گزارش بایگانی‌شده قابل فعال‌سازی نیست.");
        }

        definition.Activate();
        definition.RaiseActivatedEvent();

        _definitionRepository.Update(definition);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(ToDto(definition, await LoadLastSuccessAsync(definition.Id, ct)));
    }

    /// <inheritdoc/>
    public async Task<Result> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var definition = await _definitionRepository.GetByIdAsync(id, ct);
        if (definition is null)
        {
            return Result.Failure("report_not_found", "گزارش یافت نشد.");
        }

        if (definition.Status == ReportStatus.Archived)
        {
            return Result.Success();
        }

        definition.Archive();
        definition.RaiseArchivedEvent();

        _definitionRepository.Update(definition);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<ReportExecutionDto>> ExecuteAsync(
        Guid reportDefinitionId, CancellationToken ct = default)
    {
        var definition = await _definitionRepository.GetByIdAsync(reportDefinitionId, ct);
        if (definition is null)
        {
            return Result.Failure<ReportExecutionDto>("report_not_found", "گزارش یافت نشد.");
        }

        if (definition.Status == ReportStatus.Archived)
        {
            return Result.Failure<ReportExecutionDto>("report_archived", "گزارش بایگانی‌شده قابل اجرا نیست.");
        }

        // --- ۱. صف: ثبت اجرا پیش از شروع ----------------------------------
        var execution = new ReportExecution
        {
            ReportDefinitionId = definition.Id,
            ReportName = definition.Name,
            ReportType = definition.Type,
            Format = definition.Format,
            Status = ReportExecutionStatus.Pending,
            QueuedAt = DateTime.UtcNow,
            TriggeredBy = _currentUserService.UserId,
            TriggeredByName = _currentUserService.UserName
        };

        await _executionRepository.AddAsync(execution, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        // --- ۲. اجرا ---------------------------------------------------------
        var result = await RunExecutionAsync(definition, execution, ct);

        if (result.IsFailure)
        {
            return Result.Failure<ReportExecutionDto>(result.Error);
        }

        return Result.Success(ToExecutionDto(execution));
    }

    /// <inheritdoc/>
    public async Task<Result<ReportExecutionDto>> GetExecutionAsync(Guid executionId, CancellationToken ct = default)
    {
        var execution = await _executionRepository.GetByIdAsync(executionId, ct);
        if (execution is null)
        {
            return Result.Failure<ReportExecutionDto>("execution_not_found", "اجرای گزارش یافت نشد.");
        }

        return Result.Success(ToExecutionDto(execution));
    }

    /// <inheritdoc/>
    public async Task<PagedResult<ReportExecutionDto>> SearchExecutionsAsync(
        ExecutionSearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _executionRepository.CountAsync(request, ct);
        var executions = await _executionRepository.SearchAsync(request, ct);

        return new PagedResult<ReportExecutionDto>
        {
            Items = executions.Select(ToExecutionDto).ToList(),
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    /// <inheritdoc/>
    public async Task<Result<ReportArtifact>> GetArtifactAsync(Guid executionId, CancellationToken ct = default)
    {
        var execution = await _executionRepository.GetByIdAsync(executionId, ct);
        if (execution is null)
        {
            return Result.Failure<ReportArtifact>("execution_not_found", "اجرای گزارش یافت نشد.");
        }

        if (execution.Status != ReportExecutionStatus.Succeeded
            || string.IsNullOrWhiteSpace(execution.FilePath)
            || string.IsNullOrWhiteSpace(execution.FileName))
        {
            return Result.Failure<ReportArtifact>("artifact_unavailable", "خروجی این اجرا در دسترس نیست.");
        }

        if (!_artifactStore.Exists(execution.FilePath))
        {
            return Result.Failure<ReportArtifact>("artifact_missing", "فایل خروجی یافت نشد (ممکن است پاک شده باشد).");
        }

        var stream = await _artifactStore.OpenReadAsync(execution.FilePath, ct);

        return Result.Success(new ReportArtifact
        {
            Content = stream,
            FileName = execution.FileName,
            ContentType = execution.Format == ReportFormat.Pdf ? "application/pdf"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            SizeBytes = execution.FileSizeBytes ?? stream.Length
        });
    }

    /// <inheritdoc/>
    public async Task<int> ProcessDueReportsAsync(DateTime asOf, CancellationToken ct = default)
    {
        var due = await _definitionRepository.ListDueAsync(asOf, ct);

        if (due.Count == 0)
        {
            return 0;
        }

        var processed = 0;

        foreach (var definition in due)
        {
            // هر گزارش در یک UnitOfWork جداگانه اجرا می‌شود تا شکست یکی، بقیه را متوقف نکند.
            try
            {
                await ExecuteAsync(definition.Id, ct);
                processed++;
            }
            catch (Exception ex)
            {
                // خطای غیرمنتظره در اجرای زمان‌بندی‌شده: لاگ می‌شود و ادامه می‌دهد.
                ExecutionFailed(_logger, definition.Id, ex);
            }
        }

        return processed;
    }

    // --- خط لوله‌ی اجرا ------------------------------------------------------

    /// <summary>
    /// اجرای واقعی یک ردیف اجرا: گذر به Running، جمع‌آوری داده، رندر، ذخیره‌ی
    /// خروجی و ثبت نتیجه. شکست‌ها در ردیف اجرا ثبت می‌شوند تا تاریخچه کامل بماند.
    /// </summary>
    private async Task<Result> RunExecutionAsync(
        ReportDefinition definition, ReportExecution execution, CancellationToken ct)
    {
        try
        {
            execution.MarkRunning();
            _executionRepository.Update(execution);
            await _unitOfWork.SaveChangesAsync(ct);

            // --- جمع‌آوری داده از ماژول تحلیلات (فقط تجمع‌ها) ---------------------
            var bundle = await _dataAssembler.AssembleAsync(definition, ct);
            if (bundle.IsFailure)
            {
                await FailExecutionAsync(definition, execution, bundle.Error.Message, ct);
                return Result.Failure<ReportExecutionDto>("report_data_unavailable", bundle.Error.Message);
            }

            // --- رندر بر اساس قالب ---------------------------------------------
            if (!_renderers.TryGetValue(definition.Format, out var renderer))
            {
                var message = $"قالب خروجی «{definition.Format}» پشتیبانی نمی‌شود.";
                await FailExecutionAsync(definition, execution, message, ct);
                return Result.Failure<ReportExecutionDto>("report_format_unsupported", message);
            }

            var rendered = await renderer.RenderAsync(bundle.Value!, ct);

            // --- ذخیره‌ی فایل ---------------------------------------------------
            await using (rendered.Content)
            {
                // نام فایل باید برای هر اجرا یکتا باشد، وگرنه اجرای دومِ همان
                // گزارش روی فایل اجرای اول می‌نویسد (CreateNew) و شکست می‌خورد.
                var uniqueFileName = MakeUniqueFileName(rendered.FileName, execution.Id);

                var stored = await _artifactStore.SaveAsync(rendered.Content, uniqueFileName, ct);

                execution.MarkSucceeded(uniqueFileName, stored.RelativePath, stored.SizeBytes, rendered.RowCount);
            }

            definition.RecordExecution(execution.QueuedAt, bundle.Value!.Sections.Count > 0 ? definition.SurveyCode : null);

            _executionRepository.Update(execution);
            _definitionRepository.Update(definition);
            await _unitOfWork.SaveChangesAsync(ct);

            // --- پنجره‌ی نگه‌داری: حذف نرم اجراهای قدیمی ------------------------
            await EnforceRetentionAsync(definition, ct);

            return Result.Success();
        }
        catch (Exception ex)
        {
            await FailExecutionAsync(definition, execution, ex.Message, ct);
            ExecutionFailed(_logger, definition.Id, ex);
            return Result.Failure<ReportExecutionDto>("report_execution_failed", ex.Message);
        }
    }

    /// <summary>ثبت شکست اجرا در ردیف آن.</summary>
    private async Task FailExecutionAsync(
        ReportDefinition definition, ReportExecution execution, string errorMessage, CancellationToken ct)
    {
        try
        {
            execution.MarkFailed(errorMessage);
            definition.RecordExecution(execution.QueuedAt);

            _executionRepository.Update(execution);
            _definitionRepository.Update(definition);
            await _unitOfWork.SaveChangesAsync(ct);
        }
        catch
        {
            // اگر خود ثبت شکست هم بیفتد، فقط لاگ می‌شود (در RunExecutionAsync لاگ شده است).
        }
    }

    /// <summary>
    /// حذف نرم اجراهای فراتر از پنجره‌ی نگه‌داری و پاک‌سازی فایل‌های فیزیکی آن‌ها.
    /// </summary>
    private async Task EnforceRetentionAsync(ReportDefinition definition, CancellationToken ct)
    {
        var keep = Math.Max(1, definition.RetentionCount);
        var executions = await _executionRepository.ListByDefinitionAsync(definition.Id, keep + 50, ct);

        if (executions.Count <= keep)
        {
            return;
        }

        // قدیمی‌ترین اجراهای فراتر از پنجره (لیست از جدید به قدیم مرتب است).
        var stale = executions.Skip(keep).ToList();

        foreach (var execution in stale)
        {
            // فایل فیزیکی حذف می‌شود تا فضای دیسک اشغال نشود.
            if (!string.IsNullOrWhiteSpace(execution.FilePath) && _artifactStore.Exists(execution.FilePath))
            {
                try
                {
                    await _artifactStore.DeleteAsync(execution.FilePath, ct);
                }
                catch
                {
                    // حذف فایل ناموفق بود؛ ردیف همچنان نرم حذف می‌شود.
                }
            }

            execution.IsDeleted = true;
            _executionRepository.Update(execution);
        }

        await _unitOfWork.SaveChangesAsync(ct);
    }

    // --- کمک‌کننده‌ها ----------------------------------------------------------

    /// <summary>
    /// گزارش خلاصه‌ی داشبورد کل شرکت است و به نظرسنجی خاصی وابسته نیست.
    /// مقدار ارسالیِ کلاینت (در صورت وجود) نادیده گرفته می‌شود تا این
    /// نامعرفی در خود سرویس حفظ شود، نه فقط در اعتبارسنجی API.
    /// </summary>
    private static Guid? NormalizeSurveyId(ReportType type, Guid? surveyId) =>
        type == ReportType.DashboardSummary ? null : surveyId;

    /// <summary>
    /// ساخت نام فایل یکتا برای هر اجرا: شناسه‌ی اجرا به نام اصلی افزوده
    /// می‌شود تا دو اجرای یک گزارش هرگز روی فایل یکدیگر ننویسند. از کل
    /// شناسه‌ی اجرا استفاده می‌شود چون شناسه‌های GUID نسخه ۷ در یک میلی‌ثانیه
    /// می‌توانند پیشوند یکسان داشته باشند.
    /// </summary>
    private static string MakeUniqueFileName(string fileName, Guid executionId)
    {
        var dotIndex = fileName.LastIndexOf('.');

        // نام فایل بدون پسوند، در صورت نبودن نقطه کل نام است.
        var stem = dotIndex > 0 ? fileName[..dotIndex] : fileName;
        var extension = dotIndex > 0 ? fileName[dotIndex..] : string.Empty;

        return $"{stem}-{executionId}{extension}";
    }

    /// <summary>حل مسیر سازمانی تعریف (در صورت انتخاب واحد).</summary>
    private async Task<(Guid? OrgUnitId, string? OrgUnitPath)> ResolveOrgScopeAsync(Guid? orgUnitId, CancellationToken ct)
    {
        if (orgUnitId is null || orgUnitId == Guid.Empty)
        {
            return (null, null);
        }

        var path = await _orgUnitRepository.GetPathAsync(orgUnitId.Value, ct);

        return (orgUnitId, path);
    }

    /// <summary>آخرین اجرای موفق یک تعریف (برای نمایش در فهرست).</summary>
    private async Task<ReportExecution?> LoadLastSuccessAsync(Guid definitionId, CancellationToken ct)
    {
        var executions = await _executionRepository.ListByDefinitionAsync(definitionId, 20, ct);

        return executions.FirstOrDefault(e => e.Status == ReportExecutionStatus.Succeeded);
    }

    /// <summary>آخرین اجرای موفق چند تعریف (یک پرس‌وجوی گروهی برای فهرست).</summary>
    private async Task<Dictionary<Guid, ReportExecution>> LoadRecentSuccessAsync(
        IReadOnlyList<ReportDefinition> definitions, CancellationToken ct)
    {
        var result = new Dictionary<Guid, ReportExecution>();

        foreach (var definition in definitions)
        {
            var last = await LoadLastSuccessAsync(definition.Id, ct);
            if (last is not null)
            {
                result[definition.Id] = last;
            }
        }

        return result;
    }

    private static ReportDefinitionDto ToDto(ReportDefinition definition, ReportExecution? lastSuccess) => new()
    {
        Id = definition.Id,
        Name = definition.Name,
        Description = definition.Description,
        Type = definition.Type,
        Format = definition.Format,
        Schedule = definition.Schedule,
        Status = definition.Status,
        SurveyId = definition.SurveyId,
        SurveyCode = definition.SurveyCode,
        SurveyTitle = definition.SurveyTitle,
        From = definition.From,
        To = definition.To,
        OrgUnitId = definition.OrgUnitId,
        OrgUnitPath = definition.OrgUnitPath,
        IncludeDescendants = definition.IncludeDescendants,
        OwnerUserId = definition.OwnerUserId,
        OwnerUserName = definition.OwnerUserName,
        LastExecutedAt = definition.LastExecutedAt,
        NextRunAt = definition.NextRunAt,
        RetentionCount = definition.RetentionCount,
        CreatedAt = definition.CreatedAt,
        UpdatedAt = definition.UpdatedAt,
        LastSuccessAt = lastSuccess?.CompletedAt
    };

    private static ReportExecutionDto ToExecutionDto(ReportExecution execution) => new()
    {
        Id = execution.Id,
        ReportDefinitionId = execution.ReportDefinitionId,
        ReportName = execution.ReportName,
        ReportType = execution.ReportType,
        Format = execution.Format,
        Status = execution.Status,
        QueuedAt = execution.QueuedAt,
        StartedAt = execution.StartedAt,
        CompletedAt = execution.CompletedAt,
        TriggeredBy = execution.TriggeredBy,
        TriggeredByName = execution.TriggeredByName,
        FileName = execution.FileName,
        FileSizeBytes = execution.FileSizeBytes,
        RowCount = execution.RowCount,
        ErrorMessage = execution.ErrorMessage
    };
}
