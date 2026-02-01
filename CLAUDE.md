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

```sql
-- CORRECT: Brackets, PascalCase, parameters
SELECT [Id], [Name] FROM [Leagues] WHERE [SeasonId] = @SeasonId

-- WRONG: No brackets, wrong case
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
| UK English, naming, formatting, DateTime | [`docs/claude/code-style.md`](docs/claude/code-style.md) |
| Commands, queries, MediatR patterns | [`docs/claude/cqrs-patterns.md`](docs/claude/cqrs-patterns.md) |
| Entity construction, repositories, immutability | [`docs/claude/domain-models.md`](docs/claude/domain-models.md) |
| SQL conventions, Dapper patterns | [`docs/claude/database.md`](docs/claude/database.md) |
| Log message formatting | [`docs/claude/logging.md`](docs/claude/logging.md) |
| Domain concepts, tech stack, infrastructure | [`docs/claude/project-context.md`](docs/claude/project-context.md) |

## Project-Specific Guidelines

| Project | File |
|---------|------|
| API controllers, authentication, error handling | [`PredictionLeague.API/CLAUDE.md`](PredictionLeague.API/CLAUDE.md) |
| Blazor components, state management, CSS | [`PredictionLeague.Web.Client/CLAUDE.md`](PredictionLeague.Web.Client/CLAUDE.md) |

## Workflow Checklists

Use these when creating new features:

| Task | Checklist |
|------|-----------|
| Creating a new command | [`docs/claude/checklists/new-command.md`](docs/claude/checklists/new-command.md) |
| Creating a new query | [`docs/claude/checklists/new-query.md`](docs/claude/checklists/new-query.md) |
| Adding a new CSS file | [`docs/claude/checklists/new-css-file.md`](docs/claude/checklists/new-css-file.md) |

## Things to NEVER Do

1. **NEVER use `DateTime.Now`** - Always `DateTime.UtcNow`
2. **NEVER use repositories in Query handlers** - Use `IApplicationReadDbConnection`
3. **NEVER use reflection to set entity properties** - Use constructors
4. **NEVER bypass factory methods for new entities** - They contain validation
5. **NEVER commit secrets to appsettings.json** - Use KeyVault references
6. **NEVER put multiple public types in one file**
7. **NEVER use US English spelling** - Use UK English

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
PredictionLeague.Domain           → Core business entities
PredictionLeague.Application      → CQRS commands/queries
PredictionLeague.Infrastructure   → Data access, external services
PredictionLeague.API              → REST controllers
PredictionLeague.Web.Client       → Blazor WebAssembly UI
PredictionLeague.Contracts        → DTOs
PredictionLeague.Validators       → FluentValidation validators
```

### Useful Commands

```bash
dotnet run --project PredictionLeague.API      # Run API
dotnet run --project PredictionLeague.Web      # Run Blazor client
dotnet build PredictionLeague.sln              # Build all
```
