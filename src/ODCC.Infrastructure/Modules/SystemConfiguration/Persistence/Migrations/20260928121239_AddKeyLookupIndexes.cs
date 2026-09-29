using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ODCC.Infrastructure.Modules.SystemConfiguration.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKeyLookupIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_system_settings_Key",
                table: "system_settings",
                newName: "IX_system_settings_Key_Lookup");

            migrationBuilder.RenameIndex(
                name: "IX_system_feature_flags_Key",
                table: "system_feature_flags",
                newName: "IX_system_feature_flags_Key_Lookup");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_system_settings_Key_Lookup",
                table: "system_settings",
                newName: "IX_system_settings_Key");

            migrationBuilder.RenameIndex(
                name: "IX_system_feature_flags_Key_Lookup",
                table: "system_feature_flags",
                newName: "IX_system_feature_flags_Key");
        }
    }
}
