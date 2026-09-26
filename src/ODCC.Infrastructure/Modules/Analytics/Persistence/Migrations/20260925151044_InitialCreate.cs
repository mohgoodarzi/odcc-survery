using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ODCC.Infrastructure.Modules.Analytics.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        private static readonly string[] BenchmarkMetricIndexColumns =
            ["Metric", "IsCompanyWide", "OrgUnitId"];

        private static readonly string[] SurveyMetricUniqueIndexColumns =
            ["SurveyId", "SegmentType", "OrgUnitId", "CampaignId", "Source"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "benchmarks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Metric = table.Column<int>(type: "int", nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    IsCompanyWide = table.Column<bool>(type: "bit", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrgUnitPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_benchmarks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "survey_metrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SurveyTitle = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    SegmentType = table.Column<int>(type: "int", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    OrgUnitPath = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CampaignCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Source = table.Column<int>(type: "int", nullable: true),
                    WindowStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalSessions = table.Column<int>(type: "int", nullable: false),
                    CompletedSessions = table.Column<int>(type: "int", nullable: false),
                    CompletionRate = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    NpsScore = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    NpsPromoters = table.Column<int>(type: "int", nullable: false),
                    NpsPassives = table.Column<int>(type: "int", nullable: false),
                    NpsDetractors = table.Column<int>(type: "int", nullable: false),
                    NpsQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CsatScore = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    CsatRespondents = table.Column<int>(type: "int", nullable: false),
                    CsatQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CesScore = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    CesRespondents = table.Column<int>(type: "int", nullable: false),
                    CesQuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AverageRating = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    RatingRespondents = table.Column<int>(type: "int", nullable: false),
                    ResponseRate = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    TotalDistributions = table.Column<int>(type: "int", nullable: false),
                    RespondedDistributions = table.Column<int>(type: "int", nullable: false),
                    QuestionMetrics = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_metrics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_benchmarks_CreatedAt",
                table: "benchmarks",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_benchmarks_IsActive",
                table: "benchmarks",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_benchmarks_Metric_IsCompanyWide_OrgUnitId",
                table: "benchmarks",
                columns: BenchmarkMetricIndexColumns);

            migrationBuilder.CreateIndex(
                name: "IX_survey_metrics_CreatedAt",
                table: "survey_metrics",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_survey_metrics_SegmentType",
                table: "survey_metrics",
                column: "SegmentType");

            migrationBuilder.CreateIndex(
                name: "IX_survey_metrics_SurveyId",
                table: "survey_metrics",
                column: "SurveyId");

            migrationBuilder.CreateIndex(
                name: "IX_survey_metrics_SurveyId_SegmentType_OrgUnitId_CampaignId_Source",
                table: "survey_metrics",
                columns: SurveyMetricUniqueIndexColumns,
                unique: true,
                filter: "[OrgUnitId] IS NOT NULL AND [CampaignId] IS NOT NULL AND [Source] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "benchmarks");

            migrationBuilder.DropTable(
                name: "survey_metrics");
        }
    }
}
