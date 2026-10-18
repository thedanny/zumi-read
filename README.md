# ZumiRead

**Stories they love. Understanding that grows.** A family reading app with a public sample story, parent accounts, child profiles, continuing story series, a paginated reader, parent guides, schedules, feedback, and history.

## Start with Docker

Requirements: Docker Desktop with Compose. From this directory:

```sh
docker compose up --build
```

Open **http://localhost:8080** for the landing page. Anyone can open **/sample.html** and read a complete sample, including its questions. Four saved Mira adventures rotate in order on each visit or reload. The landing preview shows the episode its sample link will open; samples make no provider calls. Creating a personalized story requires a free parent account at the landing page signup form. The account opens **/app.html**, where the parent adds a child profile first. The default `demo` provider needs no API key and generates fictional stories for testing. The web port is bound to `127.0.0.1` only. Stop with `Ctrl+C` or `docker compose down`. The PostgreSQL and authentication-key named volumes keep accounts and data across restarts. Do not use `docker compose down -v` if you want to keep them.

The API Dockerfile installs pinned .NET 10 SDK and ASP.NET runtime tarballs from Microsoft's release CDN into the .NET 8 base images. This keeps the build reproducible on machines where the .NET 10 container registry endpoint is unavailable; Docker still needs network access for the first build and NuGet restore.

To use [OpenRouter structured outputs](https://openrouter.ai/docs/guides/features/structured-outputs), copy `.env.example` to `.env`, then set:

```dotenv
GENERATION_PROVIDER=openrouter
OPENROUTER_API_KEY=your_key
OPENROUTER_MODEL=your/model-slug
```

Choose an OpenRouter model and endpoint that support `response_format: json_schema`. ZumiRead requests only compatible endpoints and validates the result again on the server. Keep the key in `.env`, which is ignored by Git. Restart with `docker compose up --build`. A locally running app still contacts OpenRouter in this mode. The generation request uses a fictional protagonist and excludes the child's name and parent notes. Avoid topics, parent-selected difficulty, a numeric prior story rating, and summarized skill feedback are sent. Check the parent preview before reading with a child.

## Development

Requirements: .NET 10 SDK, Node 20+, Docker. Start PostgreSQL with `docker compose up -d db`. In one terminal run:

```sh
dotnet run --project server --urls http://localhost:5080
```

In another:

```sh
cd client
npm ci
npm run dev
```

Open **http://localhost:5173**. Vite proxies `/api` to port 5080. The Compose database is exposed to the host on `localhost:5433`; database/user/password are `storysprout`. Override with `ConnectionStrings__Database`. For local development, set `GENERATION_PROVIDER`, `OPENROUTER_MODEL`, and `OPENROUTER_API_KEY` in the server environment if using OpenRouter. `npm run build` and `dotnet test tests` run the build and backend tests.

### Local demo login

The current local Docker database has a fictional demo family with a Mira profile, two ready episodes, and sample progress:

| Email | Password |
| --- | --- |
| `demo@zumiread.test` | `ReadTogether2026!` |

Sign in at **http://localhost:8080/app.html** (Docker) or **http://localhost:5173/app.html** (Vite). This account is for local development only. It persists with the current PostgreSQL volume, but a fresh database does not create it automatically; use the landing page signup form there.

The landing page, sample reader, and parent app are separate Vite entry points (`index.html`, `sample.html`, and `app.html`). The landing page can be hosted separately later if its `/api/auth/*` requests are routed to the same origin as the API. The parent app uses an HTTP-only signed cookie; the API checks household ownership and CSRF tokens for writes. The public sample is static and does not create a session or call OpenRouter.

## How sessions work

1. Create or edit a child profile. Reading level is a parent-selected starting point, not a professionally validated level. Advanced story settings are separate from age and grade. Optionally choose up to three **focus goals** from the skills to practice.
2. Create a session immediately, or set a weekly schedule. The plan fixes theme, length, skills, and difficulty before generation. Sessions are saved in PostgreSQL. Opening or printing them does not call a provider again.
3. Open the distraction-free child reader. On wide screens it opens as a two-page book; the reader can switch between **One page** and **Open book**, and that choice is remembered. It uses the open-source StPageFlip engine for a curved paper turn with a back face and moving shadows. Use Previous and Next, the arrow keys, or swipe left and right on a phone. Text is fitted into visible pages without a scrollbar; reduced-motion preferences use static pages. The parent guide contains a short summary, objectives, answer cues, hints, and linked passages. Print buttons produce separate layouts.
4. In the parent guide, the optional **How did [child] do on this story?** section offers one-tap choices for each question: **On their own**, **With a nudge**, or **Let's revisit**. A parent can also mark more than one optional **What helped?** note, such as rereading or talking it through. Answer ideas and story clues can be opened as needed. The optional reading feel, interest, story fit, and parent note controls are tucked under **Anything else you noticed?**. **Save reading moment** marks the session complete; a family can save without assessing any questions, or simply read stories without using progress features.
5. **History & progress** shows observation totals and a separate card for each selected focus goal, including recent observations. The next session prioritizes a focus goal whose latest observation was **Let's revisit**, then a goal that needed a nudge, then rotates the chosen goals. Without goals, the same approach applies to all practice skills. The latest completed session's reading feel adjusts each 1–5 story setting by at most one point within bounds. Story fit, interest, and what helped become compact planning guidance for the hosted provider. Parent free-text notes stay local. These are simple rules based on parent observations, not a validated adaptive curriculum. A parent can edit goals and profile settings at any time.

### Continuing episodes

In **New story**, choose **Start a series** to enter a title, premise, and optional fictional character behavior. Choose **Next episode** later, select that series, and enter a new adventure setting. The app requires the previous episode to be ready and a different setting. Each generated episode saves its own short summary and character notes. The series also keeps a compact cumulative recap; these notes and recent episode summaries are sent with the next OpenRouter generation request so the model can preserve established behavior and events. The parent guide displays the episode memory. Regeneration of an earlier episode is disabled after a later episode exists, because later stories rely on its canon. Demo mode uses sample Mira adventures to exercise this flow.

### Questions in the reader

In the **Parent guide → Child reader** controls, choose whether questions appear. They stay hidden while the child reads. On the final story page, the child can press **I'm ready for questions**. Questions then appear one at a time for discussion with a grown-up. An optional setting closes the story until **Finished questions** is pressed; the child story API withholds paragraphs during that stage, including after a refresh. This works on a shared child device too; a signed-in parent can still view the story and answers in the parent guide.

### Send a story to a child's device

Open a ready story and choose **Share to child device → Make a QR code & link**. A child can scan the QR code, open the copied invitation link, or visit **/read** and enter the eight-digit code. The invitation works once and expires after 15 minutes. Redeeming it gives that browser access to this story for 30 days; the parent can choose **Remove child-device access** at any time. Creating another invitation replaces only the unredeemed invitation. Regenerating the story removes existing reader access.

The child page shows only the book reader and any enabled child questions. It cannot open the parent guide, account, profiles, settings, or progress. The optional story lock during questions is enforced for shared readers as well. Access is held in a separate HTTP-only cookie scoped to the reader API. The invitation link contains a one-time secret, so treat it like a private link. The server stores hashes of codes and tokens and limits code redemption attempts. Parent and child pages must be served from the same reachable HTTPS origin in a deployed setup.

**Local development:** The default Compose port binds to `127.0.0.1`. A QR code or link using that address works only on the same computer; `127.0.0.1` on a phone means the phone itself. Cross-device testing requires a reachable HTTPS deployment or a separately configured local-network HTTPS setup. Do not sign the parent account in on the child's device.

Generation jobs move through **Queued → Generating → Ready** or **Failed**. The worker makes at most three attempts for transient provider errors and two for invalid content. It checks required fields, question count, story length within **±25%**, paragraph references, accepted types, and size limits. A failed job has a retry action. A provider call interrupted by a restart is marked Failed for an explicit retry so the app does not silently repeat a potentially paid request. Regeneration replaces the saved story and clears prior feedback. Validation cannot guarantee educational quality or suitability.

## Scheduling

Schedule weekdays are **reading dates** in the chosen IANA timezone. The local generation time is on the reading date, or on the preceding local date if “prepare one day before” is selected. A nonexistent spring daylight-saving time is moved to the next valid minute. A repeated fall time uses its first occurrence. The worker checks every minute while the app runs. On restart it catches up due sessions from at most the preceding **two days**, creating no older backlog. Unique database indexes on schedule/date and manual request ID, plus a conditional job claim, prevent duplicate work. Automatic sessions are created only while the app runs.

## Database and migrations

EF Core migrations live in `server/Migrations`. The API applies them on startup after PostgreSQL reports healthy; a migration failure stops the API. **Back up the database before upgrading** and review new migrations. To create a migration in development: `dotnet ef migrations add Name --project server --output-dir Migrations`. Do not use `EnsureCreated` against the persistent database.

The family-account migration preserves existing household and story rows. Since the old local app had no owner identity, existing local data is not automatically assigned to a newly registered email. Its household ID is `11111111-1111-1111-1111-111111111111`; an operator can transfer ownership to a specific account after verifying that account, or restore the pre-upgrade database backup. Keep the database backup private because it may contain story and profile data. Authentication keys in the `auth_keys` volume must also persist across restarts, or existing sign-in cookies become invalid.

The parent-observations migration adds optional focus goals and question support notes. Existing profiles start with no focus goals, and existing story feedback is retained.

PowerShell backup and restore example:

```powershell
docker compose exec -T db pg_dump -U storysprout -d storysprout --clean --if-exists | Set-Content -Encoding utf8 storysprout-backup.sql
Get-Content -Raw storysprout-backup.sql | docker compose exec -T db psql -U storysprout -d storysprout
```

Restore to a stopped API or fresh database to avoid concurrent writes. The restore command replaces database objects in that target. Test backups periodically.

## Limits and public hosting

Parent accounts and household-scoped API access are implemented, but email verification, password reset, login throttling, billing, quotas, account deletion/export, and production monitoring are not. The Compose setup remains local-only. Set up HTTPS, secure production secrets, backup automation, privacy controls, and abuse limits before public hosting. OpenRouter use may incur provider charges; model availability, strict schema support, and output quality vary. Series titles, premises, character notes, episode summaries, and cumulative recap are sent to OpenRouter for continuing stories. Avoid including real child names or private details in those fields. Printing uses browser print styles. The progress view counts observations and excludes unassessed questions; it is not a standardized score or diagnosis.

Story illustrations are planned for a later version. The current reader is text first.
