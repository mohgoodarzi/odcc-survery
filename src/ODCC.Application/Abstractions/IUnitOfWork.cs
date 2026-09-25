using System.Linq.Expressions;
using ODCC.Domain.Common;

namespace ODCC.Application.Abstractions;

/// <summary>
/// واحد کار: یک مرز تراکنشی واحد برای چند عملیات نوشتن.
/// <see cref="SaveChangesAsync"/> تمام تغییرات DbContext جاری را در یک تراکنش ذخیره می‌کند
/// و همزمان رویدادهای دامنه‌ی منتشرنشده را هم قبل از commit تحویل می‌دهد.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>
/// مرز تراکنشی ماژول هویت. فقط این ماژول باید آن را تزریق بگیرد.
/// </summary>
public interface IIdentityUnitOfWork : IUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول سازمان. فقط این ماژول باید آن را تزریق بگیرد.
/// </summary>
public interface IOrganizationUnitOfWork : IUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول کتابخانه‌ی سؤالات. فقط این ماژول باید آن را تزریق بگیرد.
/// </summary>
public interface IQuestionBankUnitOfWork : IUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول پرسشنامه‌ها. فقط این ماژول باید آن را تزریق بگیرد.
/// </summary>
public interface IQuestionnaireUnitOfWork : IUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول نظرسنجی‌ها. فقط این ماژول باید آن را تزریق بگیرد.
/// </summary>
public interface ISurveyUnitOfWork : IUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول کمپین‌ها. فقط این ماژول باید آن را تزریق بگیرد.
/// </summary>
public interface ICampaignUnitOfWork : IUnitOfWork;

/// <summary>
/// مرز تراکنشی ماژول پاسخ‌ها. فقط این ماژول باید آن را تزریق بگیرد.
/// </summary>
public interface IResponseUnitOfWork : IUnitOfWork;

/// <summary>
/// مشخصه‌ی پرس‌وجوی قابل‌استفاده مجدد: فیلتر، مرتب‌سازی و بارگذاری ناوبری.
/// </summary>
public interface ISpecification<T> where T : BaseEntity
{
    Expression<Func<T, bool>>? Criteria { get; }
    Expression<Func<T, object>>[] OrderBy { get; }
    int? Take { get; }
    int? Skip { get; }
}
