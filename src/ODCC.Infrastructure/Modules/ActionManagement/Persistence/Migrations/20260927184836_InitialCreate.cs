using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ODCC.Infrastructure.Modules.ActionManagement.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
    // ستون‌های اندیس‌های چندستونی به‌صورت فیلدهای static readonly تعریف
    // می‌شوند تا تحلیل‌گر CA1861 (آرایه‌ی ثابت به‌عنوان آرگومان) فعال نشود.
    private static readonly string[] CommentItemIndexColumns = { "ActionItemId", "CreatedAt" };
    private static readonly string[] ItemAssigneeIndexColumns = { "AssigneeUserId", "Status" };
    private static readonly string[] ItemDueDateIndexColumns = { "Status", "DueDate" };
    private static readonly string[] ItemRemindAtIndexColumns = { "Status", "RemindAt" };
    private static readonly string[] PlanOrgUnitIndexColumns = { "OrgUnitId", "Status" };
    private static readonly string[] PlanSurveyIndexColumns = { "SurveyId", "Status" };

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.CreateTable(
                name: "action_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SourceKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SurveyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SurveyCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    SurveyTitle = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TriggerMetricType = table.Column<int>(type: "int", nullable: true),
                    TriggerMetricValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    OutcomeMetricValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    OutcomeMeasuredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrgUnitPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "action_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AssigneeUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AssigneeUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RemindAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    EscalatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Effectiveness = table.Column<int>(type: "int", nullable: false),
                    EffectivenessNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EffectivenessAssessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectivenessAssessedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_action_items_action_plans_ActionPlanId",
                        column: x => x.ActionPlanId,
                        principalTable: "action_plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "action_comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AuthorUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_comments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_action_comments_action_items_ActionItemId",
                        column: x => x.ActionItemId,
                        principalTable: "action_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "action_evidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UploadedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_action_evidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_action_evidence_action_items_ActionItemId",
                        column: x => x.ActionItemId,
                        principalTable: "action_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_action_comments_ActionItemId_CreatedAt",
                table: "action_comments",
                columns: CommentItemIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_action_comments_CreatedAt",
                table: "action_comments",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_action_evidence_ActionItemId",
                table: "action_evidence",
                column: "ActionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_action_evidence_CreatedAt",
                table: "action_evidence",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_action_items_ActionPlanId",
                table: "action_items",
                column: "ActionPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_action_items_AssigneeUserId_Status",
                table: "action_items",
                columns: ItemAssigneeIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_action_items_CreatedAt",
                table: "action_items",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_action_items_Status_DueDate",
                table: "action_items",
                columns: ItemDueDateIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_action_items_Status_RemindAt",
                table: "action_items",
                columns: ItemRemindAtIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_action_plans_CreatedAt",
                table: "action_plans",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_action_plans_OrgUnitId_Status",
                table: "action_plans",
                columns: PlanOrgUnitIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_action_plans_SourceKey",
                table: "action_plans",
                column: "SourceKey",
                unique: true,
                filter: "[SourceKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_action_plans_Status",
                table: "action_plans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_action_plans_SurveyId_Status",
                table: "action_plans",
                columns: PlanSurveyIndexColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "action_comments");

            migrationBuilder.DropTable(
                name: "action_evidence");

            migrationBuilder.DropTable(
                name: "action_items");

            migrationBuilder.DropTable(
                name: "action_plans");
        }
    }
}
