using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ODCC.Infrastructure.Modules.Integration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
    // ستون‌های اندیس‌های چندستونی به‌صورت فیلدهای static readonly
        private static readonly string[] IndexColumnsTypeIsActive = { "Type", "IsActive" };
        private static readonly string[] IndexColumnsEndpointIdEventId = { "EndpointId", "EventId" };
        private static readonly string[] IndexColumnsStatusNextAttemptAt = { "Status", "NextAttemptAt" };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "integration_endpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Url = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    HttpMethod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    AuthType = table.Column<int>(type: "int", nullable: false),
                    SecretRef = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    AuthHeaderName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    MaxRetries = table.Column<int>(type: "int", nullable: false),
                    SubscribedEvents = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SuccessfulDeliveries = table.Column<int>(type: "int", nullable: false),
                    LastDeliveryError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LastDeliveryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_endpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndpointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EndpointCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EventId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", maxLength: 1048576, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    LastAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_webhook_deliveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_integration_endpoints_Code",
                table: "integration_endpoints",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_integration_endpoints_CreatedAt",
                table: "integration_endpoints",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_integration_endpoints_IsActive",
                table: "integration_endpoints",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_integration_endpoints_Type_IsActive",
                table: "integration_endpoints",
                columns: IndexColumnsTypeIsActive);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_CreatedAt",
                table: "webhook_deliveries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_EndpointId",
                table: "webhook_deliveries",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_EndpointId_EventId",
                table: "webhook_deliveries",
                columns: IndexColumnsEndpointIdEventId,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_Status",
                table: "webhook_deliveries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_webhook_deliveries_Status_NextAttemptAt",
                table: "webhook_deliveries",
                columns: IndexColumnsStatusNextAttemptAt);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "integration_endpoints");

            migrationBuilder.DropTable(
                name: "webhook_deliveries");
        }
    }
}
