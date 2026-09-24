using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Application.Languages;
using ODCC.Application.Modules.Campaign.Abstractions;
using ODCC.Application.Modules.Campaign.Dtos;
using ODCC.Application.Modules.Organization.Abstractions;
using ODCC.Application.Modules.Survey.Abstractions;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Entities;
using ODCC.Domain.Modules.Campaign.Enums;
using ODCC.Domain.Modules.Campaign.Events;
using ODCC.Domain.Modules.Survey.Enums;
using CampaignEntity = ODCC.Domain.Modules.Campaign.Entities.Campaign;
using EmployeeEntity = ODCC.Domain.Modules.Organization.Entities.Employee;
using ODCC.Infrastructure.Modules.Campaign.Persistence;

namespace ODCC.Infrastructure.Modules.Campaign.Services;

/// <summary>
/// پیاده‌سازی سرویس کمپین‌ها.
///
/// **مرزهای ماژول‌ها:** این سرویس برای حل جمعیت هدف فقط از قرارداد
/// <see cref="IEmployeeRepository"/> (ماژول سازمان) و برای بررسی نظرسنجی فقط از
/// <see cref="ISurveyRepository"/> (ماژول نظرسنجی) استفاده می‌کند — هرگز از DbContext
/// آن ماژول‌ها.
///
/// **اجرای کمپین (<see cref="LaunchAsync"/>):** جمعیت هدف حل می‌شود، برای هر گیرنده
/// یک ردیف توزیع ساخته می‌شود و کمپین به «در حال اجرا» گذار می‌یابد — همه در یک
/// تراکنش تا هیچ‌گاه کمپینی بدون ردیف توزیع در حال اجرا نباشد.
///
/// **یادآورها:** <see cref="ProcessDueRemindersAsync"/> یادآورهای سررسیده را
/// علامت‌گذاری می‌کند و شمارنده‌ی یادآورِ گیرندگانی که هنوز پاسخ نداده‌اند را
/// افزایش می‌دهد. ارسال واقعی پیام در ماژول اعلان‌ها (Notification) انجام می‌شود.
/// </summary>
public sealed class CampaignService(
    ICampaignRepository campaignRepository,
    IDistributionRepository distributionRepository,
    ISurveyRepository surveyRepository,
    IEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ICampaignUnitOfWork unitOfWork,
    CampaignDbContext dbContext) : ICampaignService
{
    private readonly ICampaignRepository _campaignRepository = campaignRepository;
    private readonly IDistributionRepository _distributionRepository = distributionRepository;
    private readonly ISurveyRepository _surveyRepository = surveyRepository;
    private readonly IEmployeeRepository _employeeRepository = employeeRepository;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly ICampaignUnitOfWork _unitOfWork = unitOfWork;
    private readonly CampaignDbContext _dbContext = dbContext;

    public async Task<PagedResult<CampaignSummaryDto>> SearchAsync(CampaignSearchRequest request, CancellationToken ct = default)
    {
        var totalCount = await _campaignRepository.CountAsync(request, ct);
        var campaigns = await _campaignRepository.SearchAsync(request, ct);

        // شمارش توزیع‌ها در یک پرس‌وجوی گروهی، تا فهرست نیازی به بارگذاری هر کمپین نداشته باشد.
        var distributionCounts = campaigns.Count > 0
            ? await _distributionRepository.CountByCampaignsAsync(campaigns.Select(c => c.Id).ToList(), ct)
            : new Dictionary<Guid, int>();

        var dtos = campaigns.Select(c => new CampaignSummaryDto
        {
            Id = c.Id,
            Code = c.Code,
            Status = c.Status,
            Title = c.Localizations.Pick(Language.Fa)?.Title
                ?? c.Localizations.FirstOrDefault()?.Title
                ?? c.Code,
            SurveyCode = c.SurveyCode,
            AudienceType = c.AudienceType,
            Channel = c.Channel,
            ScheduledAt = c.ScheduledAt,
            StartedAt = c.StartedAt,
            TotalDistributions = distributionCounts.TryGetValue(c.Id, out var count) ? count : 0,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        }).ToList();

        return new PagedResult<CampaignSummaryDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 100)
        };
    }

    public async Task<Result<CampaignDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
        {
            return Result.Failure<CampaignDto>("campaign_not_found", "کمپین یافت نشد.");
        }

        return Result.Success(await ToDtoAsync(campaign, ct));
    }

    public async Task<Result<CampaignDto>> CreateAsync(SaveCampaignRequest request, CancellationToken ct = default)
    {
        var survey = await LoadUsableSurveyAsync(request.SurveyId, ct);
        if (survey is null)
        {
            return Result.Failure<CampaignDto>("survey_not_usable", "نظرسنجی ارجاع‌شده وجود ندارد یا قابل استفاده نیست.");
        }

        var owner = await _campaignRepository.FindByCodeAsync(request.Code, ct);
        if (owner is not null)
        {
            return Result.Failure<CampaignDto>("campaign_code_taken", "این کد کمپین قبلاً استفاده شده است.");
        }

        var campaign = new CampaignEntity
        {
            Code = request.Code,
            Status = CampaignStatus.Draft,
            SurveyId = survey.Id,
            SurveyCode = survey.Code,
            AudienceType = request.AudienceType,
            IncludeInactiveEmployees = request.IncludeInactiveEmployees,
            Channel = request.Channel,
            ScheduledAt = request.ScheduledAt,
            EndsAt = request.EndsAt
        };

        ApplyLocalizations(campaign, request.Localizations);
        ApplyTargets(campaign, request);

        // جمعیت هدف باید هم‌زمان با ایجاد کمپین پیکربندی شود تا کمپین غیرقابل‌اجرا ساخته نشود.
        if (!campaign.IsAudienceConfigured)
        {
            return Result.Failure<CampaignDto>(
                "campaign_audience_targets_required",
                "نوع جمعیت هدف انتخاب‌شده نیازمند تعیین اهداف (واحدها یا کارمندان) است.");
        }

        var reminderResult = ApplyReminders(campaign, request.Reminders);
        if (reminderResult.IsFailure)
        {
            return Result.Failure<CampaignDto>(reminderResult.Error);
        }

        campaign.RaiseDomainEvent(new CampaignCreatedEvent(campaign.Id, campaign.Code, campaign.SurveyId, _currentUserService.UserId));

        await _campaignRepository.AddAsync(campaign, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(await ToDtoAsync(campaign, ct));
    }

    public async Task<Result<CampaignDto>> UpdateAsync(Guid id, SaveCampaignRequest request, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
        {
            return Result.Failure<CampaignDto>("campaign_not_found", "کمپین یافت نشد.");
        }

        // کمپین اجراشده قابل ویرایش نیست چون توزیع‌ها از تنظیمات فعلی ساخته شده‌اند.
        if (campaign.Status != CampaignStatus.Draft)
        {
            return Result.Failure<CampaignDto>("campaign_is_not_draft", "تنها کمپین‌های پیش‌نویس قابل ویرایش هستند.");
        }

        var survey = await LoadUsableSurveyAsync(request.SurveyId, ct);
        if (survey is null)
        {
            return Result.Failure<CampaignDto>("survey_not_usable", "نظرسنجی ارجاع‌شده وجود ندارد یا قابل استفاده نیست.");
        }

        if (!string.Equals(campaign.Code, request.Code, StringComparison.Ordinal))
        {
            var owner = await _campaignRepository.FindByCodeAsync(request.Code, ct);
            if (owner is not null && owner.Id != id)
            {
                return Result.Failure<CampaignDto>("campaign_code_taken", "این کد کمپین قبلاً استفاده شده است.");
            }
        }

        campaign.Code = request.Code;
        campaign.SurveyId = survey.Id;
        campaign.SurveyCode = survey.Code;
        campaign.AudienceType = request.AudienceType;
        campaign.IncludeInactiveEmployees = request.IncludeInactiveEmployees;
        campaign.Channel = request.Channel;
        campaign.ScheduledAt = request.ScheduledAt;
        campaign.EndsAt = request.EndsAt;

        ApplyLocalizations(campaign, request.Localizations);
        ApplyTargets(campaign, request);

        // جمعیت هدف باید هم‌زمان با ویرایش کمپین پیکربندی شود تا کمپین غیرقابل‌اجرا بماند.
        if (!campaign.IsAudienceConfigured)
        {
            return Result.Failure<CampaignDto>(
                "campaign_audience_targets_required",
                "نوع جمعیت هدف انتخاب‌شده نیازمند تعیین اهداف (واحدها یا کارمندان) است.");
        }

        var reminderResult = ApplyReminders(campaign, request.Reminders);
        if (reminderResult.IsFailure)
        {
            return Result.Failure<CampaignDto>(reminderResult.Error);
        }

        campaign.RaiseDomainEvent(new CampaignUpdatedEvent(campaign.Id, campaign.Code, _currentUserService.UserId));

        _campaignRepository.Update(campaign);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(await ToDtoAsync(campaign, ct));
    }

    public async Task<Result<CampaignDto>> ScheduleAsync(Guid id, DateTime scheduledAt, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
        {
            return Result.Failure<CampaignDto>("campaign_not_found", "کمپین یافت نشد.");
        }

        if (campaign.Status != CampaignStatus.Draft)
        {
            return Result.Failure<CampaignDto>("campaign_is_not_draft", "فقط کمپین‌های پیش‌نویس قابل زمان‌بندی هستند.");
        }

        if (scheduledAt <= DateTime.UtcNow)
        {
            return Result.Failure<CampaignDto>("campaign_schedule_in_past", "زمان شروع باید در آینده باشد.");
        }

        campaign.Schedule(scheduledAt);
        campaign.RaiseDomainEvent(new CampaignScheduledEvent(campaign.Id, campaign.Code, scheduledAt, _currentUserService.UserId));

        _campaignRepository.Update(campaign);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(await ToDtoAsync(campaign, ct));
    }

    public async Task<Result<CampaignLaunchResultDto>> LaunchAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
        {
            return Result.Failure<CampaignLaunchResultDto>("campaign_not_found", "کمپین یافت نشد.");
        }

        if (!campaign.CanLaunch)
        {
            return Result.Failure<CampaignLaunchResultDto>("campaign_cannot_launch", "فقط کمپین‌های پیش‌نویس یا زمان‌بندی‌شده قابل اجرا هستند.");
        }

        if (!campaign.IsAudienceConfigured)
        {
            return Result.Failure<CampaignLaunchResultDto>("campaign_audience_empty", "جمعیت هدف این کمپین خالی است.");
        }

        var survey = await LoadUsableSurveyAsync(campaign.SurveyId, ct);
        if (survey is null || survey.Status != SurveyStatus.Active)
        {
            return Result.Failure<CampaignLaunchResultDto>("survey_not_active", "نظرسنجیِ این کمپین فعال نیست؛ ابتدا آن را منتشر و شروع کنید.");
        }

        // حل جمعیت هدف (از طریق قرارداد ماژول سازمان).
        var audience = await ResolveAudienceAsync(campaign, ct);

        // ساخت ردیف‌های توزیع (یک ردیف به ازای هر گیرنده، بدون تکرار).
        var seen = new HashSet<Guid>();
        var distributions = new List<Distribution>(audience.Count);

        foreach (var employee in audience)
        {
            if (!seen.Add(employee.Id))
            {
                continue;
            }

            distributions.Add(new Distribution
            {
                CampaignId = campaign.Id,
                EmployeeId = employee.Id,
                WorkEmail = employee.WorkEmail,
                Status = DistributionStatus.Pending
            });
        }

        await _distributionRepository.AddRangeAsync(distributions, ct);

        campaign.Launch();
        campaign.RaiseDomainEvent(new CampaignLaunchedEvent(campaign.Id, campaign.Code, distributions.Count, _currentUserService.UserId));

        _campaignRepository.Update(campaign);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new CampaignLaunchResultDto
        {
            CampaignId = campaign.Id,
            Code = campaign.Code,
            Status = campaign.Status,
            ResolvedRecipientCount = audience.Count,
            CreatedDistributionCount = distributions.Count
        });
    }

    public async Task<Result<CampaignDto>> CompleteAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
        {
            return Result.Failure<CampaignDto>("campaign_not_found", "کمپین یافت نشد.");
        }

        if (campaign.Status != CampaignStatus.Running)
        {
            return Result.Failure<CampaignDto>("campaign_is_not_running", "فقط کمپین‌های «در حال اجرا» قابل تکمیل هستند.");
        }

        campaign.Complete();
        campaign.RaiseDomainEvent(new CampaignCompletedEvent(campaign.Id, campaign.Code, _currentUserService.UserId));

        _campaignRepository.Update(campaign);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(await ToDtoAsync(campaign, ct));
    }

    public async Task<Result<CampaignDto>> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
        {
            return Result.Failure<CampaignDto>("campaign_not_found", "کمپین یافت نشد.");
        }

        if (campaign.Status == CampaignStatus.Archived)
        {
            return Result.Failure<CampaignDto>("campaign_is_archived", "کمپین از قبل بایگانی شده است.");
        }

        campaign.Archive();
        campaign.RaiseDomainEvent(new CampaignArchivedEvent(campaign.Id, campaign.Code, _currentUserService.UserId));

        _campaignRepository.Update(campaign);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(await ToDtoAsync(campaign, ct));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(id, ct);
        if (campaign is null)
        {
            return Result.Failure("campaign_not_found", "کمپین یافت نشد.");
        }

        // فقط پیش‌نویس‌ها حذف می‌شوند تا تاریخچه‌ی توزیع‌ها در کمپین‌های اجراشده حفظ شود.
        if (campaign.Status != CampaignStatus.Draft)
        {
            return Result.Failure("campaign_not_deletable", "فقط کمپین‌های پیش‌نویس قابل حذف هستند.");
        }

        _campaignRepository.Remove(campaign);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // --- توزیع و یادآور -------------------------------------------------------

    public async Task<PagedResult<DistributionDto>> GetDistributionsAsync(
        Guid campaignId,
        DistributionSearchRequest request,
        CancellationToken ct = default)
    {
        var totalCount = await _distributionRepository.CountByCampaignAsync(campaignId, ct);
        var distributions = await _distributionRepository.SearchByCampaignAsync(campaignId, request, ct);

        // نام گیرندگان برای نمایش (یک پرس‌وجو).
        var employeeIds = distributions.Select(d => d.EmployeeId).Distinct().ToList();
        var employees = employeeIds.Count > 0
            ? (await _employeeRepository.GetByIdsAsync(employeeIds, ct)).ToDictionary(e => e.Id)
            : new Dictionary<Guid, EmployeeEntity>();

        var dtos = distributions.Select(d => new DistributionDto
        {
            Id = d.Id,
            CampaignId = d.CampaignId,
            EmployeeId = d.EmployeeId,
            EmployeeName = employees.TryGetValue(d.EmployeeId, out var e) ? e.FullName : null,
            WorkEmail = d.WorkEmail,
            Status = d.Status,
            SentAt = d.SentAt,
            RespondedAt = d.RespondedAt,
            FailureReason = d.FailureReason,
            ReminderCount = d.ReminderCount
        }).ToList();

        return new PagedResult<DistributionDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200)
        };
    }

    public async Task<Result<DistributionDto>> RecordDistributionResultAsync(
        Guid distributionId,
        bool success,
        string? failureReason,
        CancellationToken ct = default)
    {
        var distribution = await _distributionRepository.GetByIdAsync(distributionId, ct);
        if (distribution is null)
        {
            return Result.Failure<DistributionDto>("distribution_not_found", "ردیف توزیع یافت نشد.");
        }

        if (distribution.Status == DistributionStatus.Responded)
        {
            return Result.Failure<DistributionDto>("distribution_already_responded", "این گیرنده از قبل پاسخ داده است.");
        }

        if (success)
        {
            distribution.MarkSent();
        }
        else
        {
            distribution.MarkFailed(string.IsNullOrWhiteSpace(failureReason) ? "خطای نامشخص کانال" : failureReason.Trim());
        }

        _distributionRepository.Update(distribution);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success(new DistributionDto
        {
            Id = distribution.Id,
            CampaignId = distribution.CampaignId,
            EmployeeId = distribution.EmployeeId,
            WorkEmail = distribution.WorkEmail,
            Status = distribution.Status,
            SentAt = distribution.SentAt,
            RespondedAt = distribution.RespondedAt,
            FailureReason = distribution.FailureReason,
            ReminderCount = distribution.ReminderCount
        });
    }

    public async Task<Result<ReminderProcessResultDto>> ProcessDueRemindersAsync(CancellationToken ct = default)
    {
        var dueCampaigns = await _campaignRepository.GetDueForRemindersAsync(ct);

        var remindersProcessed = 0;
        var recipientsNotified = 0;

        foreach (var campaign in dueCampaigns)
        {
            var now = DateTime.UtcNow;
            var dueReminders = campaign.Reminders
                .Where(r => r.Status == ReminderStatus.Scheduled && r.SendAt <= now)
                .ToList();

            if (dueReminders.Count == 0)
            {
                continue;
            }

            // گیرندگانی که هنوز پاسخ نداده‌اند (در صف یا ارسال‌شده، ردیابی‌شده).
            var remindable = await _distributionRepository.GetRemindableAsync(campaign.Id, ct);

            foreach (var reminder in dueReminders)
            {
                reminder.MarkSent();
                remindersProcessed++;
            }

            foreach (var distribution in remindable)
            {
                distribution.IncrementReminderCount();
                recipientsNotified++;
            }

            _campaignRepository.Update(campaign);
        }

        if (remindersProcessed > 0 || dueCampaigns.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync(ct);
        }

        return Result.Success(new ReminderProcessResultDto
        {
            RemindersProcessed = remindersProcessed,
            RecipientsNotified = recipientsNotified
        });
    }

    public async Task<Result> CancelReminderAsync(Guid campaignId, Guid reminderId, CancellationToken ct = default)
    {
        var campaign = await _campaignRepository.GetByIdAsync(campaignId, ct);
        if (campaign is null)
        {
            return Result.Failure("campaign_not_found", "کمپین یافت نشد.");
        }

        if (!campaign.CancelReminder(reminderId))
        {
            return Result.Failure("reminder_not_cancellable", "یادآور یافت نشد یا از قبل ارسال شده است.");
        }

        _campaignRepository.Update(campaign);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result.Success();
    }

    // --- کمک‌کننده‌ها ----------------------------------------------------------

    /// <summary>بارگذاری نظرسنجی در صورتی که قابل استفاده باشد (بسته/بایگانی نباشد).</summary>
    private async Task<Domain.Modules.Survey.Entities.Survey?> LoadUsableSurveyAsync(Guid surveyId, CancellationToken ct)
    {
        if (surveyId == Guid.Empty)
        {
            return null;
        }

        var survey = await _surveyRepository.GetByIdAsync(surveyId, ct);
        return survey is { Status: not (SurveyStatus.Archived or SurveyStatus.Closed) } ? survey : null;
    }

    /// <summary>
    /// حل جمعیت هدف بر اساس نوع آن، از طریق قرارداد ماژول سازمان.
    /// </summary>
    private async Task<IReadOnlyList<EmployeeEntity>> ResolveAudienceAsync(CampaignEntity campaign, CancellationToken ct)
    {
        var employedOnly = !campaign.IncludeInactiveEmployees;

        return campaign.AudienceType switch
        {
            TargetAudienceType.AllCompany when employedOnly => await _employeeRepository.GetEmployedAsync(ct),
            TargetAudienceType.AllCompany => await _employeeRepository.ListAsync(ct),
            TargetAudienceType.OrgUnits => await _employeeRepository.GetByOrgUnitsAsync(
                campaign.TargetUnits.Select(t => t.OrgUnitId).ToList(),
                campaign.TargetUnits.Any(t => t.IncludeDescendants),
                employedOnly,
                ct),
            TargetAudienceType.Employees => await _employeeRepository.GetByIdsAsync(
                campaign.TargetMembers.Select(m => m.EmployeeId).ToList(), ct),
            _ => []
        };
    }

    private static void ApplyLocalizations(CampaignEntity campaign, IReadOnlyList<CampaignLocalizationDto> localizations)
    {
        foreach (var loc in localizations)
        {
            campaign.SetLocalization(
                loc.Language,
                loc.Title.Trim(),
                string.IsNullOrWhiteSpace(loc.Description) ? null : loc.Description.Trim());
        }

        var requested = localizations.Select(l => l.Language).ToHashSet();
        campaign.Localizations.RemoveAll(l => !requested.Contains(l.Language));
    }

    /// <summary>بازسازی واحدهای هدف و اعضای هدف بر اساس درخواست.</summary>
    private static void ApplyTargets(CampaignEntity campaign, SaveCampaignRequest request)
    {
        var submittedUnits = request.TargetOrgUnitIds
            .Distinct()
            .Select(id => new CampaignTargetUnit
            {
                Id = Guid.CreateVersion7(),
                CampaignId = campaign.Id,
                OrgUnitId = id,
                IncludeDescendants = request.AudienceType == TargetAudienceType.OrgUnits
            })
            .ToList();

        campaign.TargetUnits.Clear();
        campaign.TargetUnits.AddRange(submittedUnits);

        var submittedMembers = request.TargetEmployeeIds
            .Distinct()
            .Select(id => new CampaignTargetMember
            {
                Id = Guid.CreateVersion7(),
                CampaignId = campaign.Id,
                EmployeeId = id
            })
            .ToList();

        campaign.TargetMembers.Clear();
        campaign.TargetMembers.AddRange(submittedMembers);
    }

    /// <summary>بازسازی یادآورها بر اساس درخواست (یادآورهای موجود با شناسه به‌روزرسانی می‌شوند).</summary>
    private static Result ApplyReminders(CampaignEntity campaign, IReadOnlyList<SaveReminderRequest> reminders)
    {
        var submittedIds = new HashSet<Guid>();

        foreach (var request in reminders)
        {
            var reminder = request.Id is { } existingId
                ? campaign.Reminders.FirstOrDefault(r => r.Id == existingId)
                : null;

            var isNew = reminder is null;
            if (isNew)
            {
                reminder = new Reminder
                {
                    Id = request.Id ?? Guid.CreateVersion7(),
                    CampaignId = campaign.Id,
                    SendAt = request.SendAt,
                    Status = ReminderStatus.Scheduled
                };
            }
            else if (reminder!.Status == ReminderStatus.Sent)
            {
                // یادآور ارسال‌شده قابل ویرایش نیست.
                return Result.Failure("reminder_already_sent", "یادآور ارسال‌شده قابل ویرایش نیست.");
            }
            else
            {
                reminder!.SendAt = request.SendAt;
            }

            foreach (var loc in request.Localizations)
            {
                reminder.SetLocalization(
                    loc.Language,
                    loc.Subject.Trim(),
                    string.IsNullOrWhiteSpace(loc.Body) ? null : loc.Body.Trim());
            }

            var requestedLanguages = request.Localizations.Select(l => l.Language).ToHashSet();
            reminder.Localizations.RemoveAll(l => !requestedLanguages.Contains(l.Language));

            if (isNew)
            {
                campaign.Reminders.Add(reminder);
            }

            submittedIds.Add(reminder.Id);
        }

        // یادآورهای ارسال‌نشده‌ای که در درخواست نیستند حذف می‌شوند.
        campaign.Reminders.RemoveAll(r => !submittedIds.Contains(r.Id) && r.Status != ReminderStatus.Sent);

        return Result.Success();
    }

    /// <summary>تبدیل تجمع کمپین به DTO به‌همراه شمارش توزیع‌ها.</summary>
    private async Task<CampaignDto> ToDtoAsync(CampaignEntity campaign, CancellationToken ct)
    {
        var picked = campaign.Localizations.Pick(Language.Fa);

        var totalDistributions = await _distributionRepository.CountByCampaignAsync(campaign.Id, ct);

        var distributionCounts = totalDistributions > 0
            ? Enum.GetValues<DistributionStatus>()
                .Select(async s => new DistributionStatusCountDto
                {
                    Status = s,
                    Count = await _distributionRepository.CountByStatusAsync(campaign.Id, s, ct)
                })
                .Select(t => t.Result)
                .Where(c => c.Count > 0)
                .ToList()
            : [];

        return new CampaignDto
        {
            Id = campaign.Id,
            Code = campaign.Code,
            Status = campaign.Status,
            SurveyId = campaign.SurveyId,
            SurveyCode = campaign.SurveyCode,
            AudienceType = campaign.AudienceType,
            IncludeInactiveEmployees = campaign.IncludeInactiveEmployees,
            Channel = campaign.Channel,
            ScheduledAt = campaign.ScheduledAt,
            EndsAt = campaign.EndsAt,
            StartedAt = campaign.StartedAt,
            CompletedAt = campaign.CompletedAt,
            ArchivedAt = campaign.ArchivedAt,
            Title = picked?.Title ?? campaign.Localizations.FirstOrDefault()?.Title ?? campaign.Code,
            Description = picked?.Description,
            Localizations = campaign.Localizations.Select(l => new CampaignLocalizationDto
            {
                Language = l.Language,
                Title = l.Title,
                Description = l.Description
            }).ToList(),
            TargetUnits = campaign.TargetUnits.Select(t => new CampaignTargetUnitDto
            {
                Id = t.Id,
                OrgUnitId = t.OrgUnitId,
                IncludeDescendants = t.IncludeDescendants
            }).ToList(),
            TargetMembers = campaign.TargetMembers.Select(m => new CampaignTargetMemberDto
            {
                Id = m.Id,
                EmployeeId = m.EmployeeId
            }).ToList(),
            Reminders = campaign.Reminders
                .OrderBy(r => r.SendAt)
                .Select(r => new ReminderDto
                {
                    Id = r.Id,
                    SendAt = r.SendAt,
                    Status = r.Status,
                    SentAt = r.SentAt,
                    Subject = r.Localizations.Pick(Language.Fa)?.Subject
                        ?? r.Localizations.FirstOrDefault()?.Subject
                        ?? string.Empty,
                    Localizations = r.Localizations.Select(l => new ReminderLocalizationDto
                    {
                        Language = l.Language,
                        Subject = l.Subject,
                        Body = l.Body
                    }).ToList()
                }).ToList(),
            IsAudienceConfigured = campaign.IsAudienceConfigured,
            CanLaunch = campaign.CanLaunch,
            TotalDistributions = totalDistributions,
            DistributionCounts = distributionCounts,
            CreatedAt = campaign.CreatedAt,
            UpdatedAt = campaign.UpdatedAt
        };
    }
}
