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

### DateTime Handling

- **All dates stored in UTC** in the database
- Property names use `Utc` suffix: `CreatedAtUtc`, `DeadlineUtc`
- Always use `DateTime.UtcNow`, never `DateTime.Now`
- Custom `DapperUtcDateTimeHandler` ensures UTC kind on deserialization

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

## API Structure

### Controller Organization

```
/api/auth           → Authentication (login, register, refresh)
/api/account        → User profile
/api/dashboard      → Dashboard data
/api/leagues        → League CRUD and membership
/api/predictions    → Prediction submission
/api/rounds         → Round queries
/api/admin/rounds   → Admin round management
/api/admin/seasons  → Admin season management
/api/tasks          → Background job triggers (API key protected)
```

### Authentication

- JWT Bearer tokens with 60-minute expiry
- Refresh tokens stored in HTTP-only cookies (7-day expiry)
- Google OAuth for social login
- API key authentication for scheduled tasks (`X-Api-Key` header)

## Client-Side (Blazor)

### State Management

Services hold state and notify components via events:

```csharp
public class DashboardStateService
{
    public event Action? OnStateChange;

    public async Task LoadMyLeaguesAsync()
    {
        // Load data...
        OnStateChange?.Invoke();
    }
}
```

### Authentication Flow

1. `ApiAuthenticationStateProvider` checks localStorage for `accessToken`
2. Validates JWT expiration
3. Auto-refreshes expired tokens via `/api/auth/refresh-token`
4. Sets `Authorization: Bearer {token}` header on HttpClient

## Database

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

## Error Handling

### ErrorHandlingMiddleware

Maps exceptions to HTTP status codes:

| Exception Type | Status Code |
|---------------|-------------|
| `KeyNotFoundException`, `EntityNotFoundException` | 404 |
| `ArgumentException`, `InvalidOperationException` | 400 |
| `ValidationException` (FluentValidation) | 400 |
| `UnauthorizedAccessException` | 401 |
| Other exceptions | 500 |

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

## Future Roadmap

### High Priority
- **UI Overhaul** - Main pages are styled, admin pages are broken by CSS changes
- **Code consistency audit** - Ensure all code follows same patterns and standards
- **Dashboard predictions** - Show predicted scores in upcoming rounds tile

### Medium Priority
- **Historic leaderboard snapshots** - Show leaderboards as they were at end of each round
- **Additional boost types** - Need icon generation solution
- **Trophy cabinet** - Achievement system (e.g., "75% correct in one round")
- **Detailed stats pages** - Performance graphs and analytics

### Lower Priority (Future)
- **Tournament seasons** - Different format for FIFA World Cup 2026 (March/April 2026)
- **Subscription payments** - Yearly sign-up fees
- **WhatsApp reminders** - Requires business registration (Sole Trader/Limited Company)
- **Mobile apps** - Google Play Store, iOS App Store, Apple Sign-In
- **Automated deployments** - CI/CD within Fasthosts limitations

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
