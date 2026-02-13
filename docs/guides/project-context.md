# Project Context (Reference)

This file provides background context for understanding the codebase. Unlike other files in this directory, this is **reference material** - not rules to actively apply.

## What This Application Does

**The Predictions** is a sports prediction league application where users:
- Predict football match scores
- Compete in leagues with friends
- Win prizes based on prediction accuracy

## Technology Stack

| Category | Technology | Version |
|----------|-----------|---------|
| Platform | .NET | 8.0 |
| Backend | ASP.NET Core | 8.0 |
| Frontend | Blazor WebAssembly | 8.0 |
| Database | SQL Server | - |
| ORM | Dapper | 2.1.66 |
| CQRS/Mediator | MediatR | 14.0.0 |
| Validation | FluentValidation | 12.1.1 |
| Guard Clauses | Ardalis.GuardClauses | 5.0.0 |
| Authentication | JWT + Google OAuth + ASP.NET Identity | 8.0 |
| Email | Brevo (Sendinblue) | 1.1.1 |
| Logging | Serilog + Datadog | 8.0.3 |
| Secrets | Azure Key Vault | 1.4.0 |

## Solution Structure

```
src/
├── PredictionLeague.API              → REST controllers and endpoints
├── PredictionLeague.Application      → CQRS commands, queries, and handlers
├── PredictionLeague.Contracts        → DTOs shared between layers
├── PredictionLeague.Domain           → Core business entities and domain logic
├── PredictionLeague.Hosting.Shared   → Shared hosting configuration
├── PredictionLeague.Infrastructure   → Data access, repositories, external services
├── PredictionLeague.Validators       → FluentValidation validators
├── PredictionLeague.Web              → Blazor server host
└── PredictionLeague.Web.Client       → Blazor WebAssembly UI components
tests/                                → (planned)
tools/
└── ThePredictions.DatabaseTools      → Database refresh (dev) and backup (prod) tool
```

**Dependency Direction:** Presentation → Application → Domain (never reverse)

## Domain Concepts

### League

- Container for users competing together
- Has members, prize settings, entry code
- Administrator is automatically added as approved member on creation
- Entry codes are 6-character alphanumeric strings

### Round

- Represents a gameweek with multiple matches
- Status flow: `Draft` → `Published` → `InProgress` → `Completed`
- Has a deadline (`DeadlineUtc`) after which predictions are locked

### Match

- Individual fixture within a round
- Has home/away teams and scores
- Future: `CustomLockTimeUtc` for per-match deadlines (column exists, not yet used)

### Prediction

- User's predicted score for a match
- Can be edited unlimited times before deadline
- Scoring:
  - **Exact Score** - Predicted score matches actual score exactly
  - **Correct Result** - Predicted winner/draw matches actual result
- Points configurable per league (`PointsForExactScore`, `PointsForCorrectResult`)

### Prize Distribution

Four prize strategies:
- `RoundPrizeStrategy` - Weekly winners
- `MonthlyPrizeStrategy` - Monthly aggregate winners
- `OverallPrizeStrategy` - Season-end winners
- `MostExactScoresPrizeStrategy` - Most exact predictions

### Boosts

Power-ups that modify scoring:
- **Double Up** - Doubles all points for the round it's applied to
- Configured per league with usage limits and windows

## Infrastructure

### Environments

| Environment | Site URL | Database | Key Vault |
|-------------|----------|----------|-----------|
| Production | `https://www.thepredictions.co.uk` | `ThePredictions` | `the-predictions-prod` |
| Development | `https://dev.thepredictions.co.uk` | `ThePredictionsDev` | `the-predictions-dev` |
| Local | `https://localhost:7132` | Uses dev Key Vault | `the-predictions-dev` |

There is also a backup database (`ThePredictionsBackup`) which receives a daily unmodified copy of production data.

### Hosting

- **Provider:** Fasthosts shared hosting (both sites)
- **Access:** FTP only (no RDP, no server configuration)
- **Deployment:** Manual publish from Visual Studio, then upload via CuteFTP
- **FTP hostname:** `ftp.fasthosts.co.uk` for both sites

### Database Server

All databases are hosted on `mssql04.mssql.prositehosting.net` (Fasthosts SQL Server).

| Database | Purpose | Logins |
|----------|---------|--------|
| `ThePredictions` | Production | `AntonyWillson` (app), `Refresh` (read for backups/refresh) |
| `ThePredictionsDev` | Development | `AntonyWillsonDev` (app), `RefreshDev` (read for refresh) |
| `ThePredictionsBackup` | Daily backup of production | `PredictionBackup` (write) |

All database logins have database owner permissions (Fasthosts only supports owner or no access).

### Publish Profiles

The `PredictionLeague.Web` project has two publish profiles:

| Profile | Environment | Output Folder | Notes |
|---------|-------------|---------------|-------|
| `Publish to Production` | `Production` | `bin\Release\net8.0\publish\` | Excludes dev config files |
| `Publish to Development` | `Development` | `bin\Release\net8.0\publish-dev\` | Excludes prod config files |

Each profile uses `CopyToPublishDirectory="Never"` to exclude the other environment's `appsettings` and secrets files. The `EnvironmentName` property in each `.pubxml` controls the `ASPNETCORE_ENVIRONMENT` value written into the published `web.config`.

### Configuration Files

| File | Used by | Purpose |
|------|---------|---------|
| `appsettings.json` | All environments | Shared base config with Key Vault substitution placeholders |
| `appsettings.Local.json` | Local development | Localhost URLs, dev Key Vault URI |
| `appsettings.Development.json` | Hosted dev site | Dev site URLs, dev Key Vault URI |
| `appsettings.Production.json` | Live site | Production URLs, prod Key Vault URI |
| `appsettings.Production.Secrets.json` | Live site | Azure service principal credentials (not in source control) |
| `appsettings.Development.Secrets.json` | Hosted dev site | Azure service principal credentials (not in source control) |

The `*.Secrets.json` files contain Azure AD service principal credentials (`TenantId`, `ClientId`, `ClientSecret`) used to authenticate to Key Vault. They are gitignored and must be manually placed on each Fasthosts site.

`launchSettings.json` sets the environment to `Local` for Visual Studio debugging, which loads `appsettings.Local.json` and uses `DefaultAzureCredential` (Visual Studio sign-in) for Key Vault access.

### Azure Key Vault

Both Key Vaults use **RBAC (role-based access control)** for permissions.

Each environment has its own Key Vault and Azure AD App Registration (service principal):

| Environment | Key Vault | App Registration |
|-------------|-----------|------------------|
| Production | `the-predictions-prod` | `The Predictions (Prod)` |
| Development | `the-predictions-dev` | `The Predictions (Dev)` |

Each App Registration has the **Key Vault Secrets User** role on its respective Key Vault.

Secrets are referenced in `appsettings.json` using substitution syntax: `${Secret-Name}`. The `EnableSubstitutions()` call in `Program.cs` replaces these placeholders with actual Key Vault values at startup.

### Scheduled Jobs (via cron-job.org)

Production only. The development site does not have scheduled jobs.

| Job | Frequency | Endpoint |
|-----|-----------|----------|
| Publish Upcoming Rounds | Daily at 9am | `/api/tasks/publish-upcoming-rounds` |
| Send Email Reminders | Every 30 minutes | `/api/tasks/send-reminders` |
| Sync Season | Daily at 8am | `/api/tasks/sync-season` |
| Live Update Scores | Every minute | `/api/tasks/update-live-scores` |

All scheduled endpoints protected by API key (`X-Api-Key` header).

### GitHub Actions

| Workflow | File | Trigger | Purpose |
|----------|------|---------|---------|
| Refresh Dev Database | `refresh-dev-db.yml` | Manual only | Copies production data to dev with anonymisation |
| Backup Production Database | `backup-prod-db.yml` | Daily at 2am UTC + manual | Copies production data to backup (no anonymisation) |

Both workflows use `tools/ThePredictions.DatabaseTools/`. The **dev refresh** reads all tables from production, anonymises personal data (realistic fake names/emails via Bogus), creates test accounts (`testplayer@dev.local` and `testadmin@dev.local`), and writes to the dev database. The **production backup** copies all data unmodified to `ThePredictionsBackup` as a safety net independent of Fasthosts' own backup policy. Token tables (`AspNetUserTokens`, `RefreshTokens`, `PasswordResetTokens`) are excluded from both.

#### GitHub Secrets

| Secret | Used by | Description |
|--------|---------|-------------|
| `PROD_CONNECTION_STRING` | Both workflows | Reads from production using `Refresh` login |
| `DEV_CONNECTION_STRING` | Dev refresh | Writes to dev using `RefreshDev` login |
| `BACKUP_CONNECTION_STRING` | Prod backup | Writes to backup using `PredictionBackup` login |
| `TEST_ACCOUNT_PASSWORD` | Dev refresh | Password for test accounts created after anonymisation |

### External API

- **Provider:** api-sports.io (Football API)
- **No fallback data** — app relies entirely on API availability

## Season Sync Algorithm

The `SyncSeasonWithApiCommandHandler` synchronises match data from the external football API into the local database. It runs daily via a scheduled job and processes all fixtures for the season in a single pass. The handler is in `src/PredictionLeague.Application/Features/Admin/Seasons/Commands/SyncSeasonWithApiCommandHandler.cs`.

### Overview

The handler runs in 8 phases:

| Phase | Purpose |
|-------|---------|
| 0 | Load all data upfront (season, API rounds, API fixtures, DB rounds, teams) |
| 1 | Filter valid fixtures (skip any with missing teams, dates, or round names) |
| 2 | Calculate round date windows using gap-based boundaries |
| 3 | Allocate each fixture to a round window by date |
| 4 | Reconcile with existing database rounds (create, move, or update matches) |
| 5 | Delete stale matches (only those without user predictions) |
| 6 | Handle unplaceable fixtures (update date but leave in current round, log error) |
| 7 | Persist all changes (move matches first, then update rounds) |
| 8 | Publish/unpublish rounds based on updated start dates |

### Phase 2: Gap-Based Round Window Calculation

Round windows determine the date range each round "owns" for allocating fixtures. Rather than using fixed-width windows (e.g. 7 days), the algorithm calculates adaptive boundaries between rounds based on where the majority of each round's matches are scheduled.

**Step 1 — Calculate the median fixture date per API round**

For each API round (e.g. "Regular Season - 28"), the fixtures are sorted by date and the median is taken. The median is robust against rescheduled outliers: if a round has 8 matches on Saturday–Sunday and 2 rescheduled to a Wednesday three weeks later, the median still falls on the Saturday/Sunday cluster.

**Step 2 — Sort rounds chronologically by median date**

Rounds are sorted by their median date, with round number as a tiebreaker for rounds whose medians fall on the same date.

**Step 3 — Calculate boundaries as midpoints between consecutive medians**

For each pair of adjacent rounds, the boundary is the exact chronological midpoint between their two median dates. This means a rescheduled fixture is allocated to whichever round's median it is closer to in time.

- First round's window starts at `DateTime.MinValue`
- Last round's window ends at `DateTime.MaxValue`
- Each intermediate boundary = `median[i].Ticks + (median[i+1].Ticks - median[i].Ticks) / 2`

**Example: Normal weekend → midweek → weekend sequence**

| Round | Fixtures | Median | Window |
|-------|----------|--------|--------|
| 28 | Sat 28 Feb – Sun 1 Mar | ~Sat 28 Feb 17:30 | `MinValue` → ~Mon 2 Mar 06:37 |
| 29 | Tue 3 Mar – Thu 5 Mar | ~Tue 3 Mar 19:45 | ~Mon 2 Mar 06:37 → ~Fri 6 Mar 05:22 |
| 30 | Sat 7 Mar – Sun 8 Mar | ~Sat 7 Mar 15:00 | ~Fri 6 Mar 05:22 → `MaxValue` |

Midweek rounds naturally get narrow windows (~4 days). Weekend rounds get wider windows. No hardcoded day-of-week rules.

**Example: 3-week gap between rounds**

| Round | Median | Boundary after |
|-------|--------|---------------|
| 28 | Sat 28 Feb | midpoint → ~Sat 7 Mar |
| 29 | Sat 21 Mar | — |

A match rescheduled to Wed 4 Mar falls before the midpoint → allocated to Round 28. A match rescheduled to Wed 11 Mar falls after → allocated to Round 29. The midpoint boundary is equivalent to "which round's matches is this date closer to".

**Edge cases:**

| Scenario | Behaviour |
|----------|-----------|
| Single round in season | Window: `MinValue` → `MaxValue` |
| Round with 1 fixture | Median = that fixture's date; boundary calculation still works |
| Empty season (all rounds filtered out) | Empty window list; all fixtures become unplaceable |
| Two rounds with identical median dates | Round number tiebreaker ensures deterministic sort order |
| Rescheduled outliers within a round | Median ignores them; they may be allocated to an adjacent round's window |

### Phase 3: Fixture Allocation

Each fixture is placed into the first round window where `fixture.MatchDateTimeUtc >= WindowStart AND fixture.MatchDateTimeUtc < WindowEnd`. Fixtures that fall outside all windows (should not happen with `MinValue`/`MaxValue` bookends, but handled defensively) go to an `unplaceableFixtures` list.

This means a fixture labelled "Round 31" by the API but rescheduled to a date within Round 27's window will be allocated to Round 27 in the local database.

### Phase 4: Match Reconciliation

For each round window with fixtures, the handler finds or creates the corresponding database round, then for each fixture:

| Scenario | Action |
|----------|--------|
| Match exists in the correct round | Update date if changed |
| Match exists in a different round | `RemoveMatch` from source round, `AcceptMatch` into target round (preserves Match ID and linked UserPredictions) |
| Match is new | `AddMatch` to the round |

After processing all fixtures for a round, the round's `StartDateUtc` and `DeadlineUtc` are recalculated from the earliest match date (deadline = earliest match minus 30 minutes).

### Phase 5: Stale Match Deletion

Matches whose `ExternalId` no longer appears in the API response are candidates for deletion. Before deleting, the handler checks for linked `UserPredictions`:

- **No predictions**: match is removed from its round
- **Has predictions**: match is kept, a warning is logged

The `RoundRepository.UpdateAsync` delete SQL also has a safety net: `AND NOT EXISTS (SELECT 1 FROM [UserPredictions] up WHERE up.[MatchId] = [Matches].[Id])`.

### Phase 7: Safe Persistence Order

When a match moves between rounds, the source round's `UpdateAsync` would see the match as "deleted" if it runs before the target round claims it. To prevent this:

1. `MoveMatchesToRoundAsync` runs first — updates `[Matches].[RoundId]` directly in the database for all moved matches
2. Then `UpdateAsync` runs for each changed round — by this point, moved matches already have their new `RoundId`, so the source round's delete detection no longer sees them

This makes the save order of individual rounds completely irrelevant.

### Phase 8: Publish/Unpublish

After all rounds are saved, `PublishUpcomingRoundsCommand` is dispatched via MediatR. This:

- **Publishes** any Draft rounds whose `StartDateUtc` is within 28 days from now
- **Unpublishes** any Published rounds whose `StartDateUtc` has moved beyond 28 days from now (sets status back to Draft)

### Key Implementation Details for Testing

**`CalculateRoundWindows` method** — `private static`, takes a `List<RoundFixtureSummary>` (already sorted by `MedianDateUtc` then `RoundNumber`), returns `List<RoundWindow>`. This is a pure function with no dependencies, ideal for unit testing.

**`RoundFixtureSummary`** — private record: `(string ApiRoundName, int RoundNumber, DateTime MedianDateUtc)`

**`RoundWindow`** — private record: `(string ApiRoundName, int RoundNumber, DateTime WindowStart, DateTime WindowEnd)`

**`ValidFixture`** — private record: `(int ExternalId, DateTime MatchDateTimeUtc, int HomeTeamId, int AwayTeamId, string ApiRoundName)`

**Median calculation** — for a list of N fixtures sorted by date, the median is at index `N / 2` (integer division). For even-count lists this picks the first element of the upper half, which is acceptable since Premier League rounds typically have 10 matches and the cluster spans at most 2 days.

**Test scenarios for `CalculateRoundWindows`:**

1. Empty input → empty output
2. Single round → window from `MinValue` to `MaxValue`
3. Two consecutive weekend rounds → boundary falls mid-week between them
4. Weekend → midweek → weekend → midweek round gets a narrow window
5. 3-week gap between rounds → boundary at the halfway point
6. Many rounds (full 38-round season) → all windows are contiguous and non-overlapping
7. Two rounds with the same median date → round number tiebreaker determines order

**Test scenarios for fixture allocation (Phase 3):**

1. All fixtures fall neatly within their API round's window
2. A fixture rescheduled earlier lands in the previous round's window
3. A fixture rescheduled later lands in the next round's window
4. A fixture exactly on a boundary → falls into the later round (`>= WindowStart`, `< WindowEnd`)

**Test scenarios for match reconciliation (Phase 4):**

1. First sync — all matches are new, all rounds are created
2. No changes — matches exist with correct dates, nothing is modified
3. Match date updated — match stays in same round, date is updated
4. Match moved between rounds — `RemoveMatch` from source, `AcceptMatch` into target
5. Round start date recalculated after match dates change

**Test scenarios for stale match deletion (Phase 5):**

1. Match removed from API with no predictions → deleted
2. Match removed from API with predictions → kept, warning logged
3. No stale matches → nothing happens

**Test scenarios for persistence safety (Phase 7):**

1. Round gains a match and loses a different match in the same sync → `MoveMatchesToRoundAsync` runs before any `UpdateAsync`, preventing accidental deletion

## Design Decisions

These are intentional trade-offs, not issues to fix:

1. **Dapper over EF Core** — Chosen for performance and explicit SQL control
2. **Blazor WASM** — Client-side rendering for responsiveness, tokens in localStorage
3. **MediatR** — Decouples controllers from business logic
4. **No unit tests yet** — Planned for future implementation
5. **Manual FTP deployment** — Hosting limitation (CI/CD via GitHub Actions planned)
6. **Separate environments** — Local (localhost), Development (hosted dev site), Production

## Documentation Locations

| Topic | Location |
|-------|----------|
| Coding guides | [`/docs/guides/`](.) |
| Workflow checklists | [`/docs/guides/checklists/`](checklists/) |
| Database schema | [`/docs/guides/database-schema.md`](database-schema.md) |
| CSS reference | [`/docs/guides/css-reference.md`](css-reference.md) |
| Security accepted risks | [`/docs/security/accepted-risks.md`](../security/accepted-risks.md) |
| Security audit history | [`/docs/security/audit-history.md`](../security/audit-history.md) |
| Feature plans | [`/docs/todo/features/`](../todo/features/) |
| Architecture plans | [`/docs/todo/architecture/`](../todo/architecture/) |
| Security plans | [`/docs/todo/security/`](../todo/security/) |

## Useful Commands

```bash
# Run the API
dotnet run --project src/PredictionLeague.API

# Run the Blazor client
dotnet run --project src/PredictionLeague.Web

# Build all projects
dotnet build PredictionLeague.sln
```
