using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using System.Net;
using System.Text.Json;
using StorySprout.Api;

namespace StorySprout.Tests;

public class RulesTests
{
    [Fact] public void Reader_invites_use_short_codes_and_distinct_random_secrets()
    {
        var code=ReaderSharing.NewCode();
        Assert.Matches("^[0-9]{8}$",code);
        var first=ReaderSharing.NewSecret();var second=ReaderSharing.NewSecret();
        Assert.NotEqual(first,second);
        Assert.Equal(43,first.Length);
        Assert.Equal(ReaderSharing.Hash(first),ReaderSharing.Hash(first));
        Assert.NotEqual(ReaderSharing.Hash(first),ReaderSharing.Hash(second));
    }
    [Fact] public void Shared_child_payload_hides_story_while_questions_are_locked()
    {
        var session=new ReadingSession {ShowChildQuestions=true,LockStoryDuringQuestions=true,QuestionModeStarted=true,
            ContentJson=JsonSerializer.Serialize(new StoryContent {Title="A story",Paragraphs=[new StoryParagraph {Id="p1",Text="A private story paragraph."}],Questions=[new StoryQuestion {Id="q1",Text="What happened?",ExpectedAnswer="An answer for parents."}]})};
        var payload=JsonSerializer.Serialize(ReaderSharing.ChildPayload(session),JsonOptions.Default);
        Assert.Contains("What happened?",payload);
        Assert.DoesNotContain("A private story paragraph.",payload);
        Assert.DoesNotContain("An answer for parents.",payload);
        session.QuestionsCompleted=true;
        payload=JsonSerializer.Serialize(ReaderSharing.ChildPayload(session),JsonOptions.Default);
        Assert.Contains("A private story paragraph.",payload);
    }
    [Fact] public void Profile_validation_rejects_invalid_difficulty_and_skill()
    {
        var p=new ChildProfile { Name="Reader",Skills=["Made up"],MemoryLoad=6 };
        var errors=Rules.ProfileErrors(p);
        Assert.Contains(errors,x=>x.Contains("skill"));Assert.Contains(errors,x=>x.Contains("Story depth"));
    }
    [Fact] public void Profile_focus_goals_must_be_selected_skills_and_limited_to_three()
    {
        var profile=new ChildProfile { Name="Reader",Skills=["Recall","Inference","Sequencing","Main idea"] };
        profile.GoalSkills=["Recall","Inference","Sequencing","Main idea"];
        Assert.Contains(Rules.ProfileErrors(profile),error=>error.Contains("focus goals"));
        profile.GoalSkills=["Recall","Vocabulary in context"];
        Assert.Contains(Rules.ProfileErrors(profile),error=>error.Contains("focus goals"));
        profile.GoalSkills=["Recall","Inference"];
        Assert.DoesNotContain(Rules.ProfileErrors(profile),error=>error.Contains("focus goals"));
    }
    [Fact] public async Task Demo_content_matches_plan_and_has_valid_references()
    {
        var s=new ReadingSession { Theme="gardens",TargetWords=350,QuestionCount=4,PrimarySkill="Recall",SecondarySkill="Inference" };
        var content=await new DemoProvider().Generate(s,default);
        Assert.Empty(Rules.ContentErrors(s,content));
        content.Questions[0].ParagraphIds=["missing"];
        Assert.Contains(Rules.ContentErrors(s,content),x=>x.Contains("missing paragraph"));
    }
    [Fact] public void Scheduler_uses_first_fall_occurrence_and_moves_spring_gap()
    {
        var zone=TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
        Assert.Equal(new DateTimeOffset(2026,11,1,5,30,0,TimeSpan.Zero),Rules.ResolveLocal(new DateOnly(2026,11,1),new TimeOnly(1,30),zone));
        Assert.Equal(new DateTimeOffset(2026,3,8,7,0,0,TimeSpan.Zero),Rules.ResolveLocal(new DateOnly(2026,3,8),new TimeOnly(2,30),zone));
    }
    [Fact] public async Task Next_plan_uses_grades_and_needs_practice_without_exceeding_profile_bounds()
    {
        var options=new DbContextOptionsBuilder<AppDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db=new AppDb(options);
        var profile=new ChildProfile { Name="Reader",Skills=["Recall","Inference"],VocabularyDifficulty=5,SentenceComplexity=2 };
        db.Profiles.Add(profile);
        var prior=new ReadingSession { ChildProfileId=profile.Id,HouseholdId=profile.HouseholdId,TargetDate=new DateOnly(2026,10,1),SessionStatus="Completed",CompletedAt=DateTimeOffset.UtcNow,ReadingDifficulty="Too easy",StoryRating=2,InterestRating="Okay",ContentJson=System.Text.Json.JsonSerializer.Serialize(new StoryContent { Questions=[new StoryQuestion { Id="q1",Skill="Inference" }] }),Feedback=new(){["q1"]="Needs practice"} };
        db.Sessions.Add(prior);await db.SaveChangesAsync();
        var next=await new SessionService(db).Create(profile.HouseholdId,profile.Id,new DateOnly(2026,10,3),null,null,null,null);
        Assert.Equal("Inference",next.PrimarySkill);Assert.Equal(5,next.VocabularyDifficulty);Assert.Equal(3,next.SentenceComplexity);Assert.Contains("rated 2/5",next.AdaptationNote);
        Assert.DoesNotContain("Reader",next.AdaptationNote);
    }
    [Fact] public async Task Next_plan_prioritizes_focus_goal_and_remembers_helpful_support()
    {
        var options=new DbContextOptionsBuilder<AppDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db=new AppDb(options);
        var profile=new ChildProfile { Name="Reader",Skills=["Recall","Inference","Sequencing"],GoalSkills=["Inference","Sequencing"] };
        db.Profiles.Add(profile);
        var prior=new ReadingSession { ChildProfileId=profile.Id,HouseholdId=profile.HouseholdId,TargetDate=new DateOnly(2026,10,1),SessionStatus="Completed",CompletedAt=DateTimeOffset.UtcNow,ContentJson=JsonSerializer.Serialize(new StoryContent { Questions=[new StoryQuestion { Id="q1",Skill="Inference" },new StoryQuestion { Id="q2",Skill="Sequencing" }] }),Feedback=new(){["q1"]="Hint",["q2"]="Needs practice"},QuestionSupports=new(){["q2"]=["Reread together","Talked it through"]} };
        db.Sessions.Add(prior);await db.SaveChangesAsync();
        var next=await new SessionService(db).Create(profile.HouseholdId,profile.Id,new DateOnly(2026,10,3),null,null,null,null);
        Assert.Equal("Sequencing",next.PrimarySkill);
        Assert.Contains("Parent focus goals: Inference, Sequencing",next.AdaptationNote);
        Assert.Contains("Sequencing: Reread together, Talked it through",next.AdaptationNote);
        Assert.DoesNotContain("Reader",next.AdaptationNote);
    }
    [Fact] public async Task Schedule_tick_is_idempotent_and_catches_up_only_two_days()
    {
        var services=new ServiceCollection(); var database=Guid.NewGuid().ToString();
        services.AddDbContext<AppDb>(o=>o.UseInMemoryDatabase(database));services.AddScoped<SessionService>();
        using var provider=services.BuildServiceProvider();
        Guid scheduleId;
        using(var scope=provider.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<AppDb>();var child=new ChildProfile { Name="Reader" };db.Profiles.Add(child);
            var schedule=new ReadingSchedule { ChildProfileId=child.Id,Weekdays=[0,1,2,3,4,5,6],LocalTime="00:00",Timezone="America/New_York" };
            db.Schedules.Add(schedule);scheduleId=schedule.Id;await db.SaveChangesAsync();
        }
        var worker=new ScheduleWorker(provider.GetRequiredService<IServiceScopeFactory>(),NullLogger<ScheduleWorker>.Instance);
        await worker.Tick();
        int firstCount;
        using(var scope=provider.CreateScope()) firstCount=await scope.ServiceProvider.GetRequiredService<AppDb>().Sessions.CountAsync(x=>x.ScheduleId==scheduleId);
        await worker.Tick();
        using(var scope=provider.CreateScope())
        {
            var sessions=await scope.ServiceProvider.GetRequiredService<AppDb>().Sessions.Where(x=>x.ScheduleId==scheduleId).ToListAsync();
            Assert.InRange(firstCount,1,3);
            Assert.Equal(firstCount,sessions.Count);
            Assert.Equal(firstCount,sessions.Select(x=>x.TargetDate).Distinct().Count());
        }
    }
    [Fact] public async Task OpenRouter_adapter_requests_structured_content_without_child_identity()
    {
        var handler=new FakeHandler();var client=new HttpClient(handler);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["OPENROUTER_API_KEY"]="test-key",["OPENROUTER_MODEL"]="test/model" }).Build();
        var session=new ReadingSession { TargetWords=220,QuestionCount=4,PrimarySkill="Recall",Theme="Gardens",Tone="Adventurous",ReadingLevel="Developing",AdaptationNote="Previous story rated 2/5.",ParentNotes="Private parent note" };
        var result=await new OpenRouterProvider(client,config).Generate(session,default);
        Assert.Equal("A sample title",result.Title);
        Assert.Equal("https://openrouter.ai/api/v1/chat/completions",handler.RequestUri);
        Assert.Contains("\"response_format\"",handler.RequestBody);
        Assert.Contains("\"require_parameters\":true",handler.RequestBody);
        Assert.DoesNotContain("Private parent note",handler.RequestBody);
        Assert.Contains("Do not use Mira by default",handler.RequestBody);
        Assert.Contains("Create fresh characters",handler.RequestBody);
    }
    [Fact] public async Task OpenRouter_uses_parent_character_ideas_for_standalone_story()
    {
        var handler=new FakeHandler();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["OPENROUTER_API_KEY"]="test-key",["OPENROUTER_MODEL"]="test/model" }).Build();
        var session=new ReadingSession { TargetWords=220,QuestionCount=4,PrimarySkill="Recall",Theme="Gardens",Tone="Adventurous",CharacterGuidance="Nia, a curious mapmaker, and Pip, her cautious fox companion." };
        await new OpenRouterProvider(new HttpClient(handler),config).Generate(session,default);
        Assert.Contains("Nia, a curious mapmaker, and Pip, her cautious fox companion",handler.RequestBody);
    }
    [Fact] public async Task Repeated_manual_request_reuses_one_persisted_job()
    {
        var options=new DbContextOptionsBuilder<AppDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db=new AppDb(options);var child=new ChildProfile { Name="Reader" };db.Profiles.Add(child);await db.SaveChangesAsync();
        var service=new SessionService(db);var token=Guid.NewGuid();var date=new DateOnly(2026,10,3);
        var first=await service.Create(child.HouseholdId,child.Id,date,"Trees",null,null,null,null,token);
        var second=await service.Create(child.HouseholdId,child.Id,date,"Trees",null,null,null,null,token);
        Assert.Equal(first.Id,second.Id);Assert.Single(await db.Sessions.ToListAsync());
    }
    [Fact] public async Task Session_creation_rejects_a_profile_from_another_household()
    {
        var options=new DbContextOptionsBuilder<AppDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db=new AppDb(options);
        var child=new ChildProfile {Name="Reader",HouseholdId=Guid.NewGuid()};
        db.Profiles.Add(child);await db.SaveChangesAsync();
        var otherHousehold=Guid.NewGuid();
        await Assert.ThrowsAsync<InvalidOperationException>(()=>new SessionService(db).Create(otherHousehold,child.Id,new DateOnly(2026,10,3),null,null,null,null));
        Assert.Empty(await db.Sessions.ToListAsync());
    }
    [Fact] public async Task OpenRouter_receives_series_memory_and_requests_continuity_fields()
    {
        var handler=new FakeHandler();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["OPENROUTER_API_KEY"]="test-key",["OPENROUTER_MODEL"]="test/model" }).Build();
        var session=new ReadingSession {SeriesId=Guid.NewGuid(),EpisodeNumber=2,SeriesContext="Mira observes before acting. Episode 1: helped a neighbor.",TargetWords=220,QuestionCount=4,PrimarySkill="Recall",Theme="Riverside",Tone="Adventurous"};
        await new OpenRouterProvider(new HttpClient(handler),config).Generate(session,default);
        Assert.Contains("Mira observes before acting",handler.RequestBody);
        Assert.Contains("episodeSummary",handler.RequestBody);
        Assert.Contains("characterNotes",handler.RequestBody);
        Assert.Contains("seriesRecap",handler.RequestBody);
    }
    [Fact] public async Task Continuing_episode_uses_persisted_canon_and_new_setting()
    {
        var options=new DbContextOptionsBuilder<AppDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db=new AppDb(options);
        var child=new ChildProfile {Name="Reader"};
        var series=new StorySeries {ChildProfileId=child.Id,Title="Mira's adventures",Premise="Mira and Grandfather help their neighbors.",CharacterNotes="Mira observes before acting."};
        db.Profiles.Add(child);db.Series.Add(series);await db.SaveChangesAsync();
        var service=new SessionService(db);
        var first=await service.Create(child.HouseholdId,child.Id,new DateOnly(2026,10,3),"Garden","Adventurous",350,4,null,null,series.Id);
        Assert.Equal(1,first.EpisodeNumber);
        var content=await new DemoProvider().Generate(first,default);
        Assert.Empty(Rules.ContentErrors(first,content));
        first.ContentJson=JsonSerializer.Serialize(content,JsonOptions.Default);first.JobStatus="Ready";
        series.CharacterNotes=content.CharacterNotes;series.Recap=content.SeriesRecap;await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(()=>service.Create(child.HouseholdId,child.Id,new DateOnly(2026,10,4),"Garden","Adventurous",350,4,null,null,series.Id));
        var second=await service.Create(child.HouseholdId,child.Id,new DateOnly(2026,10,4),"Riverside","Adventurous",350,4,null,null,series.Id);
        Assert.Equal(2,second.EpisodeNumber);
        Assert.Contains(content.EpisodeSummary,second.SeriesContext);
        Assert.Contains(content.CharacterNotes,second.SeriesContext);
        var sequel=await new DemoProvider().Generate(second,default);
        Assert.Empty(Rules.ContentErrors(second,sequel));
        Assert.Contains("Riverside",sequel.Title);
        Assert.DoesNotContain("Green Leaf Box",sequel.Title);
    }
    private sealed class FakeHandler : HttpMessageHandler
    {
        public string RequestUri="";public string RequestBody="";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            RequestUri=request.RequestUri?.ToString()??"";RequestBody=await request.Content!.ReadAsStringAsync(cancellationToken);
            var answer=JsonSerializer.Serialize(new StoryContent { Title="A sample title" });
            return new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent(JsonSerializer.Serialize(new { choices=new[]{new { message=new { content=answer }}} })) };
        }
    }
}
