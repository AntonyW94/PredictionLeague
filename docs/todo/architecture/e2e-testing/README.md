# End-to-End Tests With Playwright - Stage 2

## Status

Not Started | **In Progress** | Complete

**Stage 1 shipped, August 2026.** `tests/E2E/ThePredictions.Web.Tests.E2E` drives five browser journeys
against deployed dev, run by `.github/workflows/e2e-dev.yml` after every dev deploy and reported to
`#github`. See [`docs/guides/testing.md`](../../../guides/testing.md#end-to-end-smoke-tests-against-deployed-dev)
for how it works. **Stage 2 is what remains, and it is by some distance the larger half.**

## Priority

**High.** Two things put it here:

1. **The remaining backlog is mostly UX.** Prediction history, statistics dashboard, notifications UI,
   head-to-head, season recap, profiles, search and the historical round view are all user journeys. Every
   one built without a test is debt nobody goes back for.
2. **A production incident on 2026-07-30 that nothing caught.** A leaderboard query's `SELECT` had drifted
   from its result record, so the page threw for a real user at 07:04 UTC with 1,647 green unit tests.
   Stage 1's leaderboard journey now covers that specific page. Stage 2 is what covers the rest.

## What Stage 1 delivered, and the ceiling it hit

Five journeys, structural assertions only, no seeder: the player's dashboard, opening a league and rendering
a leaderboard, the new-sign-up onboarding takeover, an admin screen, and logging out. Nothing in it is
throwaway - the project, page objects, browser fixture and CI wiring all carry over. Only the base URL
changes.

Two limitations were accepted at Stage 1 and are Stage 2's job to lift:

| Limitation | Why it bites |
|------------|--------------|
| **It does not gate the deploy** | It reports after the fact, so a broken build is live for as long as it takes someone to read Slack |
| **It cannot test flows needing controlled state** | Submitting a prediction needs a round whose deadline has not passed; admin flows need rounds in specific states; prize flows need completed results. None of that is reliable against data that shifts on every refresh |

The second is the real trigger. **"E2E every UI feature" cannot be done against deployed dev**, so the
container and seeder below are *prerequisites* for retrospective per-feature journeys, not an optional later
upgrade.

One Stage 1 finding is worth carrying forward: the two original dev test accounts had **no Season Pass**, and
`get-pass` is a required onboarding step, so every account sat in the onboarding takeover and the dashboard
never rendered a league or a leaderboard at all. `TestAccountCreator` now grants a pass, and a third
pass-less account keeps the onboarding state deliberately. A seeder needs the same care: **a fixture that
looks complete but leaves a required step outstanding produces a page that is not the page under test.**

---

## Stage 2 - Isolated stack with a seeded database

Reach for this now that Stage 1's ceiling bites, and you know which three or four scenarios the seeder
actually has to produce.

### Shape of a single run

1. SQL Server **service container** (`mcr.microsoft.com/mssql/server:2022-latest`) starts on the runner.
   Developer edition is free for test use.
2. **DbUp builds the schema** from `src/ThePredictions.Persistence.SqlServer/Migrations/` - the same scripts
   production runs. Most projects hand-maintain a test schema; this one does not have to. The integration
   suite already does exactly this, so the mechanics are proven (`Harness/MigrationRunner.cs`).
3. Seeder populates data (see below).
4. `dotnet run` the Web project against that connection string, in the background.
5. Playwright drives it - the same project Stage 1 built, pointed at a different `E2E_BASE_URL`.
6. Job ends; everything vanishes.

### Do NOT containerise the website

This was considered and rejected. A GitHub Actions runner is already an ephemeral, isolated environment that
is destroyed after the run - the teardown is free. A Dockerfile would add an image to maintain, a build on
every run, and **Linux-compatibility work that is not otherwise needed**: `ThePredictions.Infrastructure`
references `SkiaSharp` and `Svg.Skia` with **no** `SkiaSharp.NativeAssets.Linux` package. On Windows/IIS the
native assets ship in the base package; in a Linux container the share-card and badge-PNG rendering would
fail at runtime until that package and font dependencies were added. Real work, zero test value.

Docker is needed for **SQL Server only**, because there is no other way to get one onto an Ubuntu runner.

(If the site is ever moved off shared hosting, containerising becomes worthwhile for its own reasons - and
would lift the APM ceiling in `../apm-integration/README.md`. That is a hosting decision, not a testing one.)

### Seeding strategy - split by whether state is derived

Neither pure approach is right.

**Seed directly (SQL):** teams, competitions, users, Identity roles, league membership, **Season Passes**. No
derived state, stable shape, and genuinely more reliable this way. This is most of the rows.

**Seed through the application's own endpoints:** anything that triggers computation - chiefly posting
results.

The reason is concrete. `LeagueStatsRepository` is **371 lines containing 11 window-function rankings** that
populate `SnapshotOverallRank`, `SnapshotMonthRank`, `StageRank`, `PreRoundStageRank` and
`PreRoundExactScoresRank`. Since the ranks cache shipped (ADR-0015), `GetMyLeaguesQueryHandler` reads those
columns rather than computing live:

```sql
LEFT JOIN [LeagueMemberStats] stats ON l.[LeagueId] = stats.[LeagueId] AND l.[UserId] = stats.[UserId]
```

A seeder that inserts results directly therefore produces a dashboard with **no ranks at all**, and tests
fail against correct application code. Avoiding that means either driving the real write path or
**maintaining a second implementation of the ranking algorithm inside the test project**, which would
silently drift and produce plausible-but-wrong failures that take hours to trace.

Endpoints available for the derived parts:

```text
POST /api/admin/seasons/create
POST /api/admin/rounds/create
PUT  /api/admin/rounds/{roundId}/results   <- runs the real write path, populates the stats cache
```

### The objection to API seeding, and how it is handled

Seeding through the system under test is circular: one broken write endpoint turns the whole suite red with a
single cause, where direct seeding would have failed only the test for that endpoint.

That is a genuine cost, but the trade is not symmetric - a drifted duplicate of a 371-line ranking algorithm
is worse than a loud setup failure. Two cheap mitigations make it a reporting problem rather than a
diagnostic one:

1. **Setup fails loudly and aborts the run** with the offending call named, e.g.
   `SEEDING FAILED: PUT /api/admin/rounds/12/results returned 500` - never twenty mystery downstream
   failures.
2. **A preconditions test runs first**, exercising only the endpoints seeding depends on. Red there means
   setup, not the feature under test.

Worth noting where the risk actually lands: the failure this codebase experienced was a **read** (`SELECT`
drift), while the schema moved twice in one week (`0005`, `0006`). API seeding puts the risk on write
endpoints that have been stable; direct seeding puts it on schema knowledge that demonstrably changes.

### Time is the binding constraint

**Every seeded date must be computed relative to `DateTime.UtcNow` at seed time.**

- **`UtcNow`, never `Now`.** The SQL calls `GETUTCDATE()` in **22 places**, and the UK is UTC+1 for half the
  year. Seeding "deadline in one hour" with local time makes the database see two hours, so a test passes in
  January and fails in July. The root `CLAUDE.md` already bans `DateTime.Now`; it matters doubly here.
- **The clock cannot be pinned.** Those `GETUTCDATE()` calls are the *database's* clock, so registering a
  fixed `IDateTimeProvider` in the app does not affect them. Time-travel testing is off the table; relative
  offsets are the only option.
- Hard-coded dates in fixtures pass on the day they are written and fail silently forever after.

### Canonical round-state fixture

One season exercising nearly every branch, all relative to `UtcNow`:

| Round | Start | Deadline | Status | Purpose |
|-------|-------|----------|--------|---------|
| 1 | -14d | -14d | Completed, with results | leaderboards, stats cache |
| 2 | -1h | -1h | InProgress | live scoring |
| 3 | +2d | +2d | Published | the open prediction window |
| 4 | +9d | +9d | Draft | must NOT be visible |

### Seeding belongs in setup, not in tests

A tempting design is "one test creates the season, another adds rounds". Do not do this. xUnit gives no
ordering guarantee and runs classes in parallel; one failure cascades into every downstream test, and a
single failing test cannot be re-run in isolation.

The *sequence* is right, its home is wrong: it belongs in fixture code (`IAsyncLifetime` on a class or
collection fixture). "Can an admin create a season?" still exists as a test - it just runs alongside setup
rather than being depended upon.

### Isolation - decide before writing tests

If every test shares one league, a test that submits a prediction breaks a test asserting none exist. Either
run the suite serially, or give each test class its own season and league. The second costs more setup time
but is far more robust, and with API-driven seeding it is cheap enough to be worth it.

Stage 1 runs serially for a different reason - three fixed shared accounts - so the collection definition in
`Harness/BrowserCollection.cs` will need revisiting rather than reusing as-is.

---

## Known blockers and frictions

| Friction | Severity | Notes |
|----------|----------|-------|
| Seeder design, especially relative time | **High** - the real cost | Where most E2E suites rot |
| Blazor WebAssembly load times | Medium | Stage 1 settled on 90s navigation and 45s assertion timeouts against shared hosting; a local container should be quicker, but do not tighten them on faith |
| Platform divergence (prod Windows/IIS, runner Linux) | Low-Medium | Fine for journeys; **do not E2E the share-card image** - font rendering differs |
| Key Vault configuration | Low | `Program.cs` reads Key Vault when `KeyVaultUri` is set; the test run must supply config directly |
| `TestAccountCreator` does not transfer | Low | It reads `SELECT TOP 1 ... FROM [Leagues]` to find a league and a season to attach a pass to, so it presupposes a production copy. Useless on an empty database - the seeder must create the equivalent three account states itself |

## Correction to the existing test-suite plan

`../test-suite/README.md` rates *Query Handler Integration Tests* as feasible on a **SQLite in-memory
database**. That premise no longer holds - the queries are heavily T-SQL specific:

| Construct | Uses | SQLite |
|-----------|------|--------|
| `ISNULL(` | 73 | no |
| `SELECT TOP` | 29 | no |
| `GETUTCDATE()` | 22 | no |
| `CAST(... AS bit)` | 19 | no (no `bit` type) |
| `OUTER APPLY` | 17 | no |
| `SET TRANSACTION ISOLATION` | 4 | no |

They would not run, let alone pass. That tier has since shipped against a real SQL Server container
(`tests/Integration/ThePredictions.Persistence.SqlServer.Tests.Integration`), which is the same Testcontainers
approach Stage 2 needs - reuse `Harness/MigrationRunner.cs` rather than writing a second schema builder.

## Open questions

- [ ] Serial suite, or a season and league per test class?
- [ ] Does Stage 2 gate the dev deploy, now that it can run before anything ships?
- [ ] Which three or four scenarios does the seeder actually have to produce? Stage 1's ceiling names the
      candidates: submitting a prediction, entering results as an admin, and a completed round's prize flow.
