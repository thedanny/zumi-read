using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorySprout.Api.Migrations
{
    /// <inheritdoc />
    public partial class ParentObservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "QuestionSupportsJson",
                table: "Sessions",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "GoalSkillsJson",
                table: "Profiles",
                type: "text",
                nullable: false,
                defaultValue: "[]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuestionSupportsJson",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "GoalSkillsJson",
                table: "Profiles");
        }
    }
}
