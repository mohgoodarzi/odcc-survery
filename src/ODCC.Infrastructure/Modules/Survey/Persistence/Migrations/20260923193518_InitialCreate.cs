using System;
using Microsoft.EntityFrameworkCore.Migrations;

// کد تولیدشده‌ی EF Core برای اندیس‌های چندستونیی از آرایه‌های ثابت استفاده می‌کند
// که هشدار CA1861 را فعال می‌کنند. این فایل خودکار تولید شده و قابل ویرایش
// دستی نیست، بنابراین هشدار به‌صورت محلی غیرفعال می‌شود.
#pragma warning disable CA1861

#nullable disable

namespace ODCC.Infrastructure.Modules.Survey.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "survey_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    QuestionnaireId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionnaireCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    AllowEditResponse = table.Column<bool>(type: "bit", nullable: false),
                    ShowProgressBar = table.Column<bool>(type: "bit", nullable: false),
                    SingleResponsePerUser = table.Column<bool>(type: "bit", nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_templates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "surveys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    QuestionnaireId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionnaireVersion = table.Column<int>(type: "int", nullable: false),
                    QuestionnaireCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    AllowEditResponse = table.Column<bool>(type: "bit", nullable: false),
                    ShowProgressBar = table.Column<bool>(type: "bit", nullable: false),
                    SingleResponsePerUser = table.Column<bool>(type: "bit", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EstimatedMinutes = table.Column<int>(type: "int", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ArchivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_surveys", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "survey_template_localizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    TemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_template_localizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_survey_template_localizations_survey_templates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "survey_templates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "survey_localizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    WelcomeMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ThankYouMessage = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    SurveyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_survey_localizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_survey_localizations_surveys_SurveyId",
                        column: x => x.SurveyId,
                        principalTable: "surveys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_survey_localizations_SurveyId_Language",
                table: "survey_localizations",
                columns: new[] { "SurveyId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_template_localizations_TemplateId_Language",
                table: "survey_template_localizations",
                columns: new[] { "TemplateId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_templates_Code",
                table: "survey_templates",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_survey_templates_CreatedAt",
                table: "survey_templates",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_survey_templates_QuestionnaireId",
                table: "survey_templates",
                column: "QuestionnaireId");

            migrationBuilder.CreateIndex(
                name: "IX_surveys_Code",
                table: "surveys",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_surveys_CreatedAt",
                table: "surveys",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_surveys_QuestionnaireId",
                table: "surveys",
                column: "QuestionnaireId");

            migrationBuilder.CreateIndex(
                name: "IX_surveys_Status",
                table: "surveys",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "survey_localizations");

            migrationBuilder.DropTable(
                name: "survey_template_localizations");

            migrationBuilder.DropTable(
                name: "surveys");

            migrationBuilder.DropTable(
                name: "survey_templates");
        }
    }
}
