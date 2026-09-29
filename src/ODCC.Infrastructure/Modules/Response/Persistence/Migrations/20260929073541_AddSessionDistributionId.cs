using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ODCC.Infrastructure.Modules.Response.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionDistributionId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DistributionId",
                table: "response_sessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_response_sessions_DistributionId",
                table: "response_sessions",
                column: "DistributionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_response_sessions_DistributionId",
                table: "response_sessions");

            migrationBuilder.DropColumn(
                name: "DistributionId",
                table: "response_sessions");
        }
    }
}
