# Project Context (Reference)

This file provides background context for understanding the codebase. Unlike other files in this directory, this is **reference material** - not rules to actively apply.

## What This Application Does

**PredictionLeague** is a sports prediction league application where users:
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
| CQRS/Mediator | MediatR | 13.0.0 |
| Validation | FluentValidation | 12.0.0 |
| Guard Clauses | Ardalis.GuardClauses | 5.0.0 |
| Authentication | JWT + Google OAuth + ASP.NET Identity | 8.0 |
| Email | Brevo (Sendinblue) | 1.1.1 |
| Logging | Serilog + Datadog | 9.0.0 |
| Secrets | Azure Key Vault | 1.4.0 |

## Solution Structure

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

All scheduled endpoints protected by API key (`X-Api-Key` header).

### External API

- **Provider:** api-sports.io (Football API)
- **No fallback data** - App relies entirely on API availability

### Configuration

```json
{
  "JwtSettings": { "Secret", "Issuer", "Audience", "ExpiryMinutes" },
  "Brevo": { "ApiKey", "Templates": { "JoinLeagueRequest": 1 } },
  "FootballApi": { "ApiKey", "BaseUrl", "SchedulerApiKey" },
  "Authentication:Google": { "ClientId", "ClientSecret" }
}
```

Production uses Azure Key Vault with substitution syntax: `${Secret-Name}`

## Design Decisions

These are intentional trade-offs, not issues to fix:

1. **Dapper over EF Core** - Chosen for performance and explicit SQL control
2. **Blazor WASM** - Client-side rendering for responsiveness, tokens in localStorage
3. **MediatR** - Decouples controllers from business logic
4. **No unit tests yet** - Planned for future implementation
5. **Manual FTP deployment** - Hosting limitation

## Documentation Locations

| Topic | Location |
|-------|----------|
| Database schema | [`/docs/database-schema.md`](../database-schema.md) |
| CSS reference | [`/docs/css-reference.md`](../css-reference.md) |
| Security findings | [`/docs/plans/security/`](../plans/security/) |
| Feature plans | [`/docs/features/`](../features/) |
| Future roadmap | [`/docs/future-roadmap.md`](../future-roadmap.md) |

## Useful Commands

```bash
# Run the API
dotnet run --project PredictionLeague.API

# Run the Blazor client
dotnet run --project PredictionLeague.Web

# Build all projects
dotnet build PredictionLeague.sln
```
