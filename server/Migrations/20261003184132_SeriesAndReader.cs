using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorySprout.Api.Migrations
{
    /// <inheritdoc />
    public partial class SeriesAndReader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EpisodeNumber",
                table: "Sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LockStoryDuringQuestions",
                table: "Sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "QuestionModeStarted",
                table: "Sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "QuestionsCompleted",
                table: "Sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SeriesContext",
                table: "Sessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                table: "Sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowChildQuestions",
                table: "Sessions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Premise = table.Column<string>(type: "text", nullable: false),
                    CharacterNotes = table.Column<string>(type: "text", nullable: false),
                    Recap = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Series", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_SeriesId_EpisodeNumber",
                table: "Sessions",
                columns: new[] { "SeriesId", "EpisodeNumber" },
                unique: true,
                filter: "\"SeriesId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Series_HouseholdId_ChildProfileId",
                table: "Series",
                columns: new[] { "HouseholdId", "ChildProfileId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Series");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_SeriesId_EpisodeNumber",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "EpisodeNumber",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "LockStoryDuringQuestions",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "QuestionModeStarted",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "QuestionsCompleted",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SeriesContext",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "ShowChildQuestions",
                table: "Sessions");
        }
    }
}
