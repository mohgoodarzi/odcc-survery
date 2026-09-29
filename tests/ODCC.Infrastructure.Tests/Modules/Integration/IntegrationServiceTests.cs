using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ODCC.Application.Abstractions;
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
/// آزمون‌های ماژول یکپارچه‌سازی: چرخه‌ی عمر اندپوینت‌ها، امنیت رازها (fail-closed)،
/// امضا و تأیید HMAC، دریافت وب‌هوک ورودی (حفاظت در برابر بازپخش و idempotency)،
/// تحویل وب‌هوک خروجی، سیاست تلاش مجدد و ممیزی.
///
/// همه‌ی آزمون‌ها روی SQLite درون‌حافظه‌ای اجرا می‌شوند و هیچ پایگاه‌داده‌ی
/// واقعی یا سرویس بیرونی را لمس نمی‌کنند.
/// </summary>
public class IntegrationServiceTests
{
    private static readonly string[] ManagePermissions =
        [Permissions.Integrations.Manage, Permissions.Integrations.View, Permissions.Integrations.Retry];

    // --- اندپوینت‌ها -----------------------------------------------------------

    [Fact]
    public async Task CreateEndpoint_WithUnauthenticatedType_DoesNotRequireSecret()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        var result = await service.CreateEndpointAsync(NewEndpointRequest(code: "public-hook") with
        {
            AuthType = IntegrationAuthType.None,
            SecretRef = null
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsActive.Should().BeTrue();
        result.Value.SecretConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task CreateEndpoint_WithAuthenticatedTypeButMissingSecret_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        // راز در پیکربندی موجود نیست — باید fail-closed رد شود.
        var result = await service.CreateEndpointAsync(NewEndpointRequest(code: "secured-hook") with
        {
            AuthType = IntegrationAuthType.HmacSignature,
            SecretRef = "missing-secret"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration_secret_not_configured");

        // هیچ ردیفی نباید ساخته شده باشد.
        (await env.IntegrationDbContext.IntegrationEndpoints.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateEndpoint_WithAuthenticatedTypeAndConfiguredSecret_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("hr-sync-secret", "super-secret-value");

        var service = env.Services.GetRequiredService<IIntegrationService>();

        var result = await service.CreateEndpointAsync(NewEndpointRequest(code: "secured-hook") with
        {
            AuthType = IntegrationAuthType.HmacSignature,
            SecretRef = "hr-sync-secret"
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.SecretRef.Should().Be("hr-sync-secret");
        result.Value.SecretConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task CreateEndpoint_DuplicateCode_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        await service.CreateEndpointAsync(NewEndpointRequest(code: "unique-hook"));

        var second = await service.CreateEndpointAsync(NewEndpointRequest(code: "unique-hook"));

        second.IsFailure.Should().BeTrue();
        second.Error.Code.Should().Be("integration_code_exists");
    }

    [Fact]
    public async Task UpdateEndpoint_ArchivedEndpoint_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var endpointId = (await service.CreateEndpointAsync(NewEndpointRequest(code: "to-archive"))).Value!.Id;

        await service.ArchiveEndpointAsync(endpointId);

        var result = await service.UpdateEndpointAsync(endpointId, NewEndpointRequest(code: "to-archive") with { Name = "تغییر یافته" });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration_archived");
    }

    [Fact]
    public async Task Archive_SoftDeletes_And_HidesFromSearch_ByDefault()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var endpointId = (await service.CreateEndpointAsync(NewEndpointRequest(code: "archivable"))).Value!.Id;

        await service.ArchiveEndpointAsync(endpointId);

        var hidden = await service.SearchEndpointsAsync(new IntegrationEndpointSearchRequest());
        hidden.TotalCount.Should().Be(0);

        var visible = await service.SearchEndpointsAsync(new IntegrationEndpointSearchRequest { IncludeArchived = true });
        visible.TotalCount.Should().Be(1);

        // ردیف فیزیکی حذف نشده — فقط پرچم خورده. (فیلتر سراسری حذف نرم
        // باید نادیده گرفته شود تا ردیف بایگانی‌شده دیده شود.)
        var row = await env.IntegrationDbContext.IntegrationEndpoints
            .IgnoreQueryFilters().SingleAsync();
        row.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Deactivate_Then_Activate_TogglesIsActive()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var endpointId = (await service.CreateEndpointAsync(NewEndpointRequest(code: "toggleable"))).Value!.Id;

        await service.DeactivateEndpointAsync(endpointId);
        (await env.IntegrationDbContext.IntegrationEndpoints.AsNoTracking().SingleAsync()).IsActive.Should().BeFalse();

        await service.ActivateEndpointAsync(endpointId);
        (await env.IntegrationDbContext.IntegrationEndpoints.AsNoTracking().SingleAsync()).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateEndpoint_WithNewCodeThatConflicts_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var firstId = (await service.CreateEndpointAsync(NewEndpointRequest(code: "first"))).Value!.Id;
        await service.CreateEndpointAsync(NewEndpointRequest(code: "second"));

        var result = await service.UpdateEndpointAsync(firstId, NewEndpointRequest(code: "second"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration_code_exists");
    }

    [Fact]
    public async Task EndpointDto_NeverExposesSecretValue()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("leak-check-secret", "the-actual-secret");

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var created = await service.CreateEndpointAsync(NewEndpointRequest(code: "leak-check") with
        {
            AuthType = IntegrationAuthType.HmacSignature,
            SecretRef = "leak-check-secret"
        });

        // DTO فقط نام منطقی را برمی‌گرداند — هرگز خود مقدار.
        created.Value!.SecretRef.Should().Be("leak-check-secret");

        var json = System.Text.Json.JsonSerializer.Serialize(created.Value);
        json.Should().NotContain("the-actual-secret");

        // ستون راز در پایگاه داده وجود ندارد — فقط نام منطقی.
        var row = await env.IntegrationDbContext.IntegrationEndpoints.SingleAsync();
        row.SecretRef.Should().Be("leak-check-secret");
    }

    [Fact]
    public async Task SearchEndpoints_FiltersByTypeAndActive()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1", type: IntegrationType.OutboundWebhook));
        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-2", type: IntegrationType.OutboundWebhook,
            activateImmediately: false));
        await service.CreateEndpointAsync(NewEndpointRequest(code: "in-1", type: IntegrationType.InboundWebhook));

        var activeOutbound = await service.SearchEndpointsAsync(new IntegrationEndpointSearchRequest
        {
            Type = IntegrationType.OutboundWebhook,
            IsActive = true
        });

        activeOutbound.TotalCount.Should().Be(1);
        activeOutbound.Items.Single().Code.Should().Be("out-1");
    }

    // --- امضای HMAC -------------------------------------------------------------

    [Fact]
    public async Task Signer_SignThenVerify_RoundTrips()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var signer = env.Services.GetRequiredService<IWebhookSigner>();
        const string secret = "shared-secret";
        const string payload = "{\"eventId\":\"abc\"}";

        var signature = signer.Sign(secret, payload);

        signature.Should().StartWith("sha256=");
        signer.Verify(secret, payload, signature).Should().BeTrue();
    }

    [Fact]
    public async Task Signer_Verify_WithWrongSecret_ReturnsFalse()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var signer = env.Services.GetRequiredService<IWebhookSigner>();
        const string payload = "{\"eventId\":\"abc\"}";

        var signature = signer.Sign("real-secret", payload);

        signer.Verify("other-secret", payload, signature).Should().BeFalse();
    }

    [Fact]
    public async Task Signer_Verify_WithTamperedPayload_ReturnsFalse()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var signer = env.Services.GetRequiredService<IWebhookSigner>();
        const string secret = "shared-secret";

        var signature = signer.Sign(secret, "{\"eventId\":\"abc\"}");

        signer.Verify(secret, "{\"eventId\":\"evil\"}", signature).Should().BeFalse();
    }

    [Fact]
    public async Task Signer_Verify_AcceptsBareHexHeader()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var signer = env.Services.GetRequiredService<IWebhookSigner>();
        const string secret = "shared-secret";
        const string payload = "{\"eventId\":\"abc\"}";

        var signature = signer.Sign(secret, payload);
        var bareHex = signature["sha256=".Length..];

        signer.Verify(secret, payload, bareHex).Should().BeTrue();
    }

    [Fact]
    public async Task Signer_Verify_WithEmptyInputs_ReturnsFalse()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var signer = env.Services.GetRequiredService<IWebhookSigner>();

        signer.Verify(string.Empty, "payload", "sha256=abc").Should().BeFalse();
        signer.Verify("secret", "payload", string.Empty).Should().BeFalse();
        signer.Verify("secret", "payload", " ").Should().BeFalse();
    }

    [Fact]
    public async Task Signer_Sign_WithEmptySecret_Throws()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var signer = env.Services.GetRequiredService<IWebhookSigner>();

        var act = () => signer.Sign(string.Empty, "payload");

        act.Should().Throw<ArgumentException>();
    }

    // --- حل‌کننده‌ی راز -----------------------------------------------------------

    [Fact]
    public async Task SecretResolver_UnconfiguredSecret_ReturnsNull_FailClosed()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var resolver = env.Services.GetRequiredService<ISecretResolver>();

        resolver.Resolve("never-configured").Should().BeNull();
        resolver.IsConfigured("never-configured").Should().BeFalse();
    }

    [Fact]
    public async Task SecretResolver_EmptyRef_ReturnsNull()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var resolver = env.Services.GetRequiredService<ISecretResolver>();

        resolver.Resolve(null).Should().BeNull();
        resolver.Resolve(string.Empty).Should().BeNull();
        resolver.Resolve("   ").Should().BeNull();
    }

    [Fact]
    public async Task SecretResolver_ConfiguredSecret_ReturnsValue()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetIntegrationsSecret("resolve-me", "resolved-value");

        var resolver = env.Services.GetRequiredService<ISecretResolver>();

        resolver.Resolve("resolve-me").Should().Be("resolved-value");
        resolver.IsConfigured("resolve-me").Should().BeTrue();
    }

    // --- وب‌هوک ورودی -----------------------------------------------------------

    [Fact]
    public async Task ReceiveInbound_WithUnauthenticatedEndpoint_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "inbound-open", type: IntegrationType.InboundWebhook) with
        {
            AuthType = IntegrationAuthType.None,
            SecretRef = null,
            Url = "/webhooks/inbound-open"
        });

        var result = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "inbound-open",
            PayloadJson = """{"eventId":"evt-1","eventType":"hr.employee.created","type":"test"}""",
            SourceIp = "10.0.0.1"
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DeliveryStatus.Succeeded);
        result.Value.ResponseStatusCode.Should().Be(200);
        result.Value.EventType.Should().Be("hr.employee.created");
    }

    [Fact]
    public async Task ReceiveInbound_WithAuthenticatedEndpointButMissingSignature_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("inbound-secret", "shared-inbound");

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "inbound-secured", type: IntegrationType.InboundWebhook) with
        {
            AuthType = IntegrationAuthType.HmacSignature,
            SecretRef = "inbound-secret",
            Url = "/webhooks/inbound-secured"
        });

        var result = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "inbound-secured",
            PayloadJson = """{"eventId":"evt-1"}""",
            SignatureHeader = null
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration_signature_missing");
    }

    [Fact]
    public async Task ReceiveInbound_WithInvalidSignature_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("inbound-secret", "shared-inbound");

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "inbound-secured", type: IntegrationType.InboundWebhook) with
        {
            AuthType = IntegrationAuthType.HmacSignature,
            SecretRef = "inbound-secret",
            Url = "/webhooks/inbound-secured"
        });

        var result = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "inbound-secured",
            PayloadJson = """{"eventId":"evt-1"}""",
            SignatureHeader = "sha256=deadbeef"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration_signature_invalid");

        // هیچ تحویلی ثبت نشده باشد.
        (await env.IntegrationDbContext.WebhookDeliveries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ReceiveInbound_WithValidSignature_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("inbound-secret", "shared-inbound");

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "inbound-secured", type: IntegrationType.InboundWebhook) with
        {
            AuthType = IntegrationAuthType.HmacSignature,
            SecretRef = "inbound-secret",
            Url = "/webhooks/inbound-secured"
        });

        var payload = """{"eventId":"evt-1","eventType":"hr.sync"}""";
        var signer = env.Services.GetRequiredService<IWebhookSigner>();

        var result = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "inbound-secured",
            PayloadJson = payload,
            SignatureHeader = signer.Sign("shared-inbound", payload)
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DeliveryStatus.Succeeded);
    }

    [Fact]
    public async Task ReceiveInbound_WithStaleTimestamp_Fails_ReplayProtection()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("inbound-secret", "shared-inbound");
        env.SetIntegrationOptions(options => options.InboundTimestampToleranceSeconds = 300);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "inbound-secured", type: IntegrationType.InboundWebhook) with
        {
            AuthType = IntegrationAuthType.HmacSignature,
            SecretRef = "inbound-secret",
            Url = "/webhooks/inbound-secured"
        });

        var payload = """{"eventId":"evt-1"}""";
        var signer = env.Services.GetRequiredService<IWebhookSigner>();
        var stale = DateTime.UtcNow.AddHours(-1).ToString("O");

        var result = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "inbound-secured",
            PayloadJson = payload,
            SignatureHeader = signer.Sign("shared-inbound", payload),
            TimestampHeader = stale
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration_timestamp_expired");
    }

    [Fact]
    public async Task ReceiveInbound_WithFreshTimestamp_Succeeds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("inbound-secret", "shared-inbound");
        env.SetIntegrationOptions(options => options.InboundTimestampToleranceSeconds = 300);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "inbound-secured", type: IntegrationType.InboundWebhook) with
        {
            AuthType = IntegrationAuthType.HmacSignature,
            SecretRef = "inbound-secret",
            Url = "/webhooks/inbound-secured"
        });

        var payload = """{"eventId":"evt-1"}""";
        var signer = env.Services.GetRequiredService<IWebhookSigner>();

        var result = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "inbound-secured",
            PayloadJson = payload,
            SignatureHeader = signer.Sign("shared-inbound", payload),
            TimestampHeader = DateTime.UtcNow.ToString("O")
        });

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ReceiveInbound_SameEventIdTwice_IsIdempotent()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "inbound-open", type: IntegrationType.InboundWebhook) with
        {
            AuthType = IntegrationAuthType.None,
            Url = "/webhooks/inbound-open"
        });

        var payload = """{"eventId":"evt-unique","eventType":"test"}""";

        var first = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "inbound-open",
            PayloadJson = payload
        });

        var second = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "inbound-open",
            PayloadJson = payload
        });

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Value!.Id.Should().Be(first.Value!.Id);

        // فقط یک ردیف — بازپخشِ فرستنده نباید ردیف مضاعف بسازد.
        (await env.IntegrationDbContext.WebhookDeliveries.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ReceiveInbound_OnNonInboundEndpoint_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "outbound-only"));

        var result = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "outbound-only",
            PayloadJson = """{"eventId":"evt"}"""
        });

        result.IsFailure.Should().BeTrue();
        // پیام خطا عمداً مبهم است تا وجود/نوع اندپوینت فاش نشود.
        result.Error.Code.Should().Be("integration_endpoint_not_found");
    }

    [Fact]
    public async Task ReceiveInbound_OnUnknownCode_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();

        var result = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "does-not-exist",
            PayloadJson = """{"eventId":"evt"}"""
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration_endpoint_not_found");
    }

    [Fact]
    public async Task ReceiveInbound_AuthenticatedWithoutConfiguredSecret_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        // ساخت یک اندپوینت احراز هویت‌شده با رازِ پیکربندی‌نشده از طریق سرویس
        // رد می‌شود، پس مستقیماً در پایگاه داده وارد می‌کنیم.
        await env.IntegrationDbContext.IntegrationEndpoints.AddAsync(new IntegrationEndpoint
        {
            Name = "بدون راز",
            Code = "no-secret",
            Type = IntegrationType.InboundWebhook,
            Url = "/webhooks/no-secret",
            AuthType = IntegrationAuthType.HmacSignature,
            SecretRef = "unconfigured",
            IsActive = true
        });
        await env.IntegrationDbContext.SaveChangesAsync();

        var result = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "no-secret",
            PayloadJson = """{"eventId":"evt"}""",
            SignatureHeader = "sha256=abc"
        });

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration_secret_not_configured");
    }

    [Fact]
    public async Task ReceiveInbound_GeneratesEventId_WhenPayloadLacksOne()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "inbound-open", type: IntegrationType.InboundWebhook) with
        {
            AuthType = IntegrationAuthType.None,
            Url = "/webhooks/inbound-open"
        });

        var result = await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "inbound-open",
            PayloadJson = """{"eventType":"test"}"""
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.EventId.Should().NotBeNullOrEmpty();
    }

    // --- تحویل خروجی و تلاش مجدد -------------------------------------------------

    [Fact]
    public async Task Dispatch_DeliversToActiveOutboundEndpoints_OverRealHttp()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        using var server = new MiniWebhookServer(statusCode: 200);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1", url: server.BaseUrl + "/hooks"));
        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-2", url: server.BaseUrl + "/hooks"));

        // غیرفعال — نباید چیزی دریافت کند.
        var inactiveId = (await service.CreateEndpointAsync(
            NewEndpointRequest(code: "out-3", url: server.BaseUrl + "/hooks", activateImmediately: false))).Value!.Id;
        await service.DeactivateEndpointAsync(inactiveId);

        await dispatcher.DispatchAsync(NewDispatchRequest());

        // فقط دو اندپوینت فعال باید پیام دریافت کرده باشند.
        (await server.WaitForRequestAsync()).Should().NotBeNull();
        (await server.WaitForRequestAsync()).Should().NotBeNull();

        var deliveries = await env.IntegrationDbContext.WebhookDeliveries.ToListAsync();
        deliveries.Should().HaveCount(2);
        deliveries.All(d => d.Status == DeliveryStatus.Succeeded).Should().BeTrue();
        deliveries.All(d => d.ResponseStatusCode == 200).Should().BeTrue();
    }

    [Fact]
    public async Task Dispatch_SendsHmacSignatureHeader()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("outbound-secret", "shared-outbound");

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();
        var signer = env.Services.GetRequiredService<IWebhookSigner>();

        using var server = new MiniWebhookServer(statusCode: 200);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "signed-out", url: server.BaseUrl + "/hooks") with
        {
            AuthType = IntegrationAuthType.HmacSignature,
            SecretRef = "outbound-secret"
        });

        var request = NewDispatchRequest();
        await dispatcher.DispatchAsync(request);

        var captured = await server.WaitForRequestAsync();

        // امضای HMAC باید در هدر ارسال شده باشد.
        captured.Headers.Should().ContainKey("X-ODCC-Signature");
        var signature = captured.Headers["X-ODCC-Signature"];

        signer.Verify("shared-outbound", captured.Body, signature).Should().BeTrue();
    }

    [Fact]
    public async Task Dispatch_WithBearerToken_SendsAuthorizationHeader()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);
        env.SetIntegrationsSecret("bearer-secret", "token-value-123");

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        using var server = new MiniWebhookServer(statusCode: 200);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "bearer-out", url: server.BaseUrl + "/hooks") with
        {
            AuthType = IntegrationAuthType.BearerToken,
            SecretRef = "bearer-secret"
        });

        await dispatcher.DispatchAsync(NewDispatchRequest());

        var captured = await server.WaitForRequestAsync();

        captured.Headers.Should().ContainKey("Authorization");
        captured.Headers["Authorization"].Should().Be("Bearer token-value-123");
    }

    [Fact]
    public async Task Dispatch_PayloadContainsOnlyAggregateMetadata_NoRespondentIds()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        using var server = new MiniWebhookServer(statusCode: 200);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "privacy-out", url: server.BaseUrl + "/hooks"));

        // یک رویداد شبیه‌سازی‌شده‌ی ارسال پاسخ — فقط متادیتای عمومی.
        var request = new DispatchWebhookRequest
        {
            EventType = "response.submitted",
            EventId = "evt-privacy",
            Payload = new Dictionary<string, object?>
            {
                ["surveyId"] = Guid.NewGuid(),
                ["surveyCode"] = "SURVEY-1",
                ["isAnonymous"] = true,
                ["answerCount"] = 5
            }
        };

        await dispatcher.DispatchAsync(request);

        var captured = await server.WaitForRequestAsync();
        var json = captured.Body;

        // حریم خصوصی: payload فقط تجمعی است — هیچ شناسه‌ی پاسخ‌گویی نیست.
        json.Should().Contain("surveyId");
        json.Should().NotContain("respondent");
        json.Should().NotContain("userId");
        json.Should().NotContain("employeeId");
    }

    [Fact]
    public async Task Dispatch_WithPermanentClientError_MarksFailedImmediately()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        using var server = new MiniWebhookServer(statusCode: 400);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "bad-endpoint", url: server.BaseUrl + "/hooks"));

        await dispatcher.DispatchAsync(NewDispatchRequest());

        await server.WaitForRequestAsync();

        var delivery = await env.IntegrationDbContext.WebhookDeliveries.SingleAsync();
        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.ResponseStatusCode.Should().Be(400);
        delivery.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task Dispatch_WithTransientServerError_SchedulesRetry()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        using var server = new MiniWebhookServer(statusCode: 500);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "flaky-endpoint", url: server.BaseUrl + "/hooks"));

        await dispatcher.DispatchAsync(NewDispatchRequest());

        await server.WaitForRequestAsync();

        var delivery = await env.IntegrationDbContext.WebhookDeliveries.SingleAsync();
        delivery.Status.Should().Be(DeliveryStatus.Pending);
        delivery.NextAttemptAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Dispatch_WithSubscribedEvents_FiltersCorrectly()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        await service.CreateEndpointAsync(NewEndpointRequest(code: "subscribed") with
        {
            SubscribedEvents = ["response.submitted"]
        });
        await service.CreateEndpointAsync(NewEndpointRequest(code: "unsubscribed") with
        {
            SubscribedEvents = ["analytics.computed"]
        });

        await dispatcher.DispatchAsync(NewDispatchRequest(eventType: "response.submitted"));

        var deliveries = await env.IntegrationDbContext.WebhookDeliveries.ToListAsync();
        deliveries.Should().ContainSingle(d => d.EndpointCode == "subscribed");
    }

    [Fact]
    public async Task Dispatch_SameEventIdTwice_IsIdempotent()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1"));

        var request = NewDispatchRequest();
        await dispatcher.DispatchAsync(request);
        await dispatcher.DispatchAsync(request);

        var deliveries = await env.IntegrationDbContext.WebhookDeliveries.ToListAsync();
        deliveries.Should().ContainSingle();
    }

    [Fact]
    public async Task Dispatch_WhenNoEndpoints_DoesNothing()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        var act = async () => await dispatcher.DispatchAsync(NewDispatchRequest());

        await act.Should().NotThrowAsync();
        (await env.IntegrationDbContext.WebhookDeliveries.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RetryDelivery_OnSucceeded_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        using var server = new MiniWebhookServer(statusCode: 200);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1", url: server.BaseUrl + "/hooks"));
        await dispatcher.DispatchAsync(NewDispatchRequest());
        await server.WaitForRequestAsync();

        var delivery = await env.IntegrationDbContext.WebhookDeliveries.SingleAsync();

        var result = await service.RetryDeliveryAsync(delivery.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("webhook_delivery_succeeded");
    }

    [Fact]
    public async Task RetryDelivery_OnPendingWithActiveEndpoint_AttemptsAgain()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        // ابتدا یک سرور خطادار که تحویل را می‌شکند، سپس سرور سالم برای تلاش مجدد.
        using var failingServer = new MiniWebhookServer(statusCode: 500);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1", url: failingServer.BaseUrl + "/hooks"));
        await dispatcher.DispatchAsync(NewDispatchRequest());
        await failingServer.WaitForRequestAsync();

        var delivery = await env.IntegrationDbContext.WebhookDeliveries.SingleAsync();
        delivery.Status.Should().Be(DeliveryStatus.Pending);

        var result = await service.RetryDeliveryAsync(delivery.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(DeliveryStatus.Pending);
        result.Value.AttemptCount.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task RetryDelivery_WhenEndpointInactive_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var endpointId = (await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1"))).Value!.Id;

        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();
        await dispatcher.DispatchAsync(NewDispatchRequest());

        await service.DeactivateEndpointAsync(endpointId);

        var delivery = await env.IntegrationDbContext.WebhookDeliveries.SingleAsync();
        delivery.Status = DeliveryStatus.Failed;
        delivery.AttemptCount = 1;
        delivery.NextAttemptAt = DateTime.UtcNow;
        await env.IntegrationDbContext.SaveChangesAsync();

        var result = await service.RetryDeliveryAsync(delivery.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("integration_endpoint_not_active");
    }

    [Fact]
    public async Task RetryDelivery_OnPermanentFailure_Fails()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1"));

        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();
        await dispatcher.DispatchAsync(NewDispatchRequest());

        var delivery = await env.IntegrationDbContext.WebhookDeliveries.SingleAsync();
        delivery.Status = DeliveryStatus.Failed;
        delivery.AttemptCount = 3;
        delivery.NextAttemptAt = null;
        await env.IntegrationDbContext.SaveChangesAsync();

        var result = await service.RetryDeliveryAsync(delivery.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("webhook_delivery_not_retryable");
    }

    [Fact]
    public async Task ProcessPendingDeliveries_RetriesDuePending()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        using var server = new MiniWebhookServer(statusCode: 200);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1", url: server.BaseUrl + "/hooks"));
        await dispatcher.DispatchAsync(NewDispatchRequest());

        var delivery = await env.IntegrationDbContext.WebhookDeliveries.SingleAsync();
        delivery.Status = DeliveryStatus.Pending;
        delivery.AttemptCount = 1;
        delivery.NextAttemptAt = DateTime.UtcNow.AddMinutes(-5); // سررسیده‌شده
        await env.IntegrationDbContext.SaveChangesAsync();

        var processed = await service.ProcessPendingDeliveriesAsync(DateTime.UtcNow);

        processed.Should().Be(1);

        await server.WaitForRequestAsync();

        var stored = await env.IntegrationDbContext.WebhookDeliveries.SingleAsync();
        stored.Status.Should().Be(DeliveryStatus.Succeeded);
    }

    [Fact]
    public async Task ProcessPendingDeliveries_SkipsNotYetDue()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1"));

        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();
        await dispatcher.DispatchAsync(NewDispatchRequest());

        var delivery = await env.IntegrationDbContext.WebhookDeliveries.SingleAsync();
        delivery.Status = DeliveryStatus.Pending;
        delivery.AttemptCount = 1;
        delivery.NextAttemptAt = DateTime.UtcNow.AddMinutes(10); // هنوز نرسیده
        await env.IntegrationDbContext.SaveChangesAsync();

        var processed = await service.ProcessPendingDeliveriesAsync(DateTime.UtcNow);

        processed.Should().Be(0);
    }

    [Fact]
    public async Task WebhookDelivery_FourHxx_IsPermanentFailure()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var delivery = new WebhookDelivery { EndpointId = Guid.NewGuid(), EventId = "e1", EventType = "t" };

        // ۴xx غیرقابل‌تلاش‌مجدد → شکست دائمی فوری.
        delivery.RecordFailure(400, "Bad Request", maxRetries: 3);

        delivery.Status.Should().Be(DeliveryStatus.Failed);
        delivery.NextAttemptAt.Should().BeNull();
        delivery.CanRetry(3).Should().BeFalse();
    }

    [Fact]
    public async Task WebhookDelivery_FourTwoNine_IsRetryable()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var delivery = new WebhookDelivery { EndpointId = Guid.NewGuid(), EventId = "e1", EventType = "t" };

        // ۴۲۹ (درخواست‌های زیاد) قابل‌تلاش‌مجدد است — نباید تحویل را خاتمه دهد.
        delivery.RecordFailure(429, "Too Many Requests", maxRetries: 3);

        delivery.Status.Should().Be(DeliveryStatus.Pending);
        delivery.NextAttemptAt.Should().NotBeNull();
        delivery.CanRetry(3).Should().BeTrue();
    }

    [Fact]
    public async Task WebhookDelivery_FiveHxx_SchedulesExponentialRetry()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var delivery = new WebhookDelivery { EndpointId = Guid.NewGuid(), EventId = "e1", EventType = "t" };

        delivery.RecordFailure(500, "Internal Server Error", maxRetries: 3);
        delivery.RecordFailure(500, "Internal Server Error", maxRetries: 3);

        delivery.Status.Should().Be(DeliveryStatus.Pending);
        delivery.AttemptCount.Should().Be(2);
        // تأخیر نمایی: ۲^۲ = ۴ دقیقه (حداکثر ۳۰).
        delivery.NextAttemptAt.Should().NotBeNull();
        delivery.NextAttemptAt!.Should().BeAfter(DateTime.UtcNow.AddMinutes(3));

        // تلاش سوم → اتمام سقف.
        delivery.RecordFailure(500, "Internal Server Error", maxRetries: 3);
        delivery.Status.Should().Be(DeliveryStatus.Failed);
    }

    [Fact]
    public async Task WebhookDelivery_Success_MarksSucceeded()
    {
        await using var env = await TestEnvironment.CreateAsync();

        var delivery = new WebhookDelivery { EndpointId = Guid.NewGuid(), EventId = "e1", EventType = "t" };

        delivery.RecordSuccess(200);

        delivery.Status.Should().Be(DeliveryStatus.Succeeded);
        delivery.DeliveredAt.Should().NotBeNull();
        delivery.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task AttemptDelivery_WithUnconfiguredSecret_FailsClosed()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1"));

        var endpoint = await env.IntegrationDbContext.IntegrationEndpoints.SingleAsync();
        endpoint.AuthType = IntegrationAuthType.HmacSignature;
        endpoint.SecretRef = "now-removed";
        await env.IntegrationDbContext.SaveChangesAsync();

        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();
        var delivery = new WebhookDelivery
        {
            EndpointId = endpoint.Id,
            EndpointCode = endpoint.Code,
            EventType = "test",
            EventId = "evt-fail",
            PayloadJson = "{}",
            Status = DeliveryStatus.Pending
        };
        await env.IntegrationDbContext.WebhookDeliveries.AddAsync(delivery);
        await env.IntegrationDbContext.SaveChangesAsync();

        using var httpClient = new HttpClient();
        await dispatcher.AttemptDeliveryAsync(httpClient, delivery, endpoint);

        // fail-closed: هرگز بدون احراز هویت ارسال نمی‌شود.
        delivery.Status.Should().NotBe(DeliveryStatus.Succeeded);
        delivery.LastError.Should().NotBeNullOrEmpty();
        delivery.LastError.Should().Contain("راز");
    }

    // --- آمار و ممیزی -----------------------------------------------------------

    [Fact]
    public async Task GetStats_ReflectsState()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var dispatcher = env.Services.GetRequiredService<IWebhookDispatcher>();

        using var server = new MiniWebhookServer(statusCode: 200);

        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-1", url: server.BaseUrl + "/hooks"));
        await service.CreateEndpointAsync(NewEndpointRequest(code: "out-2", activateImmediately: false));

        await dispatcher.DispatchAsync(NewDispatchRequest());
        await server.WaitForRequestAsync();

        var stats = await service.GetStatsAsync();

        stats.IsSuccess.Should().BeTrue();
        stats.Value!.TotalEndpoints.Should().Be(2);
        stats.Value.ActiveEndpoints.Should().Be(1);
        stats.Value.SuccessfulDeliveries.Should().Be(1);
    }

    [Fact]
    public async Task EndpointLifecycle_LogsAuditEntries()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        var endpointId = (await service.CreateEndpointAsync(NewEndpointRequest(code: "audited"))).Value!.Id;

        await service.ActivateEndpointAsync(endpointId);
        await service.ArchiveEndpointAsync(endpointId);

        var auditService = env.Services.GetRequiredService<IAuditService>();
        var entries = await auditService.SearchAsync(new AuditSearchRequest(
            EntityType: null, Action: null, UserId: null,
            FromUtc: null, ToUtc: null, Page: 1, PageSize: 100));

        entries.Should().Contain(e => e.Action == "create" && e.EntityType == "integration_endpoint");
        entries.Should().Contain(e => e.Action == "activate" && e.EntityType == "integration_endpoint");
        entries.Should().Contain(e => e.Action == "archive" && e.EntityType == "integration_endpoint");
    }

    [Fact]
    public async Task ReceiveInbound_LogsAuditEntry_WithSourceIp()
    {
        await using var env = await TestEnvironment.CreateAsync();
        env.SetCurrentUser(orgUnitId: null, DataScope.Company, permissions: ManagePermissions);

        var service = env.Services.GetRequiredService<IIntegrationService>();
        await service.CreateEndpointAsync(NewEndpointRequest(code: "inbound-open", type: IntegrationType.InboundWebhook) with
        {
            AuthType = IntegrationAuthType.None,
            Url = "/webhooks/inbound-open"
        });

        await service.ReceiveInboundWebhookAsync(new ReceiveInboundWebhookRequest
        {
            EndpointCode = "inbound-open",
            PayloadJson = """{"eventId":"evt","eventType":"test"}""",
            SourceIp = "192.168.1.55"
        });

        var auditService = env.Services.GetRequiredService<IAuditService>();
        var entries = await auditService.SearchAsync(new AuditSearchRequest(
            EntityType: null, Action: null, UserId: null,
            FromUtc: null, ToUtc: null, Page: 1, PageSize: 100));

        entries.Should().Contain(e => e.Action == "webhook_received" && e.EntityType == "webhook_inbound");
    }

    // --- کمک‌ها ------------------------------------------------------------------

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
}
