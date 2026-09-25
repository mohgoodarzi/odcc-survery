using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ODCC.Infrastructure.Modules.Response.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        // آرایه‌های ثابت برای جلوگیری از CA1861 (ساخت آرایه در هر فراخوانی).
        private static readonly string[] AnswerSessionItemColumns = { "SessionId", "QuestionnaireItemId" };
        private static readonly string[] SessionSurveyRespondentStatusColumns = { "SurveyId", "RespondentUserId", "Status" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "response_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SurveyCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CampaignCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RespondentUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondentEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RespondentDisplayName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    ResponseLanguage = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AnswerCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_response_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "response_answers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionnaireItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    QuestionType = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    TextValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NumericValue = table.Column<decimal>(type: "decimal(18,3)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_response_answers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_response_answers_response_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "response_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "response_answer_selections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AnswerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OptionCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_response_answer_selections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_response_answer_selections_response_answers_AnswerId",
                        column: x => x.AnswerId,
                        principalTable: "response_answers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_response_answer_selections_AnswerId",
                table: "response_answer_selections",
                column: "AnswerId");

            migrationBuilder.CreateIndex(
                name: "IX_response_answer_selections_CreatedAt",
                table: "response_answer_selections",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_response_answer_selections_OptionId",
                table: "response_answer_selections",
                column: "OptionId");

            migrationBuilder.CreateIndex(
                name: "IX_response_answers_CreatedAt",
                table: "response_answers",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_response_answers_SessionId_QuestionnaireItemId",
                table: "response_answers",
                columns: AnswerSessionItemColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_response_sessions_CreatedAt",
                table: "response_sessions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_response_sessions_Status",
                table: "response_sessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_response_sessions_SurveyId",
                table: "response_sessions",
                column: "SurveyId");

            migrationBuilder.CreateIndex(
                name: "IX_response_sessions_SurveyId_RespondentUserId_Status",
                table: "response_sessions",
                columns: SessionSurveyRespondentStatusColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "response_answer_selections");

            migrationBuilder.DropTable(
                name: "response_answers");

            migrationBuilder.DropTable(
                name: "response_sessions");
        }
    }
}
