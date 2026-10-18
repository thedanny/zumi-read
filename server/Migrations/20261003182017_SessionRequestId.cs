using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorySprout.Api.Migrations
{
    /// <inheritdoc />
    public partial class SessionRequestId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RequestId",
                table: "Sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_HouseholdId_RequestId",
                table: "Sessions",
                columns: new[] { "HouseholdId", "RequestId" },
                unique: true,
                filter: "\"RequestId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_HouseholdId_RequestId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "RequestId",
                table: "Sessions");
        }
    }
}
