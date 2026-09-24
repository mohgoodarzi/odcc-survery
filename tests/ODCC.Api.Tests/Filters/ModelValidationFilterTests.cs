using FluentAssertions;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ODCC.Api.Filters;
using ODCC.Application.Modules.Campaign.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Campaign.Enums;
using Xunit;

namespace ODCC.Api.Tests.Filters;

/// <summary>
/// آزمون‌های فیلتر اعتبارسنجی مدل.
///
/// این فیلتر مرز اعتبارسنجی سرور را فعال می‌کند: اعتبارسنج‌های
/// FluentValidation که در لایه‌ی کاربرد تعریف و در DI ثبت شده‌اند،
/// پیش از اجرای اکشن روی پارامترهای [FromBody] اجرا می‌شوند.
/// </summary>
public class ModelValidationFilterTests
{
    private static readonly ILogger<ModelValidationFilter> Logger =
        LoggerFactory.Create(builder => { }).CreateLogger<ModelValidationFilter>();

    /// <summary>ساخت یک ActionExecutingContext با آرگومان‌های داده‌شده.</summary>
    private static (ActionExecutingContext context, ActionExecutionDelegate next) CreateContext(
        ServiceProvider services, params (string Name, object? Value)[] arguments)
    {
        var actionArguments = arguments.ToDictionary(arg => arg.Name, arg => arg.Value);
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var filters = new List<IFilterMetadata>();
        var controller = new object();

        var context = new ActionExecutingContext(actionContext, filters, actionArguments!, controller)
        {
            HttpContext = { RequestServices = services }
        };

        var executed = new ActionExecutedContext(actionContext, filters, controller);
        ActionExecutionDelegate next = () => Task.FromResult(executed);

        return (context, next);
    }

    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddValidatorsFromAssemblyContaining<SaveCampaignRequestValidator>();
        return services.BuildServiceProvider();
    }

    private static SaveCampaignRequest ValidRequest() => new()
    {
        Code = "CMP-VALID-01",
        SurveyId = Guid.CreateVersion7(),
        AudienceType = TargetAudienceType.AllCompany,
        Channel = DistributionChannel.Email,
        Localizations = [new CampaignLocalizationDto { Language = Language.Fa, Title = "کمپین نمونه" }]
    };

    [Fact]
    public async Task Rejects_Invalid_Model_With_ValidationProblemDetails()
    {
        var services = BuildServices();
        var filter = new ModelValidationFilter(services, Logger);
        var invalid = ValidRequest() with { Code = string.Empty };

        var (context, next) = CreateContext(services, ("request", invalid));
        var nextCalled = false;

        await filter.OnActionExecutionAsync(context, () =>
        {
            nextCalled = true;
            return next();
        });

        nextCalled.Should().BeFalse("درخواست نامعتبر نباید به اکشن برسد");
        context.Result.Should().BeOfType<BadRequestObjectResult>();

        var problem = ((BadRequestObjectResult)context.Result!).Value.Should().BeOfType<ValidationProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Extensions.Should().ContainKey("code").WhoseValue.Should().Be("validation_failed");
        problem.Errors.Should().ContainKey("code");
    }

    [Fact]
    public async Task Converts_Property_Names_To_CamelCase()
    {
        var services = BuildServices();
        var filter = new ModelValidationFilter(services, Logger);
        var invalid = ValidRequest() with { Code = "کد نامعتبر با فاصله" };

        var (context, next) = CreateContext(services, ("request", invalid));

        await filter.OnActionExecutionAsync(context, next);

        var result = (BadRequestObjectResult)context.Result!;
        var problem = (ValidationProblemDetails)result.Value!;
        problem.Errors.Keys.Should().Contain("code");
        problem.Errors["code"][0].Should().Contain("کد کمپین");
    }

    [Fact]
    public async Task Lets_Valid_Model_Through()
    {
        var services = BuildServices();
        var filter = new ModelValidationFilter(services, Logger);

        var (context, next) = CreateContext(services, ("request", ValidRequest()));

        await filter.OnActionExecutionAsync(context, next);

        context.Result.Should().BeNull("درخواست معتبر باید به اکشن برسد");
    }

    [Fact]
    public async Task Ignores_Arguments_Without_Validator()
    {
        var services = BuildServices();
        var filter = new ModelValidationFilter(services, Logger);

        // Guid یک پارامتر مسیر است و اعتبارسنجی ندارد.
        var (context, next) = CreateContext(services, ("id", Guid.CreateVersion7()));

        await filter.OnActionExecutionAsync(context, next);

        context.Result.Should().BeNull();
    }
}
