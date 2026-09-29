using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ODCC.Application.Authorization;
using ODCC.Application.Modules.Audit.Abstractions;
using ODCC.Application.Modules.Audit.Dtos;
using ODCC.Application.Modules.Integration.Abstractions;
using ODCC.Application.Modules.Integration.Dtos;
using ODCC.Domain.Common;
using ODCC.Domain.Modules.Integration.Entities;
using ODCC.Domain.Modules.Integration.Enums;
using ODCC.Infrastructure.Modules.Integration.Persistence;
using ODCC.Infrastructure.Modules.Integration.Scheduled;
using ODCC.Infrastructure.Modules.Integration.Services;
using Xunit;

namespace ODCC.Infrastructure.Tests.Modules.Integration;

/// <summary>
/// آزمون‌های متممکز (focused) برای بخش‌های پوشش‌داده‌نشده‌ی ماژول یکپارچه‌سازی:
/// آزمون اتصال اندپوینت، جستجوی تحویل‌ها (حریم خصوصی payload)، احراز هویت
/// با ApiKey/Basic، زمان‌بند پس‌زمینه‌ی تحویل و سیاست تأیید صریح آن.
///
/// آزمون‌های اصلی در <see cref="IntegrationServiceTests"/> هستند.
/// </summary>
public class IntegrationServiceFocusedTests
{
    private static readonly string[] ManagePermissions =
        [Permissions.Integrations.Manage, Permissions.Integrations.View, Permissions.Integrations.Retry];

    // --- آزمون اتصال ---------------------------------------------------------------

    [Fact]
    public async Task TestEndpoint_OverRealHttp_ReportsSuccess()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        using var server = new MiniWebhookServer(statusCode: 200);

        var endpointId = (await service.CreateEndpointAsync(NewEndpointRequest(code: "testable", url: server.BaseUrl + "/hooks"))).Value!.Id;

        var result = await service.TestEndpointAsync(endpointId, new TestIntegrationEndpointRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeTrue();
        result.Value.StatusCode.Should().Be(200);
        result.Value.ElapsedMilliseconds.Should().BeGreaterThanOrEqualTo(0);
        result.Value.Error.Should().BeNull();

        (await server.WaitForRequestAsync()).Body.Should().Contain("test_connection");
    }

    [Fact]
    public async Task TestEndpoint_WithFailingServer_ReportsFailure()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        using var server = new MiniWebhookServer(statusCode: 500);

        var endpointId = (await service.CreateEndpointAsync(NewEndpointRequest(code: "testable", url: server.BaseUrl + "/hooks"))).Value!.Id;

        var result = await service.TestEndpointAsync(endpointId, new TestIntegrationEndpointRequest());

        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeFalse();
        result.Value.StatusCode.Should().Be(500);
        result.Value.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TestEndpoint_WithUnreachableUrl_ReportsError()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        // پورتی که هیچ چیزی روی آن گوش نمی‌دهد.
        var endpointId = (await service.CreateEndpointAsync(NewEndpointRequest(code: "unreachable",
            url: "http://127.0.0.1:1/hooks"))).Value!.Id;

        var result = await service.TestEndpointAsync(endpointId, new TestIntegrationEndpointRequest { PayloadJson = """{"a":1}""" });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Success.Should().BeFalse();
        result.Value.StatusCode.Should().BeNull();
        result.Value.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task TestEndpoint_OnUnknownEndpoint_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        var result = await service.TestEndpointAsync(Guid.NewGuid(), new TestIntegrationEndpointRequest());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration_endpoint_not_found");
    }

    [Fact]
    public async Task TestEndpoint_WithApiKey_SendsKeyHeader()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("api-key-secret", "secret-key-value");

        var service = env.Services.GetRequiredService<IIntegrationService>();

        using var server = new MiniWebhookServer(statusCode: 200);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "apikey-out", url: server.BaseUrl + "/hooks") with
        {
            AuthType = IntegrationAuthType.ApiKey,
            SecretRef = "api-key-secret",
            AuthHeaderName = "X-Custom-Key"
        });

        await service.TestEndpointAsync(
            (await env.IntegrationDbContext.IntegrationEndpoints.AsNoTracking().SingleAsync()).Id,
            new TestIntegrationEndpointRequest());

        var captured = await server.WaitForRequestAsync();

        captured.Headers.Should().ContainKey("X-Custom-Key");
        captured.Headers["X-Custom-Key"].Should().Be("secret-key-value");
    }

    [Fact]
    public async Task TestEndpoint_WithBasicAuth_SendsBasicHeader()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("basic-secret", "user:pass");

        var service = env.Services.GetRequiredService<IIntegrationService>();

        using var server = new MiniWebhookServer(statusCode: 200);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "basic-out", url: server.BaseUrl + "/hooks") with
        {
            AuthType = IntegrationAuthType.Basic,
            SecretRef = "basic-secret"
        });

        await service.TestEndpointAsync(
            (await env.IntegrationDbContext.IntegrationEndpoints.AsNoTracking().SingleAsync()).Id,
            new TestIntegrationEndpointRequest());

        var captured = await server.WaitForRequestAsync();

        captured.Headers.Should().ContainKey("Authorization");
        // مقدار راز به‌صورت Base64 ارسال می‌شود، اما هرگز در DTO یا پایگاه داده نیست.
        captured.Headers["Authorization"].Should().StartWith("Basic ");
    }

    // --- جستجوی تحویل‌ها و حریم خصوصی payload ---------------------------------------

    [Fact]
    public async Task SearchDeliveries_List_OmitsPayload_OnlyDetailsIncludeIt()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        using var server = new MiniWebhookServer(statusCode: 200);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1", url: server.BaseUrl + "/hooks"));
        await dispatcher.DispatchAsync(NewDispatchRequest());

        await server.WaitForRequestAsync();

        // فهرست: payload نیست.
        var list = await service.SearchDeliveriesAsync(new WebhookDeliverySearchRequest());
        list.TotalCount.Should().Be(1);
        list.Items.Single().PayloadJson.Should().BeNull();

        // جزئیات: payload هست (با مجوز مشاهده).
        var detail = await service.GetDeliveryByIdAsync(list.Items.Single().Id);
        detail.IsSuccess.Should().BeTrue();
        detail.Value!.PayloadJson.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SearchDeliveries_FiltersByStatusAndEndpoint()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        using var server = new MiniWebhookServer(statusCode: 500);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "flaky", url: server.BaseUrl + "/hooks"));
        await dispatcher.DispatchAsync(NewDispatchRequest());

        await server.WaitForRequestAsync();

        var failed = await service.SearchDeliveriesAsync(new WebhookDeliverySearchRequest
        {
            Status = DeliveryStatus.Pending
        });
        failed.TotalCount.Should().Be(1);

        var other = await service.SearchDeliveriesAsync(new WebhookDeliverySearchRequest
        {
            Status = DeliveryStatus.Succeeded
        });
        other.TotalCount.Should().Be(0);

        var byEndpoint = await service.SearchDeliveriesAsync(new WebhookDeliverySearchRequest
        {
            EndpointId = failed.Items.Single().EndpointId
        });
        byEndpoint.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetDeliveryById_UnknownId_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        var result = await service.GetDeliveryByIdAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("webhook_delivery_not_found");
    }

    // --- اندپوینت: موارد مرزی -------------------------------------------------------

    [Fact]
    public async Task UpdateEndpoint_WithAuthenticatedTypeAndConfiguredSecret_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("hr-secret", "value");

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var endpointId = (await service.CreateEndpointAsync(NewEndpointRequest(code: "updatable"))).Value!.Id;

        var result = await service.UpdateEndpointAsync(endpointId, NewEndpointRequest(code: "updatable") with
        {
            Name = "تغییر یافته",
            AuthType = IntegrationAuthType.HmacSignature,
            SecretRef = "hr-secret"
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.SecretConfigured.Should().BeTrue();

        var row = await env.IntegrationDbContext.IntegrationEndpoints.AsNoTracking().SingleAsync();
        row.Name.Should().Be("تغییر یافته");
        row.SecretRef.Should().Be("hr-secret");
    }

    [Fact]
    public async Task UpdateEndpoint_WithAuthenticatedTypeButMissingSecret_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var endpointId = (await service.CreateEndpointAsync(NewEndpointRequest(code: "updatable"))).Value!.Id;

        var result = await service.UpdateEndpointAsync(endpointId, NewEndpointRequest(code: "updatable") with
        {
            AuthType = IntegrationAuthType.HmacSignature,
            SecretRef = "still-missing"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration_secret_not_configured");
    }

    [Fact]
    public async Task ActivateDeactivate_OnUnknownEndpoint_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        (await service.ActivateEndpointAsync(Guid.NewGuid())).Error.Code.Should().Be("integration_endpoint_not_found");
        (await service.DeactivateEndpointAsync(Guid.NewGuid())).Error.Code.Should().Be("integration_endpoint_not_found");
        (await service.ArchiveEndpointAsync(Guid.NewGuid())).Error.Code.Should().Be("integration_endpoint_not_found");
        (await service.GetEndpointByIdAsync(Guid.NewGuid())).Error.Code.Should().Be("integration_endpoint_not_found");
    }

    [Fact]
    public async Task GetEndpointById_ReturnsArchivedAsWell()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var endpointId = (await service.CreateEndpointAsync(NewEndpointRequest(code: "archivable"))).Value!.Id;

        await service.ArchiveEndpointAsync(endpointId);

        var result = await service.GetEndpointByIdAsync(endpointId);

        // جزئیات در دسترس است (با مجوز)، ولی ویرایش آن رد می‌شود.
        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("archivable");
    }

    [Fact]
    public async Task Endpoints_TrimAndNormalizeInput()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        var result = await service.CreateEndpointAsync(new SaveIntegrationEndpointRequest
        {
            Name = "  اندپوینت آزمون  ",
            Code = "  normalized  ",
            Type = IntegrationType.OutboundWebhook,
            Url = "  https://example.test/hooks  ",
            HttpMethod = "post",
            AuthType = IntegrationAuthType.None
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Code.Should().Be("normalized");
        result.Value.Url.Should().Be("https://example.test/hooks");
        result.Value.HttpMethod.Should().Be("POST");
    }

    [Fact]
    public async Task Endpoints_SubscribedEvents_AreTrimmedAndFiltered()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        var result = await service.CreateEndpointAsync(NewEndpointRequest(code: "events") with
        {
            SubscribedEvents = ["  response.submitted  ", "", "   ", "analytics.computed"]
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.SubscribedEvents.Should().BeEquivalentTo(["response.submitted", "analytics.computed"]);
    }

    // --- زمان‌بند پس‌زمینه (BackgroundJobs) ------------------------------------------

    [Fact]
    public async Task WebhookDeliveryHostedService_WhenDisabled_DoesNothing()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        // پیش‌فرض: EnableDeliveryScheduler = false (سیاست اثر جانبی).
        var options = env.Services.GetRequiredService<IOptions<WebhookDeliveryOptions>>().Value;
        options.EnableDeliveryScheduler.Should().BeFalse();

        var service = env.Services.GetRequiredService<IIntegrationService>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // حتی اگر تحویلِ در انتظارِ سررسیده وجود داشته باشد، زمان‌بند غیرفعال است.
        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1"));
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();
        await dispatcher.DispatchAsync(NewDispatchRequest());

        var delivery = await env.IntegrationDbContext.WebhookDeliveries.SingleAsync();
        delivery.Status = DeliveryStatus.Pending;
        delivery.NextAttemptAt = DateTime.UtcNow.AddMinutes(-5);
        await env.IntegrationDbContext.SaveChangesAsync();

        var hosted = ActivatorUtilities.CreateInstance<WebhookDeliveryHostedService>(env.Services);

        await hosted.StartAsync(cts.Token);

        // زمان‌بند فعال نیست → هیچ تلاشی نباید انجام شود.
        (await env.IntegrationDbContext.WebhookDeliveries.AsNoTracking().SingleAsync()).Status
            .Should().Be(DeliveryStatus.Pending);

        await hosted.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task WebhookDeliveryHostedService_WhenEnabled_RetriesPendingDeliveries()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        // تأیید صریح برای فعال‌شدن زمان‌بند (سیاست پروژه).
        var options = env.Services.GetRequiredService<IOptions<WebhookDeliveryOptions>>().Value;
        options.EnableDeliveryScheduler = true;
        options.PollingIntervalSeconds = 60; // فقط سررسیدشدگی، نه فاصله‌ی کوتاه.

        var service = env.Services.GetRequiredService<IIntegrationService>();

        using var server = new MiniWebhookServer(statusCode: 200);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "scheduled-out", url: server.BaseUrl + "/hooks"));

        // یک تحویلِ در انتظارِ سررسیده‌شده که زمان‌بند باید آن را بگیرد.
        var endpointId = (await env.IntegrationDbContext.IntegrationEndpoints.AsNoTracking().SingleAsync()).Id;

        var pending = new WebhookDelivery
        {
            EndpointId = endpointId,
            EndpointCode = "scheduled-out",
            EventType = "response.submitted",
            EventId = "evt-scheduler",
            PayloadJson = "{}",
            Status = DeliveryStatus.Pending,
            AttemptCount = 1,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-5)
        };
        await env.IntegrationDbContext.WebhookDeliveries.AddAsync(pending);
        await env.IntegrationDbContext.SaveChangesAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var hosted = ActivatorUtilities.CreateInstance<WebhookDeliveryHostedService>(env.Services);

        await hosted.StartAsync(cts.Token);

        // یک چرخه باید تحویل سررسیده را ارسال کند.
        (await server.WaitForRequestAsync()).Should().NotBeNull();

        await hosted.StopAsync(CancellationToken.None);

        var stored = await env.IntegrationDbContext.WebhookDeliveries.AsNoTracking()
            .SingleAsync(d => d.EventId == "evt-scheduler");
        stored.Status.Should().Be(DeliveryStatus.Succeeded);
        stored.ResponseStatusCode.Should().Be(200);
    }

    [Fact]
    public async Task WebhookDeliveryHostedService_WhenCycleFails_KeepsRunning()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var options = env.Services.GetRequiredService<IOptions<WebhookDeliveryOptions>>().Value;
        options.EnableDeliveryScheduler = true;
        options.PollingIntervalSeconds = 60;

        // یک تحویلِ سررسیده با اندپوینتی که آن را پیدا نمی‌کند — چرخه باید شکست
        // بخورد ولی زمان‌بند متوقف نشود.
        var delivery = new WebhookDelivery
        {
            EndpointId = Guid.NewGuid(),
            EndpointCode = "ghost",
            EventType = "response.submitted",
            EventId = "evt-ghost",
            PayloadJson = "{}",
            Status = DeliveryStatus.Pending,
            AttemptCount = 1,
            NextAttemptAt = DateTime.UtcNow.AddMinutes(-5)
        };
        await env.IntegrationDbContext.WebhookDeliveries.AddAsync(delivery);
        await env.IntegrationDbContext.SaveChangesAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        var hosted = ActivatorUtilities.CreateInstance<WebhookDeliveryHostedService>(env.Services);

        var act = async () =>
        {
            await hosted.StartAsync(cts.Token);
            await Task.Delay(500, cts.Token);
        };

        await act.Should().NotThrowAsync();

        await hosted.StopAsync(CancellationToken.None);
    }

    // --- ممیزی تحویل ---------------------------------------------------------------

    [Fact]
    public async Task Dispatch_LogsDeliveryAuditEntries()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        using var server = new MiniWebhookServer(statusCode: 200);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1", url: server.BaseUrl + "/hooks"));
        await dispatcher.DispatchAsync(NewDispatchRequest());
        await server.WaitForRequestAsync();

        var audit = await AuditActionsAsync(env);

        // رویدادهای موفقیت/شکست تحویل ممیزی می‌شوند.
        audit.Should().Contain(a => a.EntityType == "webhook_delivery");
    }

    [Fact]
    public async Task ReceiveInbound_WithAuthenticatedEndpoint_AlwaysRequiresSignatureHeader()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("inbound-bearer-secret", "token-value");

        var service = env.Services.GetRequiredService<IIntegrationService>();

        // اندپوینت با BearerToken برای خروجی؛ ولی مسیر ورودی همیشه با امضای
        // HMAC تأیید می‌شود (یک منبع واحد برای قرار داد امضا).
        await service.CreateEndpointAsync(NewEndpointRequest(code: "inbound-bearer", type: IntegrationType.InboundWebhook) with
        {
            AuthType = IntegrationAuthType.BearerToken,
            SecretRef = "inbound-bearer-secret",
            Url = "/webhooks/inbound-bearer"
        });

        var withoutSignature = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "inbound-bearer",
            PayloadJson = """{"eventId":"evt-1","eventType":"test"}"""
        });

        withoutSignature.IsFailure.Should().BeTrue();
        withoutSignature.Error.Code.Should().Be("integration_signature_missing");

        // راز فقط برای امضا خوانده می‌شود — نوع احراز هویت خروجی روی ورودی تأثیر ندارد.
        var signer = env.Services.GetRequiredService<IWebhookSigner>();
        var payload = """{"eventId":"evt-1","eventType":"test"}""";

        var withSignature = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "inbound-bearer",
            PayloadJson = payload,
            SignatureHeader = signer.Sign("token-value", payload)
        });

        withSignature.IsSuccess.Should().BeTrue();
    }

    // --- کمک‌ها ----------------------------------------------------------------------

    private static SaveIntegrationEndpointRequest NewEndpointRequest(
        string code,
        IntegrationType type = IntegrationType.OutboundWebhook,
        bool activateImmediately = true,
        string? url = null) => new()
        {
            Name = "اندپوینت آزمون",
            Code = code,
            Type = type,
            Url = url ?? (type == IntegrationType.InboundWebhook ? $"/webhooks/{code}" : "https://example.test/hooks"),
            HttpMethod = "POST",
            AuthType = IntegrationAuthType.None,
            SecretRef = null,
            ActivateImmediately = activateImmediately
        };

    private static DispatchWebhookRequest NewDispatchRequest(string eventType = "response.submitted") => new()
    {
        EventType = eventType,
        EventId = $"evt-{Guid.NewGuid():N}",
        Payload = new Dictionary<string, object?>
        {
            ["surveyId"] = Guid.NewGuid(),
            ["isAnonymous"] = true,
            ["answerCount"] = 3
        }
    };

    private static async Task<IReadOnlyList<AuditEntryDto>> AuditActionsAsync(TestEnvironment env)
    {
        var auditService = env.Services.GetRequiredService<IAuditService>();

        return await auditService.SearchAsync(new AuditSearchRequest(
            EntityType: null, Action: null, UserId: null,
            FromUtc: null, ToUtc: null, Page: 1, PageSize: 100));
    }
}
