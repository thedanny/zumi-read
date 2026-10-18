using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace StorySprout.Api;

public static class Catalog
{
    public static readonly string[] Skills = ["Recall", "Sequencing", "Main idea", "Cause and effect", "Inference", "Character motivation", "Vocabulary in context", "Supporting an answer with evidence", "Summarization"];
    public static readonly string[] Tones = ["Funny", "Adventurous", "Realistic", "Mysterious"];
    public static readonly string[] Supports = ["Independent", "Together", "Taking turns"];
    public static readonly string[] Outcomes = ["Independent", "Hint", "Needs practice", "Not assessed"];
    public static readonly string[] QuestionSupports = ["Reread together", "Used a hint", "Talked it through"];
    public const string LocalHousehold = "11111111-1111-1111-1111-111111111111";
}

public class Household { public Guid Id { get; set; } = Guid.Parse(Catalog.LocalHousehold); public string Name { get; set; } = "Our family"; public string Timezone { get; set; } = "America/New_York"; }
public class FamilyAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public string Email { get; set; } = "";
    public string NormalizedEmail { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class ReaderShare
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; }
    public Guid SessionId { get; set; }
    public string LinkTokenHash { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }
    public string? AccessTokenHash { get; set; }
    public DateTimeOffset? AccessExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
public class ChildProfile
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid HouseholdId { get; set; } = Guid.Parse(Catalog.LocalHousehold);
    public string Name { get; set; } = ""; public int? Age { get; set; } public string Grade { get; set; } = "";
    public string ReadingLevel { get; set; } = "Developing"; public string Interests { get; set; } = "Nature, animals";
    public string Tone { get; set; } = "Adventurous"; public int TargetWords { get; set; } = 350; public int QuestionCount { get; set; } = 4;
    public string SkillsJson { get; set; } = "[\"Recall\",\"Inference\",\"Sequencing\"]";
    public string GoalSkillsJson { get; set; } = "[]";
    public string AvoidTopics { get; set; } = ""; public string ReadingSupport { get; set; } = "Together";
    public int VocabularyDifficulty { get; set; } = 2; public int SentenceComplexity { get; set; } = 2;
    public int PlotComplexity { get; set; } = 2; public int InferenceDifficulty { get; set; } = 2; public int MemoryLoad { get; set; } = 2;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<string> Skills { get => JsonSerializer.Deserialize<List<string>>(SkillsJson) ?? []; set => SkillsJson = JsonSerializer.Serialize(value); }
    public List<string> GoalSkills { get => JsonSerializer.Deserialize<List<string>>(GoalSkillsJson) ?? []; set => GoalSkillsJson = JsonSerializer.Serialize(value); }
}
public class ReadingSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid HouseholdId { get; set; } = Guid.Parse(Catalog.LocalHousehold);
    public Guid ChildProfileId { get; set; } public string WeekdaysJson { get; set; } = "[1,3,5]";
    public string LocalTime { get; set; } = "17:00"; public string Timezone { get; set; } = "America/New_York";
    public bool Enabled { get; set; } = true; public bool PrepareDayBefore { get; set; }
    public List<int> Weekdays { get => JsonSerializer.Deserialize<List<int>>(WeekdaysJson) ?? []; set => WeekdaysJson = JsonSerializer.Serialize(value); }
}
public class StorySeries
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid HouseholdId { get; set; } = Guid.Parse(Catalog.LocalHousehold);
    public Guid ChildProfileId { get; set; }
    public string Title { get; set; } = "";
    public string Premise { get; set; } = "";
    public string CharacterNotes { get; set; } = "";
    public string Recap { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public class ReadingSession
{
    public Guid Id { get; set; } = Guid.NewGuid(); public Guid HouseholdId { get; set; } = Guid.Parse(Catalog.LocalHousehold);
    public Guid ChildProfileId { get; set; } public ChildProfile? ChildProfile { get; set; }
    public Guid? ScheduleId { get; set; } public Guid? RequestId { get; set; } public DateOnly TargetDate { get; set; }
    public Guid? SeriesId { get; set; } public int? EpisodeNumber { get; set; }
    public string SeriesContext { get; set; } = "";
    public string CharacterGuidance { get; set; } = "";
    public bool ShowChildQuestions { get; set; }
    public bool LockStoryDuringQuestions { get; set; }
    public bool QuestionModeStarted { get; set; }
    public bool QuestionsCompleted { get; set; }
    public string Theme { get; set; } = ""; public string Tone { get; set; } = ""; public int TargetWords { get; set; }
    public int QuestionCount { get; set; } public string PrimarySkill { get; set; } = ""; public string? SecondarySkill { get; set; }
    public int VocabularyDifficulty { get; set; } public int SentenceComplexity { get; set; } public int PlotComplexity { get; set; }
    public int InferenceDifficulty { get; set; } public int MemoryLoad { get; set; }
    public string AvoidTopics { get; set; } = ""; public string ReadingLevel { get; set; } = "";
    public string AdaptationNote { get; set; } = "No prior assessed feedback; use profile defaults.";
    public string JobStatus { get; set; } = "Queued"; public string SessionStatus { get; set; } = "Ready";
    public int Attempts { get; set; } public string? Error { get; set; } public string? ContentJson { get; set; }
    public bool IsDemo { get; set; } public string? ReadingDifficulty { get; set; } public string? InterestRating { get; set; } public int? StoryRating { get; set; } public string? ParentNotes { get; set; }
    public string FeedbackJson { get; set; } = "{}"; public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string QuestionSupportsJson { get; set; } = "{}";
    public DateTimeOffset? StartedAt { get; set; } public DateTimeOffset? CompletedAt { get; set; }
    public Dictionary<string,string> Feedback { get => JsonSerializer.Deserialize<Dictionary<string,string>>(FeedbackJson) ?? []; set => FeedbackJson = JsonSerializer.Serialize(value); }
    public Dictionary<string,List<string>> QuestionSupports { get => JsonSerializer.Deserialize<Dictionary<string,List<string>>>(QuestionSupportsJson) ?? []; set => QuestionSupportsJson = JsonSerializer.Serialize(value); }
    public StoryContent? Content => ContentJson is null ? null : JsonSerializer.Deserialize<StoryContent>(ContentJson, JsonOptions.Default);
}
public class StoryContent
{
    public string Title { get; set; } = ""; public List<StoryParagraph> Paragraphs { get; set; } = [];
    public string ParentSummary { get; set; } = ""; public string PrimaryObjective { get; set; } = "";
    public string SecondaryObjective { get; set; } = ""; public List<string> ChallengeVocabulary { get; set; } = [];
    public string EpisodeSummary { get; set; } = "";
    public string CharacterNotes { get; set; } = "";
    public string SeriesRecap { get; set; } = "";
    public List<StoryQuestion> Questions { get; set; } = [];
}
public class StoryParagraph { public string Id { get; set; } = ""; public string Text { get; set; } = ""; }
public class StoryQuestion
{
    public string Id { get; set; } = ""; public string Skill { get; set; } = ""; public string Text { get; set; } = "";
    public string Type { get; set; } = "Factual"; public string ExpectedAnswer { get; set; } = "";
    public List<string> AlternativeAnswers { get; set; } = []; public List<string> ParagraphIds { get; set; } = [];
    public string Hint { get; set; } = ""; public string FollowUp { get; set; } = "";
}
public static class JsonOptions { public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true }; }
public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<Household> Households => Set<Household>(); public DbSet<ChildProfile> Profiles => Set<ChildProfile>();
    public DbSet<FamilyAccount> Accounts => Set<FamilyAccount>();
    public DbSet<ReaderShare> ReaderShares => Set<ReaderShare>();
    public DbSet<ReadingSchedule> Schedules => Set<ReadingSchedule>(); public DbSet<ReadingSession> Sessions => Set<ReadingSession>();
    public DbSet<StorySeries> Series => Set<StorySeries>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<FamilyAccount>().HasIndex(x => x.NormalizedEmail).IsUnique();
        b.Entity<FamilyAccount>().HasIndex(x => x.HouseholdId);
        b.Entity<ReaderShare>().HasIndex(x => x.CodeHash).IsUnique();
        b.Entity<ReaderShare>().HasIndex(x => x.LinkTokenHash).IsUnique();
        b.Entity<ReaderShare>().HasIndex(x => x.AccessTokenHash).IsUnique().HasFilter("\"AccessTokenHash\" IS NOT NULL");
        b.Entity<ReaderShare>().HasIndex(x => new { x.HouseholdId, x.SessionId });
        b.Entity<ChildProfile>().Ignore(x => x.Skills);
        b.Entity<ChildProfile>().Ignore(x => x.GoalSkills);
        b.Entity<ReadingSchedule>().Ignore(x => x.Weekdays);
        b.Entity<ReadingSession>().Ignore(x => x.Feedback).Ignore(x => x.QuestionSupports).Ignore(x => x.Content).HasIndex(x => new { x.ScheduleId, x.TargetDate }).IsUnique().HasFilter("\"ScheduleId\" IS NOT NULL");
        b.Entity<ReadingSession>().HasIndex(x => new { x.HouseholdId, x.RequestId }).IsUnique().HasFilter("\"RequestId\" IS NOT NULL");
        b.Entity<ReadingSession>().HasIndex(x => new { x.HouseholdId, x.ChildProfileId, x.TargetDate });
        b.Entity<ReadingSession>().HasIndex(x => new { x.SeriesId, x.EpisodeNumber }).IsUnique().HasFilter("\"SeriesId\" IS NOT NULL");
        b.Entity<StorySeries>().HasIndex(x => new { x.HouseholdId, x.ChildProfileId });
    }
}
