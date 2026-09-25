using Microsoft.EntityFrameworkCore;
using ODCC.Application.Abstractions;
using ODCC.Infrastructure.Modules.Identity.Persistence;
using ODCC.Infrastructure.Modules.Organization.Persistence;
using ODCC.Infrastructure.Modules.QuestionBank.Persistence;
using ODCC.Infrastructure.Modules.Questionnaire.Persistence;
using ODCC.Infrastructure.Modules.Survey.Persistence;
using ODCC.Infrastructure.Modules.Campaign.Persistence;
using ODCC.Infrastructure.Modules.Response.Persistence;
using ODCC.Infrastructure.Persistence.Common;

namespace ODCC.Infrastructure.Persistence;

/// <summary>
/// پیاده‌سازی <see cref="IUnitOfWork"/> برای یک DbContext مشخص.
/// هر ماژول نمونه‌ی خود را با context خود ثبت می‌کند تا مرز تراکنش ماژول حفظ شود.
/// </summary>
public class UnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
{
    private readonly TContext _context;
    private readonly IDomainEventDispatcher _domainEventDispatcher;

    public UnitOfWork(TContext context, IDomainEventDispatcher domainEventDispatcher)
    {
        _context = context;
        _domainEventDispatcher = domainEventDispatcher;

        // جبران‌سازی رفتار پیش‌فرض EF Core برای موجودیت‌های جدید.
        //
        // <b>چرا:</b> <see cref="BaseEntity"/> کلید هر موجودیت را هم‌زمان با ساخت
        // (<c>Guid.CreateVersion7()</c>) مقداردهی می‌کند. وقتی موجودیتی جدید به یک
        // aggregate ردیابی‌شده اضافه می‌شود و سپس DetectChanges اجرا می‌شود، EF Core
        // به‌دلیل «کلید غیرپیش‌فرض» آن موجودیت را به‌جای <c>Added</c> به‌صورت
        // <c>Modified</c> در نظر می‌گیرد (یعنی فرض می‌کند ردیفی از پیش در پایگاه
        // داده وجود دارد). نتیجه صدور دستور UPDATE برای ردیفی است که هرگز INSERT
        // نشده و در پایان <c>DbUpdateConcurrencyException</c> (۰ ردیف تحت تأثیر) است.
        //
        // رویداد <c>Tracked</c> با <c>FromQuery == false</c> فقط برای موجودیت‌های
        // کشف‌شده توسط ردیاب تغییر (نه موجودیت‌های بارگذاری‌شده از پایگاه داده) اجرا
        // می‌شود. در این معماری همواره الگوی «بارگذاری → تغییر → ذخیره» برقرار است
        // و Attach/Update موجودیتِ جدا شده وجود ندارد، پس این تصحیح فقط روی
        // موجودیت‌های واقعاً جدید اعمال می‌شود. رفتار در SQLite و SQL Server یکسان است.
        context.ChangeTracker.Tracked += static (_, args) =>
        {
            if (!args.FromQuery && args.Entry.State == EntityState.Modified)
            {
                args.Entry.State = EntityState.Added;
            }
        };
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // مهر زمان به‌روزرسانی روی موجودیت‌های تغییر یافته.
        foreach (var entry in _context.ChangeTracker.Entries<Domain.Common.BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        // در SQLite پایگاه داده قادر به تولید rowversion نیست؛ بدون این مرحله
        // توکن همزمانی تهی می‌ماند و هر به‌روزرسانی بعدی شکست می‌خورد.
        // در SQL Server این متد بی‌اثر است (مقدار نهایی توسط پایگاه داده تولید می‌شود).
        RowVersionInitializer.EnsureRowVersions(_context);

        var result = await _context.SaveChangesAsync(ct);

        // پس از commit موفق، رویدادهای دامنه تحویل داده می‌شوند. اگر تحویل
        // قبل از commit انجام می‌شد، شنونده‌ای که دیتابیس می‌خواند داده‌ی
        // هنوز ذخیره‌نشده می‌دید. ترتیب: اول commit، بعد انتشار.
        await DispatchDomainEventsAsync(ct);

        return result;
    }

    /// <summary>
    /// تحویل رویدادهای دامنه‌ی موجودیت‌های ردیابی‌شده و سپس پاکسازی آن‌ها.
    /// خطای یک شنونده مانع پاکسازی و ادامه‌ی کار نمی‌شود (خطا لاگ می‌شود).
    /// </summary>
    private async Task DispatchDomainEventsAsync(CancellationToken ct)
    {
        var entities = _context.ChangeTracker.Entries<Domain.Common.BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        if (entities.Count == 0)
        {
            return;
        }

        foreach (var entity in entities)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                await _domainEventDispatcher.DispatchAsync(domainEvent, ct);
            }

            entity.ClearDomainEvents();
        }
    }
}

/// <summary>
/// مرز تراکنشی ماژول هویت.
/// </summary>
public sealed class IdentityUnitOfWork(IdentityDbContext context, IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<IdentityDbContext>(context, domainEventDispatcher), IIdentityUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول سازمان.
/// </summary>
public sealed class OrganizationUnitOfWork(OrganizationDbContext context, IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<OrganizationDbContext>(context, domainEventDispatcher), IOrganizationUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول کتابخانه‌ی سؤالات.
/// </summary>
public sealed class QuestionBankUnitOfWork(QuestionBankDbContext context, IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<QuestionBankDbContext>(context, domainEventDispatcher), IQuestionBankUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول پرسشنامه‌ها.
/// </summary>
public sealed class QuestionnaireUnitOfWork(QuestionnaireDbContext context, IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<QuestionnaireDbContext>(context, domainEventDispatcher), IQuestionnaireUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول نظرسنجی‌ها.
/// </summary>
public sealed class SurveyUnitOfWork(SurveyDbContext context, IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<SurveyDbContext>(context, domainEventDispatcher), ISurveyUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول کمپین‌ها.
/// </summary>
public sealed class CampaignUnitOfWork(CampaignDbContext context, IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<CampaignDbContext>(context, domainEventDispatcher), ICampaignUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول پاسخ‌ها.
/// </summary>
public sealed class ResponseUnitOfWork(ResponseDbContext context, IDomainEventDispatcher domainEventDispatcher)
    : UnitOfWork<ResponseDbContext>(context, domainEventDispatcher), IResponseUnitOfWork;
