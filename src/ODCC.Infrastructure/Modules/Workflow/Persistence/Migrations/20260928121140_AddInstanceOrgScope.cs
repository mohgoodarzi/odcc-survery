using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ODCC.Infrastructure.Modules.Workflow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInstanceOrgScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OrgUnitId",
                table: "workflow_instances",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OrgUnitPath",
                table: "workflow_instances",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_workflow_instances_OrgUnitPath",
                table: "workflow_instances",
                column: "OrgUnitPath");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_workflow_instances_OrgUnitPath",
                table: "workflow_instances");

            migrationBuilder.DropColumn(
                name: "OrgUnitId",
                table: "workflow_instances");

            migrationBuilder.DropColumn(
                name: "OrgUnitPath",
                table: "workflow_instances");
        }
    }
}
