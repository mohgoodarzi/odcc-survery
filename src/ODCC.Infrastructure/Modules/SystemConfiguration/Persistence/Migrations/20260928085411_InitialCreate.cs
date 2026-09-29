using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ODCC.Infrastructure.Modules.SystemConfiguration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
    // ستون‌های اندیس‌های چندستونی به‌صورت فیلدهای static readonly
        private static readonly string[] IndexColumnsOrgUnitIdKey = { "OrgUnitId", "Key" };
        private static readonly string[] IndexColumnsTypeKey = { "Type", "Key" };

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "system_feature_flags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    State = table.Column<int>(type: "int", nullable: false),
                    Percentage = table.Column<int>(type: "int", nullable: false),
                    AllowedUserIds = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    AllowedRoles = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_feature_flags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_policies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_policies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "system_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ValueType = table.Column<int>(type: "int", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DefaultValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Group = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    IsReadOnly = table.Column<bool>(type: "bit", nullable: false),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false),
                    OrgUnitId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModifiedByUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    row_version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_settings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_system_feature_flags_CreatedAt",
                table: "system_feature_flags",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_system_feature_flags_Key",
                table: "system_feature_flags",
                column: "Key",
                unique: true,
                filter: "[OrgUnitId] IS NULL AND [Scope] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_system_feature_flags_OrgUnitId_Key",
                table: "system_feature_flags",
                columns: IndexColumnsOrgUnitIdKey,
                unique: true,
                filter: "[OrgUnitId] IS NOT NULL AND [Scope] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_system_feature_flags_State",
                table: "system_feature_flags",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_system_policies_CreatedAt",
                table: "system_policies",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_system_policies_IsEnabled",
                table: "system_policies",
                column: "IsEnabled");

            migrationBuilder.CreateIndex(
                name: "IX_system_policies_Type",
                table: "system_policies",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_system_policies_Type_Key",
                table: "system_policies",
                columns: IndexColumnsTypeKey,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_CreatedAt",
                table: "system_settings",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_Group",
                table: "system_settings",
                column: "Group");

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_Key",
                table: "system_settings",
                column: "Key",
                unique: true,
                filter: "[OrgUnitId] IS NULL AND [Scope] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_system_settings_OrgUnitId_Key",
                table: "system_settings",
                columns: IndexColumnsOrgUnitIdKey,
                unique: true,
                filter: "[OrgUnitId] IS NOT NULL AND [Scope] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "system_feature_flags");

            migrationBuilder.DropTable(
                name: "system_policies");

            migrationBuilder.DropTable(
                name: "system_settings");
        }
    }
}
