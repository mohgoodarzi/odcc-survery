namespace ODCC.Domain.Common;

/// <summary>
/// نتیجه‌ی عملیات بدون استثنا.
/// خطاهای مورد انتظار (قوانین کسب‌وکار، اعتبارسنجی) از طریق <see cref="Result{T}"/> منتقل می‌شوند
/// تا کنترلرها بتوانند کد وضعیت HTTP مناسب را انتخاب کنند.
/// فقط خطاهای غیرمنتظره به‌صورت استثنا بالا می‌آیند و توسط میان‌افزار سراسری مدیریت می‌شوند.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public AppError Error { get; }

    protected Result(bool isSuccess, AppError error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>نتیجه‌ی موفق بدون مقدار.</summary>
    public static Result Success() => new(isSuccess: true, AppError.None);

    /// <summary>نتیجه‌ی ناموفق بدون مقدار.</summary>
    public static Result Failure(AppError error) => new(isSuccess: false, error);

    /// <summary>نتیجه‌ی ناموفق بدون مقدار.</summary>
    public static Result Failure(string code, string message) => Failure(new AppError(code, message));

    /// <summary>نتیجه‌ی ناموفق دارای مقدار (مقدار <c>null</c>). نوع باید صریحاً داده شود.</summary>
    public static Result<T> Failure<T>(AppError error) => new(isSuccess: false, default, error);

    /// <summary>نتیجه‌ی ناموفق دارای مقدار (مقدار <c>null</c>). نوع باید صریحاً داده شود.</summary>
    public static Result<T> Failure<T>(string code, string message) => Failure<T>(new AppError(code, message));

    /// <summary>نتیجه‌ی موفق دارای مقدار. نوع <typeparamref name="T"/> از آرگومان استنتاج می‌شود.</summary>
    public static Result<T> Success<T>(T value) => new(isSuccess: true, value, AppError.None);
}

/// <summary>
/// نتیجه‌ی عملیات که در صورت موفقیت مقداری از نوع <typeparamref name="T"/> حمل می‌کند.
/// کارخانه‌های ساخت روی نوع غیرجنریک <see cref="Result"/> قرار دارند تا قاعده‌ی
/// «عضو استاتیک روی نوع جنریک» (CA1000) نقض نشود.
/// </summary>
public sealed class Result<T> : Result
{
    public T? Value { get; }

    internal Result(bool isSuccess, T? value, AppError error) : base(isSuccess, error)
    {
        Value = value;
    }

    /// <summary>
    /// مقدار در صورت موفقیت؛ در صورت ناموفق بودن <see cref="Result"/> استثنا پرتاب می‌کند.
    /// این متد فقط بعد از بررسی <see cref="Result.IsFailure"/> در کنترلرها استفاده می‌شود
    /// تا کامپایلر بداند مقدار null نیست.
    /// </summary>
    public T GetValueOrThrow() =>
        IsSuccess ? Value! : throw new InvalidOperationException($"Cannot access value of a failed result: {Error.Code}");

    /// <summary>تبدیل مقدار در صورت موفقیت؛ در غیر این صورت خطا منتشر می‌شود.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper) =>
        IsSuccess && Value is not null
            ? Result.Success(mapper(Value))
            : Result.Failure<TOut>(Error);
}

/// <summary>
/// خطای مستقل از زبان. کد خطا قرارداد API است و کلاینت آن را به پیام فارسی تبدیل می‌کند.
/// </summary>
public readonly record struct AppError(string Code, string Message)
{
    public static readonly AppError None = new(string.Empty, string.Empty);

    /// <summary>ترکیب چند خطا در یک خطای واحد.</summary>
    public static AppError Aggregate(IReadOnlyCollection<AppError> errors) =>
        new("validation_failed", string.Join("; ", errors.Select(e => e.Code)));
}
