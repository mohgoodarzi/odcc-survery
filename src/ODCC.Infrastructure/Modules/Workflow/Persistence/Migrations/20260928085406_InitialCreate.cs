using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ODCC.Infrastructure.Modules.Workflow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
    // ستون‌های اندیس‌های چندستونی به‌صورت فیلدهای static readonly
        private static readonly string[] IndexColumnsInstanceIdStatus = { "InstanceId", "Status" };
        private static readonly string[] IndexColumnsStatusExpiresAt = { "Status", "ExpiresAt" };
        private static readonly string[] IndexColumnsEntityTypeEntityId = { "EntityType", "EntityId" };
        private static readonly string[] IndexColumnsEntityTypeEntityIdStatus = { "EntityType", "EntityId", "Status" };
        private static readonly string[] IndexColumnsWorkflowIdCode = { "WorkflowId", "Code" };
        private static readonly string[] IndexColumnsEntityTypeStatus = { "EntityType", "Status" };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "workflow_approval_requests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstanceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransitionCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FromStateCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ToStateCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApproverPermission = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DecidedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_approval_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_instances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    WorkflowVersion = table.Column<int>(type: "int", nullable: false),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CurrentStateCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StartedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ContextJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    TransitionCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_instances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workflows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    EntityType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_states",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsInitial = table.Column<bool>(type: "bit", nullable: false),
                    IsFinal = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_states", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_states_workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workflow_transitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FromStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToStateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                    ApproverPermission = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_transitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_workflow_transitions_workflow_states_FromStateId",
                        column: x => x.FromStateId,
                        principalTable: "workflow_states",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workflow_transitions_workflow_states_ToStateId",
                        column: x => x.ToStateId,
                        principalTable: "workflow_states",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_workflow_transitions_workflows_WorkflowId",
                        column: x => x.WorkflowId,
                        principalTable: "workflows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_workflow_approval_requests_CreatedAt",
                table: "workflow_approval_requests",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_approval_requests_InstanceId",
                table: "workflow_approval_requests",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_approval_requests_InstanceId_Status",
                table: "workflow_approval_requests",
                columns: IndexColumnsInstanceIdStatus,
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_approval_requests_Status",
                table: "workflow_approval_requests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_approval_requests_Status_ExpiresAt",
                table: "workflow_approval_requests",
                columns: IndexColumnsStatusExpiresAt);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instances_CreatedAt",
                table: "workflow_instances",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instances_EntityType_EntityId",
                table: "workflow_instances",
                columns: IndexColumnsEntityTypeEntityId);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instances_EntityType_EntityId_Status",
                table: "workflow_instances",
                columns: IndexColumnsEntityTypeEntityIdStatus,
                unique: true,
                filter: "[Status] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instances_StartedAt",
                table: "workflow_instances",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instances_Status",
                table: "workflow_instances",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instances_WorkflowId",
                table: "workflow_instances",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_states_CreatedAt",
                table: "workflow_states",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_states_WorkflowId",
                table: "workflow_states",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_states_WorkflowId_Code",
                table: "workflow_states",
                columns: IndexColumnsWorkflowIdCode,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_transitions_CreatedAt",
                table: "workflow_transitions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_transitions_FromStateId",
                table: "workflow_transitions",
                column: "FromStateId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_transitions_ToStateId",
                table: "workflow_transitions",
                column: "ToStateId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_transitions_WorkflowId",
                table: "workflow_transitions",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_workflow_transitions_WorkflowId_Code",
                table: "workflow_transitions",
                columns: IndexColumnsWorkflowIdCode,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflows_Code",
                table: "workflows",
                column: "Code",
                unique: true,
                filter: "[Code] IS NOT NULL AND [Status] <> 2");

            migrationBuilder.CreateIndex(
                name: "IX_workflows_CreatedAt",
                table: "workflows",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_workflows_EntityType_Status",
                table: "workflows",
                columns: IndexColumnsEntityTypeStatus);

            migrationBuilder.CreateIndex(
                name: "IX_workflows_Status",
                table: "workflows",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "workflow_approval_requests");

            migrationBuilder.DropTable(
                name: "workflow_instances");

            migrationBuilder.DropTable(
                name: "workflow_transitions");

            migrationBuilder.DropTable(
                name: "workflow_states");

            migrationBuilder.DropTable(
                name: "workflows");
        }
    }
}
