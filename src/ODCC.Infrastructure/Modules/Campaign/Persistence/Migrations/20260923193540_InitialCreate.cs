using System;
using Microsoft.EntityFrameworkCore.Migrations;

// کد تولیدشده‌ی EF Core برای اندیس‌های چندستونیی از آرایه‌های ثابت استفاده می‌کند
// که هشدار CA1861 را فعال می‌کنند. این فایل خودکار تولید شده و قابل ویرایش
// دستی نیست، بنابراین هشدار به‌صورت محلی غیرفعال می‌شود.
#pragma warning disable CA1861

#nullable disable

namespace ODCC.Infrastructure.Modules.Campaign.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "campaign_distributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FailureReason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ReminderCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_distributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    AudienceType = table.Column<int>(type: "int", nullable: false),
                    IncludeInactiveEmployees = table.Column<bool>(type: "bit", nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    ScheduledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "campaign_localizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_localizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaign_localizations_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_reminders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SendAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_reminders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaign_reminders_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_target_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_target_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaign_target_members_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_target_units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IncludeDescendants = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_target_units", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaign_target_units_campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_reminder_localizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ReminderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_reminder_localizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_campaign_reminder_localizations_campaign_reminders_ReminderId",
                        column: x => x.ReminderId,
                        principalTable: "campaign_reminders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_distributions_CampaignId",
                table: "campaign_distributions",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_distributions_CampaignId_EmployeeId",
                table: "campaign_distributions",
                columns: new[] { "CampaignId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaign_distributions_CampaignId_Status",
                table: "campaign_distributions",
                columns: new[] { "CampaignId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_campaign_distributions_CreatedAt",
                table: "campaign_distributions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_localizations_CampaignId_Language",
                table: "campaign_localizations",
                columns: new[] { "CampaignId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaign_reminder_localizations_ReminderId_Language",
                table: "campaign_reminder_localizations",
                columns: new[] { "ReminderId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaign_reminders_CampaignId",
                table: "campaign_reminders",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_reminders_CreatedAt",
                table: "campaign_reminders",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_target_members_CampaignId_EmployeeId",
                table: "campaign_target_members",
                columns: new[] { "CampaignId", "EmployeeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaign_target_members_CreatedAt",
                table: "campaign_target_members",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_target_units_CampaignId_OrgUnitId",
                table: "campaign_target_units",
                columns: new[] { "CampaignId", "OrgUnitId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaign_target_units_CreatedAt",
                table: "campaign_target_units",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_Code",
                table: "campaigns",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_CreatedAt",
                table: "campaigns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_Status",
                table: "campaigns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_SurveyId",
                table: "campaigns",
                column: "SurveyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaign_distributions");

            migrationBuilder.DropTable(
                name: "campaign_localizations");

            migrationBuilder.DropTable(
                name: "campaign_reminder_localizations");

            migrationBuilder.DropTable(
                name: "campaign_target_members");

            migrationBuilder.DropTable(
                name: "campaign_target_units");

            migrationBuilder.DropTable(
                name: "campaign_reminders");

            migrationBuilder.DropTable(
                name: "campaigns");
        }
    }
}
