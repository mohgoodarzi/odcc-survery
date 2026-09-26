using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;
using ODCC.Api;
using ODCC.Application;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Domain;
using ODCC.Domain.Common;
using ODCC.Infrastructure;
using Xunit;

namespace ODCC.Architecture.Tests;

/// <summary>
/// بررسی قواعد لایه‌بندی معماری تمیز.
/// این قواعد در docs/architecture.md مستند شده‌اند و باید همیشه برقرار باشند.
/// </summary>
public class LayeringTests
{
    private static readonly Assembly DomainAssembly = typeof(BaseEntity).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IAuditService).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(OdccInfrastructureMarker).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_Should_Not_Depend_On_Application_Or_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny(ApplicationAssembly.GetName().Name!, InfrastructureAssembly.GetName().Name!, ApiAssembly.GetName().Name!)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"دامنه باید بدون وابستگی به لایه‌های بالاتر باشد. وابستگی‌های یافت‌شده: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_Should_Not_Depend_On_Infrastructure_Or_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(InfrastructureAssembly.GetName().Name!, ApiAssembly.GetName().Name!)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"لایه‌ی کاربری نباید به زیرساخت یا API وابسته باشد. وابستگی‌های یافت‌شده: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Infrastructure_Should_Not_Depend_On_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .Should()
            .NotHaveDependencyOn(ApiAssembly.GetName().Name!)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"زیرساخت نباید به API وابسته باشد. وابستگی‌های یافت‌شده: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    /// <summary>
    /// لایه‌ی کاربری نباید مستقیماً از DbContextهای ماژول‌ها استفاده کند.
    /// این کار مرز ماژول‌ها را می‌شکند (قاعده‌ی docs/modules.md).
    /// </summary>
    [Fact]
    public void Application_Should_Not_Reference_EntityFrameworkCore()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "لایه‌ی کاربری نباید به EF Core وابسته باشد؛ دسترسی داده فقط در زیرساخت است.");
    }

    /// <summary>
    /// لایه‌ی کاربری نباید مستقیماً به موجودیت‌های داخلی ماژول‌های دیگر ارجاع دهد.
    /// قراردادها در Abstractions مجاز هستند. این لیست تمام ماژول‌های پیاده‌سازی‌شده
    /// را پوشش می‌دهد تا اضافه‌شدن ماژول جدید به‌صورت خودکار تحت نظر باشد.
    /// </summary>
    [Fact]
    public void Application_Should_Not_Reference_Infrastructure_Module_Internals()
    {
        var moduleNamespaces = new[]
        {
            "ODCC.Infrastructure.Modules.Audit",
            "ODCC.Infrastructure.Modules.Identity",
            "ODCC.Infrastructure.Modules.Organization",
            "ODCC.Infrastructure.Modules.QuestionBank",
            "ODCC.Infrastructure.Modules.Questionnaire",
            "ODCC.Infrastructure.Modules.Survey",
            "ODCC.Infrastructure.Modules.Campaign",
            "ODCC.Infrastructure.Modules.Response",
            "ODCC.Infrastructure.Modules.Analytics"
        };

        var result = Types.InAssembly(ApplicationAssembly)
            .Should()
            .NotHaveDependencyOnAny(moduleNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "لایه‌ی کاربری نباید به جزئیات داخلی پیاده‌سازی ماژول‌ها در زیرساخت ارجاع دهد.");
    }

    /// <summary>
    /// موجودیت‌های دامنه نباید به ASP.NET Core یا EF Core وابسته باشند.
    /// </summary>
    [Fact]
    public void Domain_Entities_Should_Be_Framework_Agnostic()
    {
        var result = Types.InAssembly(DomainAssembly)
            .Should()
            .NotHaveDependencyOnAny("Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore.Identity")
            .GetResult();

        result.IsSuccessful.Should().BeTrue("موجودیت‌های دامنه باید از فریم‌ورک‌های زیرساختی مستقل باشند.");
    }

    /// <summary>
    /// تمام کنترلرها باید در اسمبلی API باشند (ریشه‌ی ترکیب).
    /// </summary>
    [Fact]
    public void Controllers_Should_Be_In_Api_Layer()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .HaveNameEndingWith("Controller")
            .Should()
            .BeClasses()
            .GetResult();

        result.IsSuccessful.Should().BeTrue();
    }
}

/// <summary>
/// نشانگر نوع برای شناسایی اسمبلی زیرساخت در آزمون‌ها.
/// </summary>
internal static class OdccInfrastructureMarker
{
    public static readonly Type Marker = typeof(ODCC.Infrastructure.DependencyInjection);
}
