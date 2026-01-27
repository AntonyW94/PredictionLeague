# CLAUDE.md - Project Intelligence for PredictionLeague

This file captures the key patterns, decisions, and context for AI assistants working on this codebase.

## Project Overview

**PredictionLeague** is a sports prediction league application where users predict football match scores, compete in leagues, and win prizes based on prediction accuracy.

### Technology Stack

| Category | Technology | Version |
|----------|-----------|---------|
| Platform | .NET | 8.0 |
| Backend | ASP.NET Core | 8.0 |
| Frontend | Blazor WebAssembly | 8.0 |
| Database | SQL Server | - |
| ORM | Dapper | 2.1.66 |
| CQRS/Mediator | MediatR | 13.0.0 |
| Validation | FluentValidation | 12.0.0 |
| Guard Clauses | Ardalis.GuardClauses | 5.0.0 |
| Authentication | JWT + Google OAuth + ASP.NET Identity | 8.0 |
| Email | Brevo (Sendinblue) | 1.1.1 |
| Logging | Serilog + Datadog | 9.0.0 |
| Secrets | Azure Key Vault | 1.4.0 |

## Architecture

### Clean Architecture with CQRS

The solution follows Clean Architecture with strict layer separation:

```
PredictionLeague.Domain           → Core business entities and logic
PredictionLeague.Application      → CQRS commands/queries, interfaces
PredictionLeague.Infrastructure   → Data access, external services
PredictionLeague.API              → REST controllers
PredictionLeague.Web.Client       → Blazor WebAssembly UI
PredictionLeague.Contracts        → DTOs shared between layers
PredictionLeague.Validators       → FluentValidation validators
```

**Dependency Direction:** Presentation → Application → Domain (never reverse)

### Project-Specific Guidelines

Some projects have additional guidelines in their own CLAUDE.md files:

- [`PredictionLeague.API/CLAUDE.md`](PredictionLeague.API/CLAUDE.md) - API controllers, authentication, error handling
- [`PredictionLeague.Web.Client/CLAUDE.md`](PredictionLeague.Web.Client/CLAUDE.md) - Blazor state management, CSS architecture

### Key Patterns

1. **CQRS** - Commands modify state, Queries fetch data via MediatR
2. **Repository Pattern** - Used by Commands only, not Queries
3. **Factory Methods** - Domain entities use `Create()` static methods for construction
4. **Rich Domain Models** - Business logic lives in entities, not services
5. **Guard Clauses** - Input validation using Ardalis.GuardClauses
6. **Strategy Pattern** - Prize distribution via `IPrizeStrategy` implementations

### CQRS Data Access Rules

**Commands** use repositories (`IXxxRepository`) for data access:
- Repositories handle entity persistence and retrieval
- Domain models are returned from repositories

**Queries** use `IApplicationReadDbConnection` directly:
- Each query handler writes its own SQL
- Returns DTOs, not domain models
- Optimised for read performance
- Never use repositories in query handlers

```csharp
// CORRECT - Query using IApplicationReadDbConnection
public class GetMyLeaguesQueryHandler : IRequestHandler<GetMyLeaguesQuery, IEnumerable<MyLeagueDto>>
{
    private readonly IApplicationReadDbConnection _readDb;

    public async Task<IEnumerable<MyLeagueDto>> Handle(...)
    {
        const string sql = "SELECT ... FROM [Leagues] ...";
        return await _readDb.QueryAsync<MyLeagueDto>(sql, cancellationToken);
    }
}

// WRONG - Query using repository
public class GetMyLeaguesQueryHandler : IRequestHandler<GetMyLeaguesQuery, IEnumerable<MyLeagueDto>>
{
    private readonly ILeagueRepository _leagueRepository; // DON'T DO THIS
}
```

## Code Conventions

### Language and Spelling

**Use UK English spelling throughout the codebase:**

| US English | UK English (Use This) |
|------------|----------------------|
| color | colour |
| center | centre |
| organize | organise |
| favorite | favourite |
| license (verb) | licence |
| analyze | analyse |
| canceled | cancelled |

This applies to:
- File names (e.g., `colours.css` not `colors.css`)
- CSS class names (where practical)
- Comments and documentation
- Variable names and string literals
- Error messages and UI text

### Naming

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `LeagueRepository` |
| Methods | PascalCase | `GetByIdAsync` |
| Private fields | _camelCase | `_connectionFactory` |
| Async methods | Async suffix | `CreateAsync` |
| Commands | Command suffix | `CreateLeagueCommand` |
| Queries | Query suffix | `GetMyLeaguesQuery` |
| Handlers | Handler suffix | `CreateLeagueCommandHandler` |
| DTOs | Dto suffix | `LeagueDto` |

### File Organisation

**One public class or interface per file** - Each public class, record, or interface must be in its own file. Never define multiple public types in a single file.

```csharp
// CORRECT - Each type in its own file
// LeagueDto.cs
public record LeagueDto(int Id, string Name);

// LeagueMemberDto.cs
public record LeagueMemberDto(int Id, string UserId);

// WRONG - Multiple public types in one file
// LeagueDtos.cs
public record LeagueDto(int Id, string Name);
public record LeagueMemberDto(int Id, string UserId); // Should be in separate file
```

This applies to:
- Classes and records
- Interfaces
- All projects in the solution

### Code Formatting

**Always put statements on a new line after `if`** - Never put `return`, `continue`, `break`, or other statements on the same line as an `if`:

```csharp
// CORRECT
if (!userRow)
    return;

if (condition)
    continue;

// WRONG
if (!userRow) return;
if (condition) continue;
```

### DateTime Handling

- **All dates stored in UTC** in the database
- Property names use `Utc` suffix: `CreatedAtUtc`, `DeadlineUtc`
- Always use `DateTime.UtcNow`, never `DateTime.Now`
- Custom `DapperUtcDateTimeHandler` ensures UTC kind on deserialization

### Logging

**Entity ID format in log messages:** When referencing entities with IDs in log messages, use the format `EntityName (ID: {EntityNameId})`:

```csharp
// CORRECT
_logger.LogInformation("Processing Round (ID: {RoundId})", round.Id);
_logger.LogInformation("User (ID: {UserId}) joined League (ID: {LeagueId})", userId, leagueId);
_logger.LogWarning("Match (ID: {MatchId}) not found", matchId);

// WRONG
_logger.LogInformation("Processing Round {RoundId}", round.Id);
_logger.LogInformation("User {UserId} joined League {LeagueId}", userId, leagueId);
```

This format provides clear, consistent log messages that are easy to search and parse.

### Domain Model Construction

Domain entities have two construction paths:

1. **Factory method (`Create`)** - For new entities with business validation:
   ```csharp
   public static League Create(int seasonId, string name, ...)
   {
       Validate(...);
       return new League { ... };
   }
   ```

2. **Public constructor** - For hydration from database (no validation):
   ```csharp
   public League(int id, string name, ...)
   {
       Id = id;
       Name = name;
       // Direct assignment, no validation
   }
   ```

### Repository Pattern

Repositories return new instances after insert (immutability preserved):

```csharp
public async Task<League> CreateAsync(League league, CancellationToken ct)
{
    // INSERT and get new ID
    var newId = await Connection.ExecuteScalarAsync<int>(command);

    // Return NEW instance with ID set
    return new League(id: newId, name: league.Name, ...);
}
```

**Never use reflection to set `init` properties.**

## Project Structure

### Feature Organization (Application Layer)

Features are organized by domain area:

```
Features/
├── Authentication/Commands/Login, Register, Logout...
├── Dashboard/Queries/GetMyLeagues, GetLeaderboards...
├── Leagues/Commands/CreateLeague, JoinLeague...
├── Leagues/Queries/GetLeagueById, GetLeagueDashboard...
├── Predictions/Commands/SubmitPredictions
├── Admin/Rounds/Commands/CreateRound, UpdateMatchResults...
└── Admin/Seasons/Commands/CreateSeason...
```

### MediatR Pipeline Behaviors

1. **ValidationBehaviour** - Runs FluentValidation validators before handler
2. **TransactionBehaviour** - Wraps `ITransactionalRequest` in TransactionScope

## Key Domain Concepts

### League

- Has members, prize settings, entry code
- Administrator is automatically added as approved member on creation
- Entry codes are 6-character alphanumeric strings

### Round

- Represents a gameweek with multiple matches
- Status flow: Draft → Published → InProgress → Completed
- Contains matches with predictions

### Prediction Scoring

- **Exact Score** - Predicted score matches actual score exactly
- **Correct Result** - Predicted winner/draw matches actual result
- Points are configurable per league (PointsForExactScore, PointsForCorrectResult)

### Prize Distribution

Four prize strategies:
- `RoundPrizeStrategy` - Weekly winners
- `MonthlyPrizeStrategy` - Monthly aggregate winners
- `OverallPrizeStrategy` - Season-end winners
- `MostExactScoresPrizeStrategy` - Most exact predictions

## Database

### Schema Reference

**Full database schema documentation:** [`/docs/database-schema.md`](docs/database-schema.md)

This file contains all tables, columns, types, constraints, relationships, and common query examples.

### Key Tables

- `Leagues` - League definitions
- `LeagueMembers` - User membership in leagues
- `LeaguePrizeSettings` - Prize configuration per league
- `LeagueRoundResults` - Cached scoring results per member per round
- `Rounds` - Gameweeks
- `Matches` - Individual fixtures
- `UserPredictions` - User predictions for matches
- `RoundResults` - Aggregated round results per user
- `Winnings` - Prize payouts
- `AspNetUsers` - Identity users (extended with FirstName, LastName)

### SQL Conventions

- Table names: `[PascalCase]` with brackets
- Column names: `[PascalCase]` with brackets
- All queries use parameterized commands via Dapper
- Complex aggregations use CTEs

## Configuration

### Settings Sections

```json
{
  "JwtSettings": { "Secret", "Issuer", "Audience", "ExpiryMinutes" },
  "Brevo": { "ApiKey", "Templates": { "JoinLeagueRequest": 1 } },
  "FootballApi": { "ApiKey", "BaseUrl", "SchedulerApiKey" },
  "Authentication:Google": { "ClientId", "ClientSecret" }
}
```

Production uses Azure Key Vault with substitution syntax: `${Secret-Name}`

## Deployment & Infrastructure

### Hosting
- **Provider:** Fasthosts shared hosting
- **Access:** FTP only (no RDP, no server configuration)
- **Deployment:** Manual via CuteFTP
- **Environments:** Production only (no staging)

### Scheduled Jobs (via cron-job.org)
| Job | Frequency | Endpoint |
|-----|-----------|----------|
| Publish Upcoming Rounds | Daily at 9am | `/api/tasks/publish-upcoming-rounds` |
| Send Email Reminders | Every 30 minutes | `/api/tasks/send-reminders` |
| Sync Season | Daily at 8am | `/api/tasks/sync-season` |
| Live Update Scores | Every minute | `/api/tasks/update-live-scores` |

All scheduled endpoints are protected by API key (`X-Api-Key` header).

### External API
- **Provider:** api-sports.io (Football API)
- **No fallback data** - App relies entirely on API availability
- **Future consideration:** Add caching/fallback for API outages

### CSS Bundling and Cache Busting

CSS files are bundled and versioned during `dotnet publish` to optimise production performance.

**Development:** Uses separate CSS files with `@import` statements in `app.css` for easy debugging.

**Production:** An MSBuild target in `PredictionLeague.Web.csproj` runs after Publish and:
1. Concatenates all CSS files into a single `app.css` (in specified order)
2. Prepends Google Fonts import
3. Deletes individual CSS files and subdirectories
4. Adds cache busting version to `index.html`: `app.css?v=TIMESTAMP`

**Location:** `PredictionLeague.Web.csproj` - Target `BundleCssAndAddCacheBusting`

**Adding new CSS files:**
When adding a new CSS file, you must update TWO places:
1. `PredictionLeague.Web.Client/wwwroot/css/app.css` - Add `@import` for development
2. `PredictionLeague.Web.csproj` - Add to `<CssFilesToBundle>` ItemGroup (in correct load order)

**Verifying the bundle:**
```bash
dotnet publish PredictionLeague.Web/PredictionLeague.Web -c Release -o ./publish-test
```
Check that:
- `./publish-test/wwwroot/css/app.css` contains all CSS concatenated
- `./publish-test/wwwroot/css/` has no subdirectories
- `./publish-test/wwwroot/index.html` has `app.css?v=TIMESTAMP`

## Boosts System

### Current Implementation
- **Double Up** - Doubles all points for the round it's applied to

### Configuration (Per League)
- Total uses per season (e.g., 3 uses total)
- Window-based limits (e.g., Window 1: Rounds 1-19 with 2 uses, Window 2: Rounds 20-38 with 2 uses)

### Key Components
- `BoostDefinition` - Defines boost types
- `BoostEligibilityEvaluator` - Determines if user can apply boost
- `IBoostService` - Applies boost to predictions

## Prediction Rules

### Editing
- Users can edit predictions unlimited times before deadline
- No background job at deadline - enforcement is in UI and API validation

### Deadlines
- **Current:** One deadline per round (`Round.DeadlineUtc`)
- **Future:** Per-match deadlines via `Match.CustomLockTimeUtc` (column exists, not yet used)
- **Use case:** FIFA World Cup 2026 knockout rounds where matches have different kick-off times

## Known Decisions & Trade-offs

1. **Dapper over EF Core** - Chosen for performance and explicit SQL control
2. **Blazor WASM** - Client-side rendering for responsiveness, tokens in localStorage (security trade-off)
3. **MediatR** - Decouples controllers from business logic
4. **No unit tests yet** - Planned for future implementation
5. **Manual FTP deployment** - Hosting limitation, automated CI/CD desired for future

## Feature Planning

**Feature plans location:** [`/docs/features/`](docs/features/)

Detailed implementation plans for new features are stored here. Each feature has:
- `README.md` - Overview, acceptance criteria, and task list
- Numbered task files (`01-xxx.md`, `02-xxx.md`) - Step-by-step implementation details

To work on a planned feature, read the feature's README.md first, then work through tasks in order.

**Templates:** [`/docs/features/_template/`](docs/features/_template/) contains templates for creating new feature plans.

## Future Roadmap

**Full roadmap:** [`/docs/future-roadmap.md`](docs/future-roadmap.md)

## Things to Avoid

1. **Never use reflection to set entity properties** - Use constructors instead
2. **Never use `DateTime.Now`** - Always use `DateTime.UtcNow`
3. **Never bypass factory methods for new entities** - They contain validation
4. **Never add features without checking existing patterns** - Maintain consistency
5. **Never commit secrets to appsettings.json** - Use KeyVault references
6. **Never use repositories in Query handlers** - Use `IApplicationReadDbConnection` with custom SQL instead

## Useful Commands

```bash
# Run the API
dotnet run --project PredictionLeague.API

# Run the Blazor client
dotnet run --project PredictionLeague.Web

# Build all projects
dotnet build PredictionLeague.sln
```
