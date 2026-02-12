# CLAUDE.md - PredictionLeague

This file contains the critical rules for AI assistants. **Read this entire file before making any changes.**

## Critical Rules (ALWAYS Apply)

These rules are non-negotiable. Violating them will cause issues.

### Code Style

| Rule | Example |
|------|---------|
| **UK English spelling** | `colour`, `organise`, `favourite` (NOT `color`, `organize`, `favorite`) |
| **One public type per file** | `LeagueDto.cs` contains only `LeagueDto` |
| **Statements on new line after `if`** | `if (x)\n    return;` (NOT `if (x) return;`) |
| **`DateTime.UtcNow` only** | NEVER use `DateTime.Now` |
| **DateTime properties use `Utc` suffix** | `CreatedAtUtc`, `DeadlineUtc` |

### Database Schema Documentation

Any database changes (new tables, new columns, modified constraints, new indexes) **must** be reflected in [`docs/guides/database-schema.md`](docs/guides/database-schema.md). This file is the single source of truth for the database schema.

Any new tables must also be added to the database refresh tool in [`tools/ThePredictions.DatabaseTools/`](tools/ThePredictions.DatabaseTools/):
- Add the table to the correct position in the `TableCopyOrder` or `TablesToSkip` arrays in `DatabaseRefresher.cs` (respecting foreign key dependencies)
- If the table contains personal data, add anonymisation rules to `DataAnonymiser.cs`
- If the table contains sensitive tokens, add verification to `PersonalDataVerifier.cs`

### CQRS Data Access

| Operation | Use | NEVER Use |
|-----------|-----|-----------|
| **Commands** (write) | Repositories (`IXxxRepository`) | `IApplicationReadDbConnection` |
| **Queries** (read) | `IApplicationReadDbConnection` + SQL | Repositories |

```csharp
// CORRECT - Query handler
public class GetLeaguesQueryHandler
{
    private readonly IApplicationReadDbConnection _readDb;  // YES
}

// WRONG - Query handler with repository
public class GetLeaguesQueryHandler
{
    private readonly ILeagueRepository _repo;  // NO - queries don't use repositories
}
```

### Domain Models

| Creating new entity | Loading from database |
|--------------------|-----------------------|
| Use `Entity.Create(...)` factory method | Use public constructor |
| Validation runs | No validation (data already valid) |

```csharp
// New entity - use factory
var league = League.Create(seasonId, name, userId);

// From database - Dapper uses constructor automatically
```

### SQL Conventions

| Rule | Example |
|------|---------|
| **Brackets around table and column names** | `[Leagues]`, `[SeasonId]` |
| **PascalCase** | `[CreatedAtUtc]` (NOT `[created_at_utc]`) |
| **Always use table aliases** (no brackets on aliases) | `[Leagues] l` |
| **One column per line** in SELECT, INSERT, UPDATE | See below |
| **Each keyword on its own line**, next line indented | `SELECT`, `FROM`, `WHERE`, `AND`, `ORDER BY`, etc. |
| **Parameterised queries** | `WHERE l.[Id] = @Id` |

```sql
-- CORRECT
SELECT
    l.[Id],
    l.[Name]
FROM
    [Leagues] l
WHERE
    l.[SeasonId] = @SeasonId
    AND l.[Status] = @Status

-- WRONG: No brackets, no alias, columns on one line, wrong case
SELECT Id, name FROM Leagues WHERE season_id = @seasonId
```

### Logging Format

```csharp
// CORRECT: "EntityName (ID: {EntityNameId})"
_logger.LogInformation("League (ID: {LeagueId}) created", league.Id);

// WRONG: Missing "ID:" label
_logger.LogInformation("League {LeagueId} created", league.Id);
```

## Detailed Guidelines

For comprehensive rules with examples, consult these files:

| Topic | File |
|-------|------|
| UK English, naming, formatting, DateTime | [`docs/guides/code-style.md`](docs/guides/code-style.md) |
| Commands, queries, MediatR patterns | [`docs/guides/cqrs-patterns.md`](docs/guides/cqrs-patterns.md) |
| Entity construction, repositories, immutability | [`docs/guides/domain-models.md`](docs/guides/domain-models.md) |
| SQL conventions, Dapper patterns | [`docs/guides/database.md`](docs/guides/database.md) |
| Log message formatting | [`docs/guides/logging.md`](docs/guides/logging.md) |
| Domain concepts, tech stack, infrastructure | [`docs/guides/project-context.md`](docs/guides/project-context.md) |

## Project-Specific Guidelines

| Project | File |
|---------|------|
| API controllers, authentication, error handling | [`src/PredictionLeague.API/CLAUDE.md`](src/PredictionLeague.API/CLAUDE.md) |
| Blazor components, state management, CSS | [`src/PredictionLeague.Web.Client/CLAUDE.md`](src/PredictionLeague.Web.Client/CLAUDE.md) |

## Workflow Checklists

Use these when creating new features:

| Task | Checklist |
|------|-----------|
| Creating a new command | [`docs/guides/checklists/new-command.md`](docs/guides/checklists/new-command.md) |
| Creating a new query | [`docs/guides/checklists/new-query.md`](docs/guides/checklists/new-query.md) |
| Creating a new domain entity | [`docs/guides/checklists/new-entity.md`](docs/guides/checklists/new-entity.md) |
| Adding a new API endpoint | [`docs/guides/checklists/new-api-endpoint.md`](docs/guides/checklists/new-api-endpoint.md) |
| Creating a new Blazor component | [`docs/guides/checklists/new-blazor-component.md`](docs/guides/checklists/new-blazor-component.md) |
| Adding a new CSS file | [`docs/guides/checklists/new-css-file.md`](docs/guides/checklists/new-css-file.md) |
| Running a security audit | [`docs/guides/checklists/security-audit.md`](docs/guides/checklists/security-audit.md) |

## Things to NEVER Do

1. **NEVER use `DateTime.Now`** - Always `DateTime.UtcNow`
2. **NEVER use repositories in Query handlers** - Use `IApplicationReadDbConnection`
3. **NEVER use reflection to set entity properties** - Use constructors
4. **NEVER bypass factory methods for new entities** - They contain validation
5. **NEVER commit secrets to appsettings.json** - Use KeyVault references
6. **NEVER put multiple public types in one file**
7. **NEVER use US English spelling** - Use UK English
8. **NEVER make database changes without updating `docs/guides/database-schema.md`** and the refresh tool in `tools/ThePredictions.DatabaseTools/`

## Quick Reference

### Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Classes | PascalCase | `LeagueRepository` |
| Private fields | _camelCase | `_connectionFactory` |
| Commands | Command suffix | `CreateLeagueCommand` |
| Queries | Query suffix | `GetMyLeaguesQuery` |
| Handlers | Handler suffix | `CreateLeagueCommandHandler` |
| DTOs | Dto suffix | `LeagueDto` |

### Solution Structure

```
src/
├── PredictionLeague.Domain           → Core business entities
├── PredictionLeague.Application      → CQRS commands/queries
├── PredictionLeague.Infrastructure   → Data access, external services
├── PredictionLeague.API              → REST controllers
├── PredictionLeague.Web              → Blazor server host
├── PredictionLeague.Web.Client       → Blazor WebAssembly UI
├── PredictionLeague.Contracts        → DTOs
└── PredictionLeague.Validators       → FluentValidation validators
tools/
└── ThePredictions.DatabaseTools      → Dev database refresh & prod backup tool
tests/                                → (planned)
```

### Useful Commands

```bash
dotnet run --project src/PredictionLeague.API      # Run API
dotnet run --project src/PredictionLeague.Web      # Run Blazor client
dotnet build PredictionLeague.sln                  # Build all
```
