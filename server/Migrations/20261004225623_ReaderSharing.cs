using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorySprout.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReaderSharing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReaderShares",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkTokenHash = table.Column<string>(type: "text", nullable: false),
                    CodeHash = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RedeemedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AccessTokenHash = table.Column<string>(type: "text", nullable: true),
                    AccessExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReaderShares", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReaderShares_AccessTokenHash",
                table: "ReaderShares",
                column: "AccessTokenHash",
                unique: true,
                filter: "\"AccessTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ReaderShares_CodeHash",
                table: "ReaderShares",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReaderShares_HouseholdId_SessionId",
                table: "ReaderShares",
                columns: new[] { "HouseholdId", "SessionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReaderShares_LinkTokenHash",
                table: "ReaderShares",
                column: "LinkTokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReaderShares");
        }
    }
}
