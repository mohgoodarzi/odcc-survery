using System;
using Microsoft.EntityFrameworkCore.Migrations;

// کد تولیدشده‌ی EF Core برای اندیس‌های چندستونیی از آرایه‌های ثابت استفاده می‌کند
// که هشدار CA1861 را فعال می‌کنند. این فایل خودکار تولید شده و قابل ویرایش
// دستی نیست، بنابراین هشدار به‌صورت محلی غیرفعال می‌شود.
#pragma warning disable CA1861

#nullable disable

namespace ODCC.Infrastructure.Modules.Questionnaire.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "questionnaires",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questionnaires", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "questionnaire_localizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    QuestionnaireId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questionnaire_localizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_questionnaire_localizations_questionnaires_QuestionnaireId",
                        column: x => x.QuestionnaireId,
                        principalTable: "questionnaires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "questionnaire_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionnaireId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsOptional = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questionnaire_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_questionnaire_sections_questionnaires_QuestionnaireId",
                        column: x => x.QuestionnaireId,
                        principalTable: "questionnaires",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "questionnaire_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionVersionNumber = table.Column<int>(type: "int", nullable: false),
                    QuestionCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    QuestionType = table.Column<int>(type: "int", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    TitleOverride = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questionnaire_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_questionnaire_items_questionnaire_sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "questionnaire_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "questionnaire_section_localizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Language = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questionnaire_section_localizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_questionnaire_section_localizations_questionnaire_sections_SectionId",
                        column: x => x.SectionId,
                        principalTable: "questionnaire_sections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "questionnaire_branching_rules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TargetItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Condition = table.Column<int>(type: "int", nullable: false),
                    ExpectedValue = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_questionnaire_branching_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_questionnaire_branching_rules_questionnaire_items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "questionnaire_items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_branching_rules_CreatedAt",
                table: "questionnaire_branching_rules",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_branching_rules_ItemId",
                table: "questionnaire_branching_rules",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_branching_rules_TargetItemId",
                table: "questionnaire_branching_rules",
                column: "TargetItemId");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_items_CreatedAt",
                table: "questionnaire_items",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_items_QuestionId",
                table: "questionnaire_items",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_items_SectionId_DisplayOrder",
                table: "questionnaire_items",
                columns: new[] { "SectionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_localizations_QuestionnaireId_Language",
                table: "questionnaire_localizations",
                columns: new[] { "QuestionnaireId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_section_localizations_SectionId_Language",
                table: "questionnaire_section_localizations",
                columns: new[] { "SectionId", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_sections_CreatedAt",
                table: "questionnaire_sections",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaire_sections_QuestionnaireId",
                table: "questionnaire_sections",
                column: "QuestionnaireId");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaires_Code",
                table: "questionnaires",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_questionnaires_CreatedAt",
                table: "questionnaires",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_questionnaires_Status",
                table: "questionnaires",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "questionnaire_branching_rules");

            migrationBuilder.DropTable(
                name: "questionnaire_localizations");

            migrationBuilder.DropTable(
                name: "questionnaire_section_localizations");

            migrationBuilder.DropTable(
                name: "questionnaire_items");

            migrationBuilder.DropTable(
                name: "questionnaire_sections");

            migrationBuilder.DropTable(
                name: "questionnaires");
        }
    }
}
