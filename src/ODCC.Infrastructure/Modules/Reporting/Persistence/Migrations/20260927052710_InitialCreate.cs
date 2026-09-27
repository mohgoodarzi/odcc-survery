using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ODCC.Infrastructure.Modules.Reporting.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        private static readonly string[] DefinitionStatusNextRunColumns = { "Status", "NextRunAt" };
        private static readonly string[] ExecutionDefinitionQueuedColumns = { "ReportDefinitionId", "QueuedAt" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "report_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    Schedule = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SurveyCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SurveyTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    From = table.Column<DateTime>(type: "datetime2", nullable: true),
                    To = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrgUnitPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IncludeDescendants = table.Column<bool>(type: "bit", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OwnerUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    LastExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetentionCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "report_executions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReportName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReportType = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    QueuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TriggeredBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TriggeredByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    FilePath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    RowCount = table.Column<int>(type: "int", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_report_executions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_report_definitions_CreatedAt",
                table: "report_definitions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_report_definitions_Status",
                table: "report_definitions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_report_definitions_Status_NextRunAt",
                table: "report_definitions",
                columns: DefinitionStatusNextRunColumns);

            migrationBuilder.CreateIndex(
                name: "IX_report_definitions_SurveyId",
                table: "report_definitions",
                column: "SurveyId");

            migrationBuilder.CreateIndex(
                name: "IX_report_executions_CreatedAt",
                table: "report_executions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_report_executions_ReportDefinitionId_QueuedAt",
                table: "report_executions",
                columns: ExecutionDefinitionQueuedColumns);

            migrationBuilder.CreateIndex(
                name: "IX_report_executions_Status",
                table: "report_executions",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "report_definitions");

            migrationBuilder.DropTable(
                name: "report_executions");
        }
    }
}
