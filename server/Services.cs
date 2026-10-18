using System.Net;
using System.Security.Cryptography;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace StorySprout.Api;

public static class Rules
{
    public static List<string> ProfileErrors(ChildProfile p)
    {
        var e = new List<string>();
        if (string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 60) e.Add("Name must be 1–60 characters.");
        if (p.Age is < 3 or > 18) e.Add("Age must be 3–18 when supplied.");
        if (p.Grade.Length > 30 || p.Interests.Length > 300 || p.AvoidTopics.Length > 300) e.Add("Profile text is too long.");
        if (p.TargetWords is < 150 or > 900) e.Add("Story length must be 150–900 words.");
        if (p.QuestionCount is < 2 or > 8) e.Add("Question count must be 2–8.");
        if (!Catalog.Tones.Contains(p.Tone) || !Catalog.Supports.Contains(p.ReadingSupport)) e.Add("Choose a valid tone and reading support.");
        if (p.Skills.Count == 0 || p.Skills.Any(s => !Catalog.Skills.Contains(s))) e.Add("Choose at least one valid comprehension skill.");
        if (p.GoalSkills.Count > 3 || p.GoalSkills.Distinct().Count() != p.GoalSkills.Count || p.GoalSkills.Any(s => !p.Skills.Contains(s))) e.Add("Choose up to three focus goals from the skills to practice.");
        if (new[] { p.VocabularyDifficulty,p.SentenceComplexity,p.PlotComplexity,p.InferenceDifficulty,p.MemoryLoad }.Any(v => v is < 1 or > 5)) e.Add("Story depth settings must be 1–5.");
        return e;
    }
    public static List<string> ContentErrors(ReadingSession s, StoryContent? c)
    {
        var e = new List<string>();
        if (c is null) return ["No structured content returned."];
        if (string.IsNullOrWhiteSpace(c.Title) || c.Title.Length > 150 || string.IsNullOrWhiteSpace(c.ParentSummary) || c.ParentSummary.Length > 1200) e.Add("Title or summary is missing or too long.");
        if (string.IsNullOrWhiteSpace(c.PrimaryObjective) || c.PrimaryObjective.Length > 400 || c.SecondaryObjective.Length > 400) e.Add("Learning objectives are missing or too long.");
        if (c.Paragraphs.Count is < 2 or > 16 || c.Paragraphs.Any(p => string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Text) || p.Text.Length > 2500)) e.Add("Invalid paragraphs.");
        var ids = c.Paragraphs.Select(p => p.Id).ToHashSet();
        if (ids.Count != c.Paragraphs.Count) e.Add("Paragraph identifiers must be unique.");
        var words = Regex.Matches(string.Join(" ", c.Paragraphs.Select(p => p.Text)), @"\b[\p{L}\p{N}']+\b").Count;
        if (words < Math.Floor(s.TargetWords * .75) || words > Math.Ceiling(s.TargetWords * 1.25)) e.Add($"Story word count {words} is outside the 25% tolerance around {s.TargetWords}.");
        if (c.Questions.Count != s.QuestionCount) e.Add("Question count does not match the plan.");
        if (c.Questions.Select(q => q.Id).Distinct().Count() != c.Questions.Count) e.Add("Question identifiers must be unique.");
        foreach (var q in c.Questions)
        {
            if (string.IsNullOrWhiteSpace(q.Id) || string.IsNullOrWhiteSpace(q.Text) || string.IsNullOrWhiteSpace(q.ExpectedAnswer) || string.IsNullOrWhiteSpace(q.Hint) || q.Text.Length > 500 || q.ExpectedAnswer.Length > 800 || q.Hint.Length > 500) e.Add("A question has missing or oversized fields.");
            if (!Catalog.Skills.Contains(q.Skill) || !new[] { "Factual", "Open" }.Contains(q.Type)) e.Add("A question has an invalid skill or type.");
            if (q.ParagraphIds.Count == 0 || q.ParagraphIds.Any(id => !ids.Contains(id))) e.Add("A question refers to a missing paragraph.");
            if (q.AlternativeAnswers.Count > 8 || q.AlternativeAnswers.Any(a => a.Length > 400) || q.FollowUp.Length > 500) e.Add("Question details are too long.");
        }
        if (!c.Questions.Any(q => q.Skill == s.PrimarySkill)) e.Add("Primary skill is not practiced.");
        if (c.ChallengeVocabulary.Count > 12 || c.ChallengeVocabulary.Any(v => v.Length > 80)) e.Add("Vocabulary list is too long.");
        if (s.SeriesId.HasValue && (string.IsNullOrWhiteSpace(c.EpisodeSummary) || c.EpisodeSummary.Length > 800 || string.IsNullOrWhiteSpace(c.CharacterNotes) || c.CharacterNotes.Length > 1000 || string.IsNullOrWhiteSpace(c.SeriesRecap) || c.SeriesRecap.Length > 1600)) e.Add("Series memory is missing or too long.");
        return e.Distinct().ToList();
    }
    public static string PickSkill(ChildProfile profile, int existingCount)
    {
        var skills = profile.Skills.Distinct().ToArray(); return skills[existingCount % skills.Length];
    }
    public static DateTimeOffset ResolveLocal(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local)) local = local.AddMinutes(1);
        var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset).ToUniversalTime();
    }
    public static DateOnly LocalToday(TimeZoneInfo zone, DateTimeOffset now) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
}

public interface IStoryProvider { Task<StoryContent> Generate(ReadingSession session, CancellationToken ct); bool IsDemo { get; } }
public sealed class ProviderException(string message, bool transient) : Exception(message) { public bool Transient { get; } = transient; }

public class DemoProvider : IStoryProvider
{
    public bool IsDemo => true;
    public Task<StoryContent> Generate(ReadingSession s, CancellationToken ct)
    {
        var theme = string.IsNullOrWhiteSpace(s.Theme) ? "the garden" : s.Theme.Trim();
        var paragraphs = new List<StoryParagraph>
        {
            new() { Id="p1", Text=$"On Saturday morning, Mira visited a neighborhood fair about {theme} with her grandfather. Near a crooked fence, she noticed a small wooden box with a painted green leaf on its lid. Inside was a note asking anyone who found it to help a neighbor. Mira wondered who had left the box and why. She put the note back carefully and looked around for a clue." },
            new() { Id="p2", Text=$"A trail of tiny blue ribbons led Mira past a bench to Mrs. Vale's porch. Mrs. Vale was carrying pots of seedlings, but the wind had scattered the labels. Without those labels, she could not tell which plants needed shade. Mira offered to help sort them. Grandfather suggested looking at the leaf shapes before making a guess." },
            new() { Id="p3", Text=$"Mira studied each plant instead of rushing. The broad leaves matched the drawing on one fallen label, while the narrow leaves matched another. She and Mrs. Vale placed the labels beside the right pots. One ribbon had caught on a watering can, so Mira tied it back to the handle. Now the trail made sense: Mrs. Vale had left it for a willing helper." },
            new() { Id="p4", Text=$"When the pots were safe, Mrs. Vale smiled and gave Mira a paper packet of seeds. Mira asked if they could plant them together next week. Grandfather agreed. On the walk home, Mira realized that the mysterious box had led her to something better than treasure: a new friendship and a useful job well done." }
        };
        string[] extras = ["Mira paused to notice what had changed since she first arrived. She could hear the breeze moving softly through the nearby trees and see sunlight crossing the path.", "Grandfather did not solve the puzzle for her. He asked what she could observe, and he waited while she put her own ideas into words.", "Mrs. Vale thanked Mira for checking the clues carefully. A patient choice had made the work easier for everyone, even though it took a little longer at first.", "Mira kept thinking about the note as she worked. She liked that the invitation had asked for help, because it made the discovery feel like a shared adventure."];
        var index = 0;
        while (WordCount(paragraphs) < s.TargetWords * .86 && index < 28) { paragraphs[index % 4].Text += " " + extras[index % 4]; index++; }
        var questions = new List<StoryQuestion>();
        for (var i = 0; i < s.QuestionCount; i++)
        {
            var skill = i == 0 ? s.PrimarySkill : (i == 1 && s.SecondarySkill is not null ? s.SecondarySkill : Catalog.Skills[(Array.IndexOf(Catalog.Skills, s.PrimarySkill) + i) % Catalog.Skills.Length]);
            var v = skill switch {
                "Sequencing" => ("What happened after Mira followed the blue ribbons?", "She reached Mrs. Vale's porch and offered to help with the seedlings.", new[]{"p2"}, "Follow the ribbons in the second paragraph.", "What happened after the plants were sorted?", "Factual"),
                "Main idea" => ("What is this story mostly about?", "Mira follows a clue, helps a neighbor solve a problem, and makes a new friend.", new[]{"p1","p2","p3","p4"}, "Think about the problem and how it ends.", "What title would you give the story?", "Open"),
                "Cause and effect" => ("Why could Mrs. Vale not tell which plants needed shade?", "The wind had scattered the labels.", new[]{"p2"}, "What happened to the labels?", "What changed after Mira sorted them?", "Factual"),
                "Inference" => ("What clues suggest Mrs. Vale left the blue ribbons?", "A ribbon was caught on her watering can, and the trail led to her porch; other text-supported ideas are welcome.", new[]{"p2","p3"}, "Where did the ribbons lead?", "Why might she have made a trail?", "Open"),
                "Character motivation" => ("Why do you think Mira chose to help Mrs. Vale?", "She was curious about the note and wanted to help a neighbor; reasonable ideas supported by the story are welcome.", new[]{"p1","p2"}, "Remember what the note asked for.", "Would you have helped? Why?", "Open"),
                "Vocabulary in context" => ("What are seedlings in this story?", "Young plants in pots, as suggested by the later planting and leaf clues.", new[]{"p2","p3","p4"}, "Look for the pots and leaves.", "What might a seedling need to grow?", "Open"),
                "Supporting an answer with evidence" => ("Was Mira careful while solving the problem? Give a detail from the story.", "Yes. She studied leaf shapes instead of rushing and checked them against the labels; other supported details are welcome.", new[]{"p3"}, "Look at how she matched the plants.", "Why did that detail help?", "Open"),
                "Summarization" => ("Tell the story in a few sentences: what happened at the beginning, middle, and end?", "Mira found a note, followed ribbons, helped sort seedlings, and made a new friend. Equivalent summaries are welcome.", new[]{"p1","p2","p3","p4"}, "Start with the box, then the plants, then the walk home.", "Which event matters most?", "Open"),
                _ => ("What did Mira find by the fence?", "A small wooden box with a note asking someone to help a neighbor.", new[]{"p1"}, "Look near the fence in the first paragraph.", "Why might someone leave a note there?", "Factual")
            };
            questions.Add(new StoryQuestion { Id=$"q{i+1}", Skill=skill, Text=v.Item1, ExpectedAnswer=v.Item2, ParagraphIds=v.Item3.ToList(), Hint=v.Item4, FollowUp=v.Item5, Type=v.Item6, AlternativeAnswers=v.Item6=="Open" ? ["Another reasonable answer supported by the cited paragraphs."] : [] });
        }
        var result = new StoryContent { Title="Mira and the Green Leaf Box", Paragraphs=paragraphs, ParentSummary="Mira follows a note and ribbon trail to help a neighbor sort mixed-up seedlings. She uses clues patiently and forms a new friendship.", PrimaryObjective=$"Practice {s.PrimarySkill.ToLowerInvariant()} using details from the story.", SecondaryObjective=s.SecondarySkill is null ? "Notice how careful observation helps solve a problem." : $"Also practice {s.SecondarySkill.ToLowerInvariant()}.", ChallengeVocabulary=["seedlings", "scattered"], Questions=questions, EpisodeSummary="Mira followed blue ribbons to Mrs. Vale's porch and patiently sorted wind-scattered seedling labels. She gained a new friend and seeds to plant with Grandfather.", CharacterNotes="Mira is curious, observant, patient, and eager to help. Grandfather guides with questions instead of solving problems for her. Mrs. Vale is a grateful neighbor and gardener.", SeriesRecap="Mira and Grandfather found a green leaf box that led to Mrs. Vale's garden. Mira helped sort seedlings, and Mrs. Vale gave her seeds. The three are friends." };
        if (s.EpisodeNumber is > 1) return Task.FromResult(ContinuingDemo(s));
        return Task.FromResult(result);
    }
    private static StoryContent ContinuingDemo(ReadingSession s)
    {
        var place = s.Theme.Trim();
        var paragraphs = new List<StoryParagraph> {
            new() { Id="p1", Text=$"The next Saturday, Mira and Grandfather explored {place}. Mrs. Vale had invited them to find a quiet place for a community reading picnic. Mira carried a small map and the seeds from their last adventure. When she noticed a path missing from the map, she wondered where it led. Grandfather smiled and asked what clues she could see." },
            new() { Id="p2", Text="A painted sign pointed toward an old stone bridge, but its arrow had turned in the wind. Beside it, Mira found fresh footprints and a folded paper crane. A younger child nearby said the picnic basket had been taken to a sheltered spot before the rain. Mira listened carefully and checked the footprints rather than guessing." },
            new() { Id="p3", Text="Mira followed the footprints to a small covered pavilion. The basket was safe there, and Mrs. Vale was setting out books. The paper crane was a marker she had left for helpers. Mira suggested drawing the bridge and pavilion on the map so other families could find them easily. Grandfather gave her the pencil and let her decide where each landmark belonged." },
            new() { Id="p4", Text="When the families arrived, Mira showed them the new map. Everyone found a place to read while rain tapped softly on the pavilion roof. Mrs. Vale thanked Mira for being careful and thoughtful. Mira still loved a mystery, but she liked it most when solving one helped other people. After the picnic, she and Grandfather planned to plant the seeds together." }
        };
        string[] extras=["Mira stopped to compare the marks on the ground with the lines on her map. She knew a careful look could reveal more than a quick guess.","Grandfather asked her to describe what she noticed. He listened, then encouraged her to test her idea with another clue.","Mrs. Vale welcomed the new visitors and thanked everyone who helped. Mira saw that sharing the discovery made the day brighter.","Mira remembered the garden puzzle from last week. She was learning that patience and kindness could make an ordinary day feel adventurous."];
        for(var i=0;WordCount(paragraphs)<s.TargetWords*.86 && i<28;i++) paragraphs[i%4].Text+=" "+extras[i%4];
        var questions=new List<StoryQuestion>();
        for(var i=0;i<s.QuestionCount;i++)
        {
            var skill=i==0?s.PrimarySkill:i==1 && s.SecondarySkill is not null?s.SecondarySkill:Catalog.Skills[(Array.IndexOf(Catalog.Skills,s.PrimarySkill)+i)%Catalog.Skills.Length];
            var v=skill switch {
                "Sequencing" => ("What did Mira do after she found the paper crane?","She followed the footprints to the pavilion.",new[]{"p2","p3"}),
                "Main idea" => ("What is this episode mostly about?","Mira uses clues to find the picnic place and helps other families find it.",new[]{"p1","p2","p3","p4"}),
                "Cause and effect" => ("Why did Mira draw landmarks on the map?","So other families could find the pavilion easily.",new[]{"p3","p4"}),
                "Inference" => ("Why did Mrs. Vale leave a paper crane?","It was a marker to help people find the sheltered pavilion.",new[]{"p2","p3"}),
                "Character motivation" => ("Why did Mira check the footprints instead of guessing?","She wanted to find the picnic basket carefully and help others.",new[]{"p2","p3"}),
                "Vocabulary in context" => ("What is a pavilion in this story?","A covered place where the picnic and reading took place.",new[]{"p3","p4"}),
                "Supporting an answer with evidence" => ("Was Mira helpful? Give a detail.","Yes. She drew the landmarks on the map and guided families to the pavilion.",new[]{"p3","p4"}),
                "Summarization" => ("What happened at the beginning, middle, and end?","Mira sought the picnic site, followed clues to the pavilion, and helped families find it.",new[]{"p1","p2","p3","p4"}),
                _ => ("Where did Mira find the picnic basket?","At a covered pavilion.",new[]{"p3"})
            };
            questions.Add(new StoryQuestion { Id=$"q{i+1}",Skill=skill,Text=v.Item1,ExpectedAnswer=v.Item2,ParagraphIds=v.Item3.ToList(),Hint="Look back at the cited part of the story.",FollowUp="What clue helped Mira most?",Type="Open",AlternativeAnswers=["Another answer supported by the story."] });
        }
        return new StoryContent { Title=$"Mira and the Map of {place}",Paragraphs=paragraphs,ParentSummary="Mira follows clues to find a sheltered reading picnic, then improves a map to help other families.",PrimaryObjective=$"Practice {s.PrimarySkill.ToLowerInvariant()} using story details.",SecondaryObjective="Notice how Mira uses patience and kindness in a new setting.",ChallengeVocabulary=["pavilion","landmark"],Questions=questions,EpisodeSummary=$"At {place}, Mira followed footprints and a paper crane to a sheltered reading picnic, then improved a map for other families.",CharacterNotes="Mira is curious, observant, patient, and eager to help. Grandfather guides with questions instead of solving problems for her. Mrs. Vale is a grateful neighbor and gardener.",SeriesRecap=$"Mira and Grandfather first helped Mrs. Vale sort seedlings in her garden. Later, at {place}, Mira found a sheltered reading picnic and guided families with a new map. Their friendship continues." };
    }
    private static int WordCount(List<StoryParagraph> ps) => Regex.Matches(string.Join(" ",ps.Select(p=>p.Text)), @"\b[\p{L}\p{N}']+\b").Count;
}

public class OpenRouterProvider(HttpClient client, IConfiguration config) : IStoryProvider
{
    private static readonly string[] ProtagonistNames = ["Ari", "Asha", "Benji", "Cleo", "Dara", "Eli", "Esme", "Farah", "Finn", "Inez", "Jae", "Jun", "Kai", "Lena", "Leo", "Luz", "Mina", "Nico", "Noor", "Omar", "Pia", "Quinn", "Ravi", "Remy", "Rosa", "Sam", "Sana", "Tali", "Theo", "Uma", "Vera", "Wren", "Yara", "Zane"];

    private static string RandomProtagonistName() => ProtagonistNames[RandomNumberGenerator.GetInt32(ProtagonistNames.Length)];

    public bool IsDemo => false;
    public async Task<StoryContent> Generate(ReadingSession s, CancellationToken ct)
    {
        var key = config["OPENROUTER_API_KEY"]; var model = config["OPENROUTER_MODEL"];
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(model)) throw new ProviderException("Set OPENROUTER_API_KEY and OPENROUTER_MODEL in the server environment.", false);
        var schema = JsonSerializer.Deserialize<JsonElement>("""
            {
              "type": "object",
              "properties": {
                "title": {"type":"string"},
                "paragraphs": {"type":"array","items":{"type":"object","properties":{"id":{"type":"string"},"text":{"type":"string"}},"required":["id","text"],"additionalProperties":false}},
                "parentSummary": {"type":"string"},
                "primaryObjective": {"type":"string"},
                "secondaryObjective": {"type":"string"},
                "challengeVocabulary": {"type":"array","items":{"type":"string"}},
                "episodeSummary": {"type":"string"},
                "characterNotes": {"type":"string"},
                "seriesRecap": {"type":"string"},
                "questions": {"type":"array","items":{"type":"object","properties":{
                  "id":{"type":"string"},"skill":{"type":"string"},"text":{"type":"string"},
                  "type":{"type":"string","enum":["Factual","Open"]},"expectedAnswer":{"type":"string"},
                  "alternativeAnswers":{"type":"array","items":{"type":"string"}},
                  "paragraphIds":{"type":"array","items":{"type":"string"}},
                  "hint":{"type":"string"},"followUp":{"type":"string"}
                },"required":["id","skill","text","type","expectedAnswer","alternativeAnswers","paragraphIds","hint","followUp"],"additionalProperties":false}}
              },
              "required": ["title","paragraphs","parentSummary","primaryObjective","secondaryObjective","challengeVocabulary","episodeSummary","characterNotes","seriesRecap","questions"],
              "additionalProperties": false
            }
            """);
        var seriesInstruction=s.SeriesId.HasValue ? $"This is episode {s.EpisodeNumber} of a continuing series. Canon and prior episode memory: {s.SeriesContext}. Preserve established character personalities, relationships, and major events. Set this episode in the new requested setting, with a distinct adventure and a complete beginning, middle, and end. Write episodeSummary (one concise recap of this episode), characterNotes (stable traits, relationships, and any meaningful change), and seriesRecap (a compact cumulative high-level canon summary for future episodes)." : "For a standalone story, set episodeSummary, characterNotes, and seriesRecap to empty strings.";
        var characterInstruction=s.SeriesId.HasValue ? "Use the continuing series characters and names from its canon." : string.IsNullOrWhiteSpace(s.CharacterGuidance) ? $"Create fresh characters appropriate to the setting and use {RandomProtagonistName()} as the fictional protagonist's name. Invent any supporting character names needed. Do not use Mira by default or the child's profile name." : $"Use the parent's fictional character direction and names: {s.CharacterGuidance}. If the parent described characters but did not name a protagonist, use {RandomProtagonistName()} as the protagonist name. Do not use the child's profile name.";
        var prompt = $"Write a child-safe original story and a parent discussion guide from the SAME story. {characterInstruction} Theme or new setting: {s.Theme}. Tone: {s.Tone}. Approximate story words: {s.TargetWords} (within 25%). Reading level selected by parent: {s.ReadingLevel}; this is guidance, not a validated score. Difficulty 1-5: vocabulary {s.VocabularyDifficulty}, sentences {s.SentenceComplexity}, plot {s.PlotComplexity}, inference {s.InferenceDifficulty}, memory {s.MemoryLoad}. Prior progress guidance: {s.AdaptationNote}. Avoid: {s.AvoidTopics}. Primary skill: {s.PrimarySkill}. Secondary skill: {s.SecondarySkill ?? "none"}. {seriesInstruction} Return exactly {s.QuestionCount} questions with unique IDs and references to actual paragraph IDs. Every answer must follow from the story, except explicit predictions/reflections. Open answers should accept reasonable text-supported ideas. Keep the summary brief enough for a parent who has not read the story. No child name or parent notes have been supplied.";
        using var req = new HttpRequestMessage(HttpMethod.Post,"https://openrouter.ai/api/v1/chat/completions"); req.Headers.Authorization = new AuthenticationHeaderValue("Bearer",key);
        req.Content = new StringContent(JsonSerializer.Serialize(new { model, messages = new[] { new { role="system", content="Create age-appropriate reading content. Follow the supplied JSON schema. Never include unsafe or hateful content." }, new { role="user", content=prompt } }, response_format = new { type="json_schema", json_schema = new { name="story_session", strict=true, schema } }, provider = new { require_parameters=true } }), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try { response = await client.SendAsync(req, ct); } catch (TaskCanceledException) when (!ct.IsCancellationRequested) { throw new ProviderException("Generation timed out. Retry the session.",true); } catch (HttpRequestException) { throw new ProviderException("Could not reach the generation provider. Retry the session.",true); }
        using (response)
        {
            if (!response.IsSuccessStatusCode) throw new ProviderException(response.StatusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout ? "Provider temporarily unavailable. Retry shortly." : "Provider rejected the request. Check provider settings.", (int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests);
            try
            {
                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var json=doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
                if (json.Length > 60000) throw new ProviderException("Generated content exceeded size limit.",false);
                return JsonSerializer.Deserialize<StoryContent>(json,JsonOptions.Default) ?? throw new InvalidContentException("Provider returned empty content.");
            }
            catch (JsonException) { throw new InvalidContentException("Provider returned invalid structured content."); }
            catch (KeyNotFoundException) { throw new InvalidContentException("Provider returned no story content."); }
            catch (InvalidOperationException) { throw new InvalidContentException("Provider returned unexpected content."); }
        }
    }
}

public class SessionService(AppDb db)
{
    public async Task<ReadingSession> Create(Guid householdId, Guid profileId, DateOnly date, string? theme, string? tone, int? words, int? questions, Guid? scheduleId = null, Guid? requestId = null, Guid? seriesId = null, string? characterGuidance = null)
    {
        if(requestId.HasValue && await db.Sessions.SingleOrDefaultAsync(x=>x.HouseholdId==householdId && x.RequestId==requestId) is { } existing) return existing;
        var p = await db.Profiles.SingleAsync(x=>x.Id==profileId && x.HouseholdId==householdId);
        StorySeries? series=null; int? episodeNumber=null; string seriesContext="";
        if(seriesId.HasValue)
        {
            series=await db.Series.SingleOrDefaultAsync(x=>x.Id==seriesId && x.HouseholdId==householdId && x.ChildProfileId==profileId);
            if(series is null) throw new ArgumentException("Choose a series for this child.");
            var episodes=await db.Sessions.Where(x=>x.SeriesId==seriesId).OrderByDescending(x=>x.EpisodeNumber).Take(4).ToListAsync();
            if(episodes.Any(x=>x.JobStatus!="Ready")) throw new ArgumentException("Finish the current episode before creating the next one.");
            if(episodes.Count>0 && string.Equals(episodes[0].Theme.Trim(),(theme??"").Trim(),StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose a different setting for the next episode.");
            episodeNumber=(episodes.FirstOrDefault()?.EpisodeNumber??0)+1;
            var recentSummaries=episodes.OrderBy(x=>x.EpisodeNumber).Select(x=>$"Episode {x.EpisodeNumber}: {x.Content?.EpisodeSummary}");
            seriesContext=$"Series: {series.Title}. Premise: {series.Premise}. Established characters: {series.CharacterNotes}. Cumulative recap: {series.Recap}. Recent episodes: {string.Join(" | ",recentSummaries)}";
        }
        var count = await db.Sessions.CountAsync(x=>x.ChildProfileId==p.Id && x.HouseholdId==householdId);
        var recent = await db.Sessions.Where(x=>x.ChildProfileId==p.Id && x.HouseholdId==householdId && x.SessionStatus=="Completed").OrderByDescending(x=>x.CompletedAt).Take(5).ToListAsync();
        var focusSkills=p.GoalSkills.Count>0?p.GoalSkills:p.Skills;
        var latestBySkill=recent.SelectMany(session=>session.Content?.Questions
                .Where(q=>session.Feedback.TryGetValue(q.Id,out var outcome) && outcome!="Not assessed")
                .Select(q=>new { q.Skill,Outcome=session.Feedback[q.Id] }) ?? [])
            .Where(x=>focusSkills.Contains(x.Skill)).GroupBy(x=>x.Skill).Select(group=>group.First()).ToList();
        var needs=latestBySkill.FirstOrDefault(x=>x.Outcome=="Needs practice")?.Skill;
        var withHelp=latestBySkill.FirstOrDefault(x=>x.Outcome=="Hint")?.Skill;
        var skill=needs ?? withHelp ?? focusSkills[count%focusSkills.Count];
        var last=recent.FirstOrDefault(); var shift=last?.ReadingDifficulty=="Too easy" ? 1 : last?.ReadingDifficulty=="Too difficult" ? -1 : 0;
        var ratingGuidance=last?.StoryRating is <=2 ? "Use a fresh plot hook and avoid repeating the prior story structure." : last?.StoryRating is >=4 ? "Keep an engaging tone while making the new plot original." : "Use the selected theme and tone.";
        var note=last is null ? "No prior assessed feedback; use profile defaults." : $"Previous story rated {last.StoryRating?.ToString() ?? "unrated"}/5 by parent; reading challenge: {last.ReadingDifficulty ?? "unreported"}; interest: {last.InterestRating ?? "unreported"}. {ratingGuidance} {(needs is not null ? $"Revisit {needs} because the latest observation for that skill needs more practice." : withHelp is not null ? $"Practice {withHelp} with clear story clues because the latest observation needed a little help." : "Keep rotating skills.")} Match the challenge to these observations without treating them as a validated reading score.";
        if(p.GoalSkills.Count>0)note+=$" Parent focus goals: {string.Join(", ",p.GoalSkills)}.";
        if(last is not null && last.Content is { } lastContent)
        {
            var supportNotes=lastContent.Questions.Where(q=>last.QuestionSupports.TryGetValue(q.Id,out var tags) && tags.Count>0)
                .Select(q=>$"{q.Skill}: {string.Join(", ",last.QuestionSupports[q.Id])}").Distinct().ToArray();
            if(supportNotes.Length>0)note+=$" Support that helped by skill: {string.Join("; ",supportNotes)}. Offer opportunities to use these supports naturally.";
        }
        var s = new ReadingSession { ChildProfileId=p.Id, HouseholdId=householdId, ScheduleId=scheduleId, RequestId=requestId, SeriesId=seriesId, EpisodeNumber=episodeNumber, SeriesContext=seriesContext, CharacterGuidance=characterGuidance?.Trim()??"", TargetDate=date, Theme=(theme ?? p.Interests.Split(',')[0]).Trim(), Tone=tone ?? p.Tone, TargetWords=words ?? p.TargetWords, QuestionCount=questions ?? p.QuestionCount, PrimarySkill=skill, SecondarySkill=focusSkills.FirstOrDefault(x=>x!=skill)??p.Skills.FirstOrDefault(x=>x!=skill), AvoidTopics=p.AvoidTopics, ReadingLevel=p.ReadingLevel, AdaptationNote=note, VocabularyDifficulty=Math.Clamp(p.VocabularyDifficulty+shift,1,5), SentenceComplexity=Math.Clamp(p.SentenceComplexity+shift,1,5), PlotComplexity=Math.Clamp(p.PlotComplexity+shift,1,5), InferenceDifficulty=Math.Clamp(p.InferenceDifficulty+shift,1,5), MemoryLoad=Math.Clamp(p.MemoryLoad+shift,1,5) };
        if (s.TargetWords is < 150 or > 900 || s.QuestionCount is < 2 or > 8 || s.Theme.Length is < 1 or > 120 || !Catalog.Tones.Contains(s.Tone)) throw new ArgumentException("Check theme, tone, story length and question count.");
        db.Sessions.Add(s); await db.SaveChangesAsync(); return s;
    }
}

public class GenerationWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<GenerationWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000,stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Tick(stoppingToken); } catch (Exception ex) { log.LogError(ex,"Generation worker tick failed"); }
            await Task.Delay(TimeSpan.FromSeconds(3),stoppingToken);
        }
    }
    private async Task Tick(CancellationToken ct)
    {
        using var scope=scopes.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AppDb>();
        var id=await db.Sessions.Where(x=>x.JobStatus=="Queued").OrderBy(x=>x.CreatedAt).Select(x=>x.Id).FirstOrDefaultAsync(ct);
        if (id==Guid.Empty) return;
        if (await db.Sessions.Where(x=>x.Id==id && x.JobStatus=="Queued").ExecuteUpdateAsync(x=>x.SetProperty(y=>y.JobStatus,"Generating").SetProperty(y=>y.Attempts,y=>y.Attempts+1),ct)!=1) return;
        var s=await db.Sessions.SingleAsync(x=>x.Id==id,ct);
        var provider=string.Equals(config["GENERATION_PROVIDER"],"openrouter",StringComparison.OrdinalIgnoreCase) ? (IStoryProvider)scope.ServiceProvider.GetRequiredService<OpenRouterProvider>() : scope.ServiceProvider.GetRequiredService<DemoProvider>();
        try
        {
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(75));
            var content=await provider.Generate(s,timeout.Token); var errors=Rules.ContentErrors(s,content);
            if (errors.Count>0) throw new InvalidContentException(string.Join(" ",errors));
            s.ContentJson=JsonSerializer.Serialize(content,JsonOptions.Default); s.JobStatus="Ready"; s.Error=null; s.IsDemo=provider.IsDemo;
            if(s.SeriesId.HasValue && await db.Series.SingleOrDefaultAsync(x=>x.Id==s.SeriesId,ct) is { } series) { series.CharacterNotes=content.CharacterNotes; series.Recap=content.SeriesRecap; }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            var retry=ex is ProviderException pe && pe.Transient && s.Attempts<3 || ex is InvalidContentException && s.Attempts<2;
            s.JobStatus=retry ? "Queued":"Failed"; s.Error=ex is InvalidContentException ? "Generated story failed validation. Please retry or adjust the plan." : ex is ProviderException ? ex.Message : "Generation failed. Please retry.";
            log.LogWarning("Generation attempt {Attempt} failed for session {Id}: {Error}",s.Attempts,s.Id,s.Error);
        }
        await db.SaveChangesAsync(ct);
    }
}
public class InvalidContentException(string message) : Exception(message);

public class ScheduleWorker(IServiceScopeFactory scopes, ILogger<ScheduleWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Tick(stoppingToken); } catch (Exception ex) { log.LogError(ex,"Schedule tick failed"); }
            await Task.Delay(TimeSpan.FromMinutes(1),stoppingToken);
        }
    }
    public async Task Tick(CancellationToken ct=default)
    {
        using var scope=scopes.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<AppDb>(); var svc=scope.ServiceProvider.GetRequiredService<SessionService>();
        var now=DateTimeOffset.UtcNow;
        foreach(var schedule in await db.Schedules.Where(x=>x.Enabled).ToListAsync(ct))
        {
            TimeZoneInfo zone; TimeOnly time; try { zone=TimeZoneInfo.FindSystemTimeZoneById(schedule.Timezone); time=TimeOnly.Parse(schedule.LocalTime); } catch { continue; }
            var today=Rules.LocalToday(zone,now);
            for(var offset=-2;offset<=1;offset++)
            {
                var target=today.AddDays(offset); if(!schedule.Weekdays.Contains((int)target.DayOfWeek)) continue;
                var runDate=schedule.PrepareDayBefore ? target.AddDays(-1):target;
                var due=Rules.ResolveLocal(runDate,time,zone);
                if(due>now || now-due>TimeSpan.FromDays(2)) continue;
                if(await db.Sessions.AnyAsync(x=>x.ScheduleId==schedule.Id && x.TargetDate==target,ct)) continue;
                try { await svc.Create(schedule.HouseholdId,schedule.ChildProfileId,target,null,null,null,null,schedule.Id); }
                catch(DbUpdateException) { db.ChangeTracker.Clear(); } // Unique schedule/date index resolves concurrent ticks.
            }
        }
    }
}
