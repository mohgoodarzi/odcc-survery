using System;
using Microsoft.EntityFrameworkCore.Migrations;

// کد تولیدشده‌ی EF Core برای اندیس‌های چندستونیی از آرایه‌های ثابت استفاده می‌کند
// که هشدار CA1861 را فعال می‌کنند. این فایل خودکار تولید شده و قابل ویرایش
// دستی نیست، بنابراین هشدار به‌صورت محلی غیرفعال می‌شود.
#pragma warning disable CA1861

#nullable disable

namespace ODCC.Infrastructure.Modules.QuestionBank.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    ScaleMax = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    IsArchived = table.Column<bool>(type: "bit", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "question_localizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_localizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_localizations_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_options",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_options", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_options_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_tags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_tags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_tags_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_versions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionNumber = table.Column<int>(type: "int", nullable: false),
                    Snapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ChangeSummary = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_versions_questions_QuestionId",
                        column: x => x.QuestionId,
                        principalTable: "questions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_option_localizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Text = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    QuestionOptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_question_option_localizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_question_option_localizations_question_options_QuestionOptionId",
                        column: x => x.QuestionOptionId,
                        principalTable: "question_options",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_question_localizations_QuestionId_Language",
                table: "question_localizations",
                columns: new[] { "QuestionId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_option_localizations_QuestionOptionId_Language",
                table: "question_option_localizations",
                columns: new[] { "QuestionOptionId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_options_CreatedAt",
                table: "question_options",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_question_options_QuestionId_Code",
                table: "question_options",
                columns: new[] { "QuestionId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_tags_CreatedAt",
                table: "question_tags",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_question_tags_QuestionId_Name",
                table: "question_tags",
                columns: new[] { "QuestionId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_question_versions_CreatedAt",
                table: "question_versions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_question_versions_QuestionId",
                table: "question_versions",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_questions_Code",
                table: "questions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_questions_CreatedAt",
                table: "questions",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_questions_IsArchived",
                table: "questions",
                column: "IsArchived");

            migrationBuilder.CreateIndex(
                name: "IX_questions_Type",
                table: "questions",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "question_localizations");

            migrationBuilder.DropTable(
                name: "question_option_localizations");

            migrationBuilder.DropTable(
                name: "question_tags");

            migrationBuilder.DropTable(
                name: "question_versions");

            migrationBuilder.DropTable(
                name: "question_options");

            migrationBuilder.DropTable(
                name: "questions");
        }
    }
}
