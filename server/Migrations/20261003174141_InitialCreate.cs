using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StorySprout.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Households",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Timezone = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Households", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Age = table.Column<int>(type: "integer", nullable: true),
                    Grade = table.Column<string>(type: "text", nullable: false),
                    ReadingLevel = table.Column<string>(type: "text", nullable: false),
                    Interests = table.Column<string>(type: "text", nullable: false),
                    Tone = table.Column<string>(type: "text", nullable: false),
                    TargetWords = table.Column<int>(type: "integer", nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    SkillsJson = table.Column<string>(type: "text", nullable: false),
                    AvoidTopics = table.Column<string>(type: "text", nullable: false),
                    ReadingSupport = table.Column<string>(type: "text", nullable: false),
                    VocabularyDifficulty = table.Column<int>(type: "integer", nullable: false),
                    SentenceComplexity = table.Column<int>(type: "integer", nullable: false),
                    PlotComplexity = table.Column<int>(type: "integer", nullable: false),
                    InferenceDifficulty = table.Column<int>(type: "integer", nullable: false),
                    MemoryLoad = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Schedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    WeekdaysJson = table.Column<string>(type: "text", nullable: false),
                    LocalTime = table.Column<string>(type: "text", nullable: false),
                    Timezone = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    PrepareDayBefore = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Schedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HouseholdId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduleId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Theme = table.Column<string>(type: "text", nullable: false),
                    Tone = table.Column<string>(type: "text", nullable: false),
                    TargetWords = table.Column<int>(type: "integer", nullable: false),
                    QuestionCount = table.Column<int>(type: "integer", nullable: false),
                    PrimarySkill = table.Column<string>(type: "text", nullable: false),
                    SecondarySkill = table.Column<string>(type: "text", nullable: true),
                    VocabularyDifficulty = table.Column<int>(type: "integer", nullable: false),
                    SentenceComplexity = table.Column<int>(type: "integer", nullable: false),
                    PlotComplexity = table.Column<int>(type: "integer", nullable: false),
                    InferenceDifficulty = table.Column<int>(type: "integer", nullable: false),
                    MemoryLoad = table.Column<int>(type: "integer", nullable: false),
                    AvoidTopics = table.Column<string>(type: "text", nullable: false),
                    ReadingLevel = table.Column<string>(type: "text", nullable: false),
                    JobStatus = table.Column<string>(type: "text", nullable: false),
                    SessionStatus = table.Column<string>(type: "text", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    ContentJson = table.Column<string>(type: "text", nullable: true),
                    IsDemo = table.Column<bool>(type: "boolean", nullable: false),
                    ReadingDifficulty = table.Column<string>(type: "text", nullable: true),
                    InterestRating = table.Column<string>(type: "text", nullable: true),
                    ParentNotes = table.Column<string>(type: "text", nullable: true),
                    FeedbackJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sessions_Profiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "Profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ChildProfileId",
                table: "Sessions",
                column: "ChildProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_HouseholdId_ChildProfileId_TargetDate",
                table: "Sessions",
                columns: new[] { "HouseholdId", "ChildProfileId", "TargetDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ScheduleId_TargetDate",
                table: "Sessions",
                columns: new[] { "ScheduleId", "TargetDate" },
                unique: true,
                filter: "\"ScheduleId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Households");

            migrationBuilder.DropTable(
                name: "Schedules");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Profiles");
        }
    }
}
