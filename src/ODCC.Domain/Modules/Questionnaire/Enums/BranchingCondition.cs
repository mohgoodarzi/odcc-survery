namespace ODCC.Domain.Modules.Questionnaire.Enums;

/// <summary>
/// شرط انشعاب در یک قانون انشعاب.
/// </summary>
public enum BranchingCondition
{
    /// <summary>پاسخ برابر با مقدار مورد انتظار باشد.</summary>
    Equals = 1,

    /// <summary>پاسخ مخالف مقدار مورد انتظار باشد.</summary>
    NotEquals = 2,

    /// <summary>پاسخ شامل مقدار مورد انتظار باشد (برای چندانتخابی).</summary>
    Contains = 3,

    /// <summary>پاسخ عددی بزرگ‌تر از مقدار مورد انتظار باشد.</summary>
    GreaterThan = 4,

    /// <summary>پاسخ عددی کوچک‌تر از مقدار مورد انتظار باشد.</summary>
    LessThan = 5
}
