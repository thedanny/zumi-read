using System.Text.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;
using StorySprout.Api;

var builder=WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDb>(o=>o.UseNpgsql(builder.Configuration.GetConnectionString("Database") ?? "Host=localhost;Port=5433;Database=storysprout;Username=storysprout;Password=storysprout"));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPasswordHasher<FamilyAccount>, PasswordHasher<FamilyAccount>>();
var keyPath=builder.Configuration["AUTH_KEY_PATH"] ?? Path.Combine(builder.Environment.ContentRootPath,".auth-keys");
Directory.CreateDirectory(keyPath);
ReaderSharing.Configure(keyPath);
builder.Services.AddDataProtection().SetApplicationName("ZumiRead").PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options=>
{
    options.Cookie.Name="zumi.auth";
    options.Cookie.HttpOnly=true;
    options.Cookie.SameSite=SameSiteMode.Lax;
    options.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan=TimeSpan.FromDays(14);
    options.SlidingExpiration=true;
    options.Events.OnRedirectToLogin=context=>{context.Response.StatusCode=StatusCodes.Status401Unauthorized;return Task.CompletedTask;};
    options.Events.OnRedirectToAccessDenied=context=>{context.Response.StatusCode=StatusCodes.Status403Forbidden;return Task.CompletedTask;};
});
builder.Services.AddAuthorization(options=>options.FallbackPolicy=new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddAntiforgery(options=>{options.HeaderName="X-CSRF-TOKEN";options.Cookie.Name="zumi.csrf";options.Cookie.SameSite=SameSiteMode.Strict;options.Cookie.SecurePolicy=CookieSecurePolicy.SameAsRequest;});
builder.Services.AddRateLimiter(options=>
{
    options.RejectionStatusCode=StatusCodes.Status429TooManyRequests;
    options.AddPolicy("reader-redeem",context=>RateLimitPartition.GetFixedWindowLimiter(
        context.Request.Headers["X-Real-IP"].FirstOrDefault() ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _=>new FixedWindowRateLimiterOptions { PermitLimit=8, Window=TimeSpan.FromMinutes(1), QueueLimit=0 }));
});
builder.Services.AddScoped<SessionService>(); builder.Services.AddSingleton<DemoProvider>();
builder.Services.AddHttpClient<OpenRouterProvider>(c=>c.Timeout=TimeSpan.FromSeconds(80));
builder.Services.AddHostedService<GenerationWorker>(); builder.Services.AddHostedService<ScheduleWorker>();
builder.Services.ConfigureHttpJsonOptions(o=>o.SerializerOptions.PropertyNamingPolicy=JsonNamingPolicy.CamelCase);
var app=builder.Build();
var contextAccessor=app.Services.GetRequiredService<IHttpContextAccessor>();
var currentFamily=new CurrentFamily(contextAccessor);
using(var scope=app.Services.CreateScope())
{
    var db=scope.ServiceProvider.GetRequiredService<AppDb>();
    await db.Database.MigrateAsync();
    // An interrupted provider call is not replayed automatically after a restart.
    await db.Sessions.Where(x=>x.JobStatus=="Generating").ExecuteUpdateAsync(x=>x.SetProperty(y=>y.JobStatus,"Failed").SetProperty(y=>y.Error,"Generation was interrupted by a restart. Retry this session when ready."));
}
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.Use(async(context,next)=>
{
    if(context.Request.Path.StartsWithSegments("/api") &&
       (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method)))
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch(AntiforgeryValidationException)
        {
            context.Response.StatusCode=StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error="Refresh the page and try again." });
            return;
        }
    }
    await next();
});
app.UseAuthorization();
app.MapGet("/api/health",()=>Results.Ok(new { status="ok" })).AllowAnonymous();
app.MapGet("/api/auth/csrf",(HttpContext context,IAntiforgery antiforgery)=>Results.Ok(new { token=antiforgery.GetAndStoreTokens(context).RequestToken })).AllowAnonymous();
app.MapPost("/api/auth/signup",async(SignupRequest input,AppDb db,IPasswordHasher<FamilyAccount> hasher,HttpContext context)=>
{
    if(!string.IsNullOrEmpty(input.Website))return Results.BadRequest(new { error="Please try again." });
    var normalized=FamilyAuth.NormalizeEmail(input.Email);
    if(normalized is null)return Results.BadRequest(new { error="Enter a valid email address." });
    if(input.Password is null || input.Password.Length is < 10 or > 128)return Results.BadRequest(new { error="Use a password between 10 and 128 characters." });
    if(await db.Accounts.AnyAsync(x=>x.NormalizedEmail==normalized))return Results.Conflict(new { error="This email already has an account. Sign in instead." });
    var household=new Household { Id=Guid.NewGuid() };
    var account=new FamilyAccount { HouseholdId=household.Id,Email=input.Email.Trim(),NormalizedEmail=normalized };
    account.PasswordHash=hasher.HashPassword(account,input.Password);
    db.Households.Add(household);db.Accounts.Add(account);
    try { await db.SaveChangesAsync(); }
    catch(DbUpdateException) { return Results.Conflict(new { error="This email already has an account. Sign in instead." }); }
    await FamilyAuth.SignIn(context,account);
    return Results.Ok(new { account.Email });
}).AllowAnonymous();
app.MapPost("/api/auth/login",async(LoginRequest input,AppDb db,IPasswordHasher<FamilyAccount> hasher,HttpContext context)=>
{
    var normalized=FamilyAuth.NormalizeEmail(input.Email);
    var account=normalized is null?null:await db.Accounts.SingleOrDefaultAsync(x=>x.NormalizedEmail==normalized);
    if(account is null || string.IsNullOrEmpty(input.Password) || hasher.VerifyHashedPassword(account,account.PasswordHash,input.Password)==PasswordVerificationResult.Failed)
        return Results.Unauthorized();
    await FamilyAuth.SignIn(context,account);
    return Results.Ok(new { account.Email });
}).AllowAnonymous();
app.MapGet("/api/auth/me",(HttpContext context)=>Results.Ok(new { email=context.User.FindFirstValue(ClaimTypes.Email) }));
app.MapPost("/api/auth/logout",async(HttpContext context)=>{await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);return Results.NoContent();});
app.MapGet("/api/catalog",()=>Results.Ok(new { Catalog.Skills,Catalog.Tones,Catalog.Supports,Catalog.Outcomes }));
app.MapGet("/api/settings",async(AppDb db,IConfiguration cfg)=>Results.Ok(new { household=await db.Households.SingleAsync(x=>x.Id==currentFamily.Id), provider=string.Equals(cfg["GENERATION_PROVIDER"],"openrouter",StringComparison.OrdinalIgnoreCase)?"OpenRouter":"Demo", providerReady=string.Equals(cfg["GENERATION_PROVIDER"],"openrouter",StringComparison.OrdinalIgnoreCase)?!string.IsNullOrWhiteSpace(cfg["OPENROUTER_API_KEY"])&&!string.IsNullOrWhiteSpace(cfg["OPENROUTER_MODEL"]):true, model=string.Equals(cfg["GENERATION_PROVIDER"],"openrouter",StringComparison.OrdinalIgnoreCase)?cfg["OPENROUTER_MODEL"]:null }));
app.MapPut("/api/settings",async(Household input,AppDb db)=>
{
    if(input.Name.Length is < 1 or > 80 || !ValidZone(input.Timezone)) return Results.BadRequest(new { error="Enter a household name and valid IANA timezone." });
    var h=await db.Households.SingleAsync(x=>x.Id==currentFamily.Id); h.Name=input.Name; h.Timezone=input.Timezone; await db.SaveChangesAsync(); return Results.Ok(h);
});
app.MapGet("/api/profiles",async(AppDb db)=>await db.Profiles.Where(x=>x.HouseholdId==currentFamily.Id).OrderBy(x=>x.CreatedAt).ToListAsync());
app.MapPost("/api/profiles",async(ChildProfile input,AppDb db)=>
{
    var errors=Rules.ProfileErrors(input); if(errors.Count>0)return Results.BadRequest(new { errors });
    input.Id=Guid.NewGuid(); input.HouseholdId=currentFamily.Id; input.CreatedAt=DateTimeOffset.UtcNow; db.Profiles.Add(input); await db.SaveChangesAsync(); return Results.Created($"/api/profiles/{input.Id}",input);
});
app.MapPut("/api/profiles/{id:guid}",async(Guid id,ChildProfile input,AppDb db)=>
{
    var errors=Rules.ProfileErrors(input); if(errors.Count>0)return Results.BadRequest(new { errors });
    var p=await db.Profiles.SingleOrDefaultAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id); if(p is null)return Results.NotFound();
    p.Name=input.Name;p.Age=input.Age;p.Grade=input.Grade;p.ReadingLevel=input.ReadingLevel;p.Interests=input.Interests;p.Tone=input.Tone;p.TargetWords=input.TargetWords;p.QuestionCount=input.QuestionCount;p.Skills=input.Skills;p.GoalSkills=input.GoalSkills;p.AvoidTopics=input.AvoidTopics;p.ReadingSupport=input.ReadingSupport;p.VocabularyDifficulty=input.VocabularyDifficulty;p.SentenceComplexity=input.SentenceComplexity;p.PlotComplexity=input.PlotComplexity;p.InferenceDifficulty=input.InferenceDifficulty;p.MemoryLoad=input.MemoryLoad;
    await db.SaveChangesAsync();return Results.Ok(p);
});
app.MapDelete("/api/profiles/{id:guid}",async(Guid id,AppDb db)=>
{
    var p=await db.Profiles.SingleOrDefaultAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id);if(p is null)return Results.NotFound();
    if(await db.Sessions.AnyAsync(x=>x.ChildProfileId==id)) return Results.Conflict(new { error="This profile has session history and cannot be deleted." });
    db.Series.RemoveRange(await db.Series.Where(x=>x.ChildProfileId==id && x.HouseholdId==currentFamily.Id).ToListAsync());
    db.Profiles.Remove(p);await db.SaveChangesAsync();return Results.NoContent();
});
app.MapGet("/api/series",async(Guid? childId,AppDb db)=>
{
    var q=db.Series.Where(x=>x.HouseholdId==currentFamily.Id);
    if(childId.HasValue)q=q.Where(x=>x.ChildProfileId==childId);
    return await q.OrderByDescending(x=>x.CreatedAt).ToListAsync();
});
app.MapPost("/api/series",async(CreateSeriesRequest input,AppDb db)=>
{
    if(!await db.Profiles.AnyAsync(x=>x.Id==input.ChildProfileId && x.HouseholdId==currentFamily.Id))return Results.NotFound();
    if(string.IsNullOrWhiteSpace(input.Title) || input.Title.Length>100 || string.IsNullOrWhiteSpace(input.Premise) || input.Premise.Length>600 || input.CharacterNotes?.Length>800)return Results.BadRequest(new {error="Add a series title and premise; keep character notes brief."});
    var series=new StorySeries {ChildProfileId=input.ChildProfileId,HouseholdId=currentFamily.Id,Title=input.Title.Trim(),Premise=input.Premise.Trim(),CharacterNotes=input.CharacterNotes?.Trim()??""};
    db.Series.Add(series);await db.SaveChangesAsync();return Results.Created($"/api/series/{series.Id}",series);
});
app.MapPost("/api/sessions",async(CreateSessionRequest request,SessionService service,AppDb db)=>
{
    if(!await db.Profiles.AnyAsync(x=>x.Id==request.ChildProfileId && x.HouseholdId==currentFamily.Id))return Results.NotFound();
    if(request.CharacterGuidance?.Length>500)return Results.BadRequest(new {error="Keep character ideas under 500 characters."});
    try { var s=await service.Create(currentFamily.Id,request.ChildProfileId,request.TargetDate ?? DateOnly.FromDateTime(DateTime.Today),request.Theme,request.Tone,request.TargetWords,request.QuestionCount,null,request.RequestId,request.SeriesId,request.CharacterGuidance);return Results.Created($"/api/sessions/{s.Id}",s); }
    catch(ArgumentException ex){return Results.BadRequest(new { error=ex.Message });}
    catch(DbUpdateException) when(request.RequestId.HasValue)
    {
        db.ChangeTracker.Clear();
        var existing=await db.Sessions.SingleOrDefaultAsync(x=>x.HouseholdId==currentFamily.Id && x.RequestId==request.RequestId);
        return existing is null ? Results.Conflict(new { error="Session creation conflicted. Please retry." }):Results.Ok(existing);
    }
});
app.MapGet("/api/sessions",async(Guid? childId,string? status,string? skill,DateOnly? from,DateOnly? to,AppDb db)=>
{
    var q=db.Sessions.Include(x=>x.ChildProfile).Where(x=>x.HouseholdId==currentFamily.Id);
    if(childId.HasValue)q=q.Where(x=>x.ChildProfileId==childId.Value);
    if(!string.IsNullOrWhiteSpace(status))q=q.Where(x=>x.SessionStatus==status);
    if(!string.IsNullOrWhiteSpace(skill))q=q.Where(x=>x.PrimarySkill==skill);
    if(from.HasValue)q=q.Where(x=>x.TargetDate>=from.Value);
    if(to.HasValue)q=q.Where(x=>x.TargetDate<=to.Value);
    return await q.OrderByDescending(x=>x.TargetDate).ThenByDescending(x=>x.CreatedAt).Take(250).ToListAsync();
});
app.MapGet("/api/sessions/{id:guid}",async(Guid id,AppDb db)=>await db.Sessions.Include(x=>x.ChildProfile).SingleOrDefaultAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id) is { } s ? Results.Ok(s):Results.NotFound());
app.MapGet("/api/sessions/{id:guid}/child",async(Guid id,AppDb db)=>
{
    var s=await db.Sessions.SingleOrDefaultAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id);var c=s?.Content;
    if(c is null || s is null)return Results.NotFound();
    return Results.Ok(ReaderSharing.ChildPayload(s));
});
app.MapPut("/api/sessions/{id:guid}/reader-settings",async(Guid id,ReaderSettingsRequest input,AppDb db)=>
{
    var s=await db.Sessions.SingleOrDefaultAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id);if(s is null)return Results.NotFound();
    if(s.QuestionModeStarted && !s.QuestionsCompleted)return Results.Conflict(new {error="Finish the current questions before changing reader settings."});
    s.ShowChildQuestions=input.ShowChildQuestions;s.LockStoryDuringQuestions=input.ShowChildQuestions && input.LockStoryDuringQuestions;s.QuestionModeStarted=false;s.QuestionsCompleted=false;
    await db.SaveChangesAsync();return Results.Ok(s);
});
app.MapPost("/api/sessions/{id:guid}/reader/start-questions",async(Guid id,AppDb db)=>
{
    var s=await db.Sessions.SingleOrDefaultAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id);if(s is null)return Results.NotFound();
    if(s.JobStatus!="Ready" || !s.ShowChildQuestions)return Results.Conflict(new {error="Questions are not enabled for this story."});
    if(!s.QuestionModeStarted){s.QuestionModeStarted=true;await db.SaveChangesAsync();}
    return Results.Ok(new {s.QuestionModeStarted,s.QuestionsCompleted});
});
app.MapPost("/api/sessions/{id:guid}/reader/finish-questions",async(Guid id,AppDb db)=>
{
    var s=await db.Sessions.SingleOrDefaultAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id);if(s is null)return Results.NotFound();
    if(!s.QuestionModeStarted)return Results.Conflict(new {error="Start questions first."});
    if(!s.QuestionsCompleted){s.QuestionsCompleted=true;await db.SaveChangesAsync();}
    return Results.Ok(new {s.QuestionModeStarted,s.QuestionsCompleted});
});
app.MapGet("/api/sessions/{id:guid}/reader-share",async(Guid id,AppDb db)=>
{
    if(!await db.Sessions.AnyAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id))return Results.NotFound();
    var now=DateTimeOffset.UtcNow;
    var shares=await db.ReaderShares.Where(x=>x.SessionId==id && x.HouseholdId==currentFamily.Id && x.RevokedAt==null).ToListAsync();
    return Results.Ok(new { activeReaders=shares.Count(x=>x.AccessTokenHash!=null && x.AccessExpiresAt>now), invitationPending=shares.Any(x=>x.RedeemedAt==null && x.ExpiresAt>now) });
});
app.MapPost("/api/sessions/{id:guid}/reader-share",async(Guid id,AppDb db,HttpContext context)=>
{
    var session=await db.Sessions.SingleOrDefaultAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id);
    if(session is null)return Results.NotFound();
    if(session.JobStatus!="Ready" || session.Content is null)return Results.Conflict(new {error="Wait until the story is ready to share it."});
    var now=DateTimeOffset.UtcNow;
    await db.ReaderShares.Where(x=>x.SessionId==id && x.HouseholdId==currentFamily.Id && x.RedeemedAt==null && x.RevokedAt==null)
        .ExecuteUpdateAsync(update=>update.SetProperty(x=>x.RevokedAt,(DateTimeOffset?)now));
    string code;
    do { code=ReaderSharing.NewCode(); }
    while(await db.ReaderShares.AnyAsync(x=>x.CodeHash==ReaderSharing.CodeHash(code)));
    var token=ReaderSharing.NewSecret();
    var share=new ReaderShare { HouseholdId=currentFamily.Id,SessionId=id,CodeHash=ReaderSharing.CodeHash(code),LinkTokenHash=ReaderSharing.Hash(token),CreatedAt=now,ExpiresAt=now+ReaderSharing.InviteLifetime };
    db.ReaderShares.Add(share);await db.SaveChangesAsync();
    context.Response.Headers.CacheControl="no-store";
    return Results.Ok(new { code,token,share.ExpiresAt });
});
app.MapPost("/api/sessions/{id:guid}/reader-share/revoke",async(Guid id,AppDb db)=>
{
    if(!await db.Sessions.AnyAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id))return Results.NotFound();
    var now=DateTimeOffset.UtcNow;
    await db.ReaderShares.Where(x=>x.SessionId==id && x.HouseholdId==currentFamily.Id && x.RevokedAt==null)
        .ExecuteUpdateAsync(update=>update.SetProperty(x=>x.RevokedAt,(DateTimeOffset?)now));
    return Results.Ok(new { activeReaders=0,invitationPending=false });
});
app.MapPost("/api/reader/redeem",async(ReaderRedeemRequest input,AppDb db,HttpContext context)=>
{
    var code=input.Code?.Replace(" ","").Replace("-","");
    var hasCode=code is { Length:8 } && code.All(char.IsAsciiDigit);
    var hasToken=input.Token is { Length:43 };
    if(hasCode==hasToken)return Results.BadRequest(new {error="Enter the eight-digit code or open a fresh story link."});
    var linkHash=hasToken?ReaderSharing.Hash(input.Token!):null;
    var now=DateTimeOffset.UtcNow;
    var share=hasCode
        ? await db.ReaderShares.AsNoTracking().SingleOrDefaultAsync(x=>x.CodeHash==ReaderSharing.CodeHash(code!))
        : await db.ReaderShares.AsNoTracking().SingleOrDefaultAsync(x=>x.LinkTokenHash==linkHash);
    if(share is null || share.RevokedAt!=null || share.RedeemedAt!=null || share.ExpiresAt<=now)
        return Results.BadRequest(new {error="That code or link has expired or was already used. Ask a grown-up for a new one."});
    var access=ReaderSharing.NewSecret();
    var accessExpires=now+ReaderSharing.AccessLifetime;
    var changed=await db.ReaderShares.Where(x=>x.Id==share.Id && x.RevokedAt==null && x.RedeemedAt==null && x.ExpiresAt>now)
        .ExecuteUpdateAsync(update=>update.SetProperty(x=>x.RedeemedAt,(DateTimeOffset?)now)
            .SetProperty(x=>x.AccessTokenHash,ReaderSharing.Hash(access))
            .SetProperty(x=>x.AccessExpiresAt,(DateTimeOffset?)accessExpires));
    if(changed!=1)return Results.BadRequest(new {error="That code or link was already used. Ask a grown-up for a new one."});
    ReaderSharing.SetAccessCookie(context,access);
    context.Response.Headers.CacheControl="no-store";
    return Results.Ok(new { ready=true });
}).AllowAnonymous().RequireRateLimiting("reader-redeem");
app.MapGet("/api/reader/story",async(AppDb db,HttpContext context)=>
{
    var session=await ReaderSharing.GetSession(context,db);
    if(session?.Content is null)return Results.Unauthorized();
    context.Response.Headers.CacheControl="no-store";
    return Results.Ok(ReaderSharing.ChildPayload(session));
}).AllowAnonymous();
app.MapPost("/api/reader/start-questions",async(AppDb db,HttpContext context)=>
{
    var session=await ReaderSharing.GetSession(context,db);
    if(session is null)return Results.Unauthorized();
    if(!session.ShowChildQuestions)return Results.Conflict(new {error="Questions are not enabled for this story."});
    if(!session.QuestionModeStarted){session.QuestionModeStarted=true;await db.SaveChangesAsync();}
    return Results.Ok(new {session.QuestionModeStarted,session.QuestionsCompleted});
}).AllowAnonymous();
app.MapPost("/api/reader/finish-questions",async(AppDb db,HttpContext context)=>
{
    var session=await ReaderSharing.GetSession(context,db);
    if(session is null)return Results.Unauthorized();
    if(!session.QuestionModeStarted)return Results.Conflict(new {error="Start questions first."});
    if(!session.QuestionsCompleted){session.QuestionsCompleted=true;await db.SaveChangesAsync();}
    return Results.Ok(new {session.QuestionModeStarted,session.QuestionsCompleted});
}).AllowAnonymous();
app.MapPost("/api/sessions/{id:guid}/retry",async(Guid id,AppDb db)=>
{
    var s=await db.Sessions.SingleOrDefaultAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id);if(s is null)return Results.NotFound();
    if(s.JobStatus is "Generating" or "Queued")return Results.Ok(s);
    if(s.SeriesId.HasValue && await db.Sessions.AnyAsync(x=>x.SeriesId==s.SeriesId && x.EpisodeNumber>s.EpisodeNumber))return Results.Conflict(new {error="A later episode depends on this story; it cannot be regenerated."});
    await db.ReaderShares.Where(x=>x.SessionId==id && x.HouseholdId==currentFamily.Id && x.RevokedAt==null)
        .ExecuteUpdateAsync(update=>update.SetProperty(x=>x.RevokedAt,(DateTimeOffset?)DateTimeOffset.UtcNow));
    s.JobStatus="Queued";s.Error=null;s.ContentJson=null;s.Attempts=0;s.Feedback=new();s.QuestionSupports=new();s.ReadingDifficulty=null;s.InterestRating=null;s.StoryRating=null;s.ParentNotes=null;s.SessionStatus="Ready";s.CompletedAt=null;s.QuestionModeStarted=false;s.QuestionsCompleted=false;await db.SaveChangesAsync();return Results.Ok(s);
});
app.MapPut("/api/sessions/{id:guid}/feedback",async(Guid id,FeedbackRequest input,AppDb db)=>
{
    var s=await db.Sessions.SingleOrDefaultAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id);if(s is null)return Results.NotFound();
    if(s.JobStatus!="Ready")return Results.Conflict(new { error="Wait until the story is ready." });
    var validIds=s.Content!.Questions.Select(q=>q.Id).ToHashSet();
    var supports=input.QuestionSupports ?? [];
    if(!new[]{"Ready","In progress","Completed","Skipped"}.Contains(input.SessionStatus) || input.Feedback.Any(x=>!validIds.Contains(x.Key)||!Catalog.Outcomes.Contains(x.Value)) || supports.Any(x=>!validIds.Contains(x.Key)||x.Value is null||x.Value.Count>Catalog.QuestionSupports.Length||x.Value.Distinct().Count()!=x.Value.Count||x.Value.Any(value=>!Catalog.QuestionSupports.Contains(value))) || input.ReadingDifficulty is not (null or "Too easy" or "About right" or "Too difficult") || input.InterestRating is not (null or "Loved it" or "Okay" or "Did not enjoy it") || input.StoryRating is < 1 or > 5 || input.ParentNotes?.Length>2000) return Results.BadRequest(new { error="Check the reading notes and try again." });
    s.SessionStatus=input.SessionStatus;s.Feedback=input.Feedback;s.QuestionSupports=supports;s.ReadingDifficulty=input.ReadingDifficulty;s.InterestRating=input.InterestRating;s.StoryRating=input.StoryRating;s.ParentNotes=input.ParentNotes;
    if(s.SessionStatus=="In progress" && s.StartedAt is null)s.StartedAt=DateTimeOffset.UtcNow;
    if(s.SessionStatus=="Completed" && s.CompletedAt is null)s.CompletedAt=DateTimeOffset.UtcNow;
    await db.SaveChangesAsync();return Results.Ok(s);
});
app.MapGet("/api/schedules",async(AppDb db)=>await db.Schedules.Where(x=>x.HouseholdId==currentFamily.Id).ToListAsync());
app.MapPost("/api/schedules",async(ReadingSchedule input,AppDb db)=>
{
    var error=ScheduleError(input);if(error is not null)return Results.BadRequest(new { error });
    if(!await db.Profiles.AnyAsync(x=>x.Id==input.ChildProfileId && x.HouseholdId==currentFamily.Id))return Results.NotFound();
    input.Id=Guid.NewGuid();input.HouseholdId=currentFamily.Id;db.Schedules.Add(input);await db.SaveChangesAsync();return Results.Created($"/api/schedules/{input.Id}",input);
});
app.MapPut("/api/schedules/{id:guid}",async(Guid id,ReadingSchedule input,AppDb db)=>
{
    var error=ScheduleError(input);if(error is not null)return Results.BadRequest(new { error });
    var s=await db.Schedules.SingleOrDefaultAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id);if(s is null)return Results.NotFound();
    if(!await db.Profiles.AnyAsync(x=>x.Id==input.ChildProfileId && x.HouseholdId==currentFamily.Id))return Results.BadRequest(new { error="Choose a child profile." });
    s.ChildProfileId=input.ChildProfileId;s.Weekdays=input.Weekdays;s.LocalTime=input.LocalTime;s.Timezone=input.Timezone;s.Enabled=input.Enabled;s.PrepareDayBefore=input.PrepareDayBefore;await db.SaveChangesAsync();return Results.Ok(s);
});
app.MapDelete("/api/schedules/{id:guid}",async(Guid id,AppDb db)=>{var s=await db.Schedules.SingleOrDefaultAsync(x=>x.Id==id && x.HouseholdId==currentFamily.Id);if(s is null)return Results.NotFound();db.Schedules.Remove(s);await db.SaveChangesAsync();return Results.NoContent();});
app.MapGet("/api/progress",async(Guid? childId,AppDb db)=>
{
    var q=db.Sessions.Where(x=>x.HouseholdId==currentFamily.Id && x.SessionStatus=="Completed");if(childId.HasValue)q=q.Where(x=>x.ChildProfileId==childId.Value);
    var sessions=await q.OrderByDescending(x=>x.CompletedAt).ToListAsync();
    var counts=new Dictionary<string,Dictionary<string,int>>();var words=0;var assessed=0;
    foreach(var s in sessions){var c=s.Content;if(c is null)continue;words+=System.Text.RegularExpressions.Regex.Matches(string.Join(" ",c.Paragraphs.Select(p=>p.Text)),@"\b[\p{L}\p{N}']+\b").Count;foreach(var question in c.Questions){if(!s.Feedback.TryGetValue(question.Id,out var outcome)||outcome=="Not assessed")continue;assessed++;if(!counts.ContainsKey(question.Skill))counts[question.Skill]=new();counts[question.Skill][outcome]=counts[question.Skill].GetValueOrDefault(outcome)+1;}}
    var profiles=await db.Profiles.Where(x=>x.HouseholdId==currentFamily.Id && (!childId.HasValue || x.Id==childId.Value)).ToListAsync();
    var goals=profiles.SelectMany(profile=>profile.GoalSkills.Select(skill=>
    {
        var observations=sessions.Where(s=>s.ChildProfileId==profile.Id).SelectMany(s=>s.Content?.Questions
            .Where(question=>question.Skill==skill && s.Feedback.TryGetValue(question.Id,out var outcome) && outcome!="Not assessed")
            .Select(question=>s.Feedback[question.Id]) ?? []).ToList();
        return new {childId=profile.Id,childName=profile.Name,skill,independent=observations.Count(x=>x=="Independent"),together=observations.Count(x=>x=="Hint"),revisit=observations.Count(x=>x=="Needs practice"),recent=observations.Take(5).ToArray()};
    })).ToList();
    return Results.Ok(new { completedSessions=sessions.Count,wordsRead=words,questionsAssessed=assessed,bySkill=counts,goals,recentFeedback=sessions.Take(5).Select(s=>new { s.Id,s.TargetDate,s.ReadingDifficulty,s.InterestRating }) });
});
app.Run();

static bool ValidZone(string id){try { _=TimeZoneInfo.FindSystemTimeZoneById(id);return id.Contains('/'); }catch{return false;}}
static string? ScheduleError(ReadingSchedule s){if(s.Weekdays.Count==0||s.Weekdays.Any(x=>x is < 0 or > 6))return "Select at least one weekday.";if(!TimeOnly.TryParse(s.LocalTime,out _))return "Enter a valid local time.";if(!ValidZone(s.Timezone))return "Enter a valid IANA timezone.";return null;}
public record CreateSessionRequest(Guid ChildProfileId,DateOnly? TargetDate,string? Theme,string? Tone,int? TargetWords,int? QuestionCount,Guid? RequestId,Guid? SeriesId,string? CharacterGuidance);
public record CreateSeriesRequest(Guid ChildProfileId,string Title,string Premise,string? CharacterNotes);
public record ReaderSettingsRequest(bool ShowChildQuestions,bool LockStoryDuringQuestions);
public record FeedbackRequest(string SessionStatus,Dictionary<string,string> Feedback,string? ReadingDifficulty,string? InterestRating,int? StoryRating,string? ParentNotes,Dictionary<string,List<string>>? QuestionSupports=null);
public record ReaderRedeemRequest(string? Code,string? Token);
public partial class Program { }
