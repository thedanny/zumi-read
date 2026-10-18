using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorySprout.Api.Migrations
{
    /// <inheritdoc />
    public partial class CharacterGuidance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CharacterGuidance",
                table: "Sessions",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CharacterGuidance",
                table: "Sessions");
        }
    }
}
