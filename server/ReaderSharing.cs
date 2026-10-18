using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace StorySprout.Api;

public static class ReaderSharing
{
    private static byte[]? codeKey;
    public const string CookieName = "zumi.reader";
    public static readonly TimeSpan InviteLifetime = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan AccessLifetime = TimeSpan.FromDays(30);

    public static string NewSecret() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
    public static string NewCode() => RandomNumberGenerator.GetInt32(100_000_000).ToString("D8");
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string CodeHash(string code) => Convert.ToHexString(HMACSHA256.HashData(codeKey ?? throw new InvalidOperationException("Reader code key has not been configured."), Encoding.UTF8.GetBytes(code)));

    public static void Configure(string keyPath)
    {
        var path = Path.Combine(keyPath, "reader-code.key");
        if (!File.Exists(path))
        {
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temporary, RandomNumberGenerator.GetBytes(32));
            try { File.Move(temporary, path); }
            catch (IOException) when (File.Exists(path)) { File.Delete(temporary); }
        }
        codeKey = File.ReadAllBytes(path);
        if (codeKey.Length != 32) throw new InvalidOperationException("The reader code key is invalid.");
    }

    public static void SetAccessCookie(HttpContext context, string secret)
    {
        context.Response.Cookies.Append(CookieName, secret, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Strict,
            Secure = context.Request.IsHttps || string.Equals(context.Request.Headers["X-Forwarded-Proto"].FirstOrDefault(), "https", StringComparison.OrdinalIgnoreCase),
            Path = "/api/reader",
            MaxAge = AccessLifetime
        });
    }

    public static async Task<ReadingSession?> GetSession(HttpContext context, AppDb db)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var secret) || string.IsNullOrWhiteSpace(secret)) return null;
        var now = DateTimeOffset.UtcNow;
        var hash = Hash(secret);
        var share = await db.ReaderShares.AsNoTracking().SingleOrDefaultAsync(x => x.AccessTokenHash == hash && x.RevokedAt == null && x.AccessExpiresAt > now);
        if (share is null) return null;
        return await db.Sessions.SingleOrDefaultAsync(x => x.Id == share.SessionId && x.HouseholdId == share.HouseholdId && x.JobStatus == "Ready");
    }

    public static object ChildPayload(ReadingSession session)
    {
        var content = session.Content!;
        var locked = session.ShowChildQuestions && session.LockStoryDuringQuestions && session.QuestionModeStarted && !session.QuestionsCompleted;
        return new
        {
            session.Id, session.TargetDate, content.Title,
            paragraphs = locked ? new List<StoryParagraph>() : content.Paragraphs,
            questions = session.ShowChildQuestions && session.QuestionModeStarted
                ? content.Questions.Select(question => new { question.Id, question.Text }).ToList() : [],
            questionCount = session.ShowChildQuestions ? content.Questions.Count : 0,
            session.ShowChildQuestions, session.LockStoryDuringQuestions, session.QuestionModeStarted,
            session.QuestionsCompleted, session.IsDemo, session.EpisodeNumber
        };
    }
}
