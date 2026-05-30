# CLAUDE.md - ThePredictions

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

### Testing & Code Coverage

The Domain project **must maintain 100% line and branch coverage**. After writing or modifying code:

1. Write unit tests for all new/changed logic
2. Run the coverage script: `tools\Test Coverage\coverage-unit.bat`
3. Verify the report shows 100% line and 100% branch coverage
4. If code genuinely cannot be tested (e.g. parameterless constructors for ORM hydration), add `[ExcludeFromCodeCoverage]` to keep 100%

| Rule | Detail |
|------|--------|
| **Test naming** | `MethodName_ShouldX_WhenY()` |
| **New entities in tests** | Use public constructor with explicit ID (not `Create()` factory which leaves ID as 0) |
| **Factory methods in tests** | Use `Entity.Create(...)` only when testing the factory itself |
| **ORM-only constructors** | Mark with `[ExcludeFromCodeCoverage]` — they have no logic to test |
| **Data-only classes** | Mark with `[ExcludeFromCodeCoverage]` if class has no logic (only properties) |
| **Unreachable code** | Remove it rather than excluding it |
| **CancellationToken arguments** | Use `CancellationToken.None`, never a bare `default` |

> **CancellationToken in tests (xUnit1051):** The CI build runs with `/p:TreatWarningsAsErrors=true`, and the `xunit.v3` analyser raises **xUnit1051** when a method taking a `CancellationToken` is called with a bare `default`. This turns the whole build red. Always pass `CancellationToken.None` instead — including in mock verifications such as `DidNotReceiveWithAnyArgs()`.
>
> ```csharp
> // WRONG — xUnit1051, fails CI under TreatWarningsAsErrors
> await _repo.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, default);
>
> // CORRECT
> await _repo.DidNotReceiveWithAnyArgs().AddWinningsAsync(default!, CancellationToken.None);
> ```

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
| Testing, coverage tools, report interpretation | [`docs/guides/testing.md`](docs/guides/testing.md) |
| Domain concepts, tech stack, infrastructure | [`docs/guides/project-context.md`](docs/guides/project-context.md) |

## Project-Specific Guidelines

| Project | File |
|---------|------|
| API controllers, authentication, error handling | [`src/ThePredictions.API/CLAUDE.md`](src/ThePredictions.API/CLAUDE.md) |
| Blazor components, state management, CSS | [`src/ThePredictions.Web.Client/CLAUDE.md`](src/ThePredictions.Web.Client/CLAUDE.md) |

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
9. **NEVER leave code coverage below 100%** - Write tests or add `[ExcludeFromCodeCoverage]` for untestable code

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
├── ThePredictions.Domain           → Core business entities
├── ThePredictions.Application      → CQRS commands/queries
├── ThePredictions.Infrastructure   → Data access, external services
├── ThePredictions.API              → REST controllers
├── ThePredictions.Web              → Blazor server host
├── ThePredictions.Web.Client       → Blazor WebAssembly UI
├── ThePredictions.Contracts        → DTOs
└── ThePredictions.Validators       → FluentValidation validators
tools/
├── Test Coverage                     → Coverage scripts and settings
└── ThePredictions.DatabaseTools      → Dev database refresh & prod backup tool
tests/
├── Shared/                           → Shared test helpers (TestDateTimeProvider, etc.)
└── Unit/                             → Unit tests (xUnit + FluentAssertions)
```

### Useful Commands

```bash
dotnet run --project src/ThePredictions.API      # Run API
dotnet run --project src/ThePredictions.Web      # Run Blazor client
dotnet build ThePredictions.sln                  # Build all
tools\Test Coverage\coverage-unit.bat              # Run unit tests + coverage report
```

## Claude Code on the Web — Installing the .NET SDK

Web/mobile sessions run in an ephemeral Linux container that does **not** ship
with the .NET SDK, so `dotnet` is missing by default. The network allowlist
blocks the usual installers (`dot.net`, `builds.dotnet.microsoft.com`,
`packages.microsoft.com`), so `dotnet-install.sh` and `packages.microsoft.com`
will fail — but the **Ubuntu archive and `api.nuget.org` are reachable**, so
install the SDK from `apt` instead:

```bash
sudo apt-get update -qq                    # refresh the (stale) package index first
sudo apt-get install -y -qq dotnet-sdk-8.0 # installs the latest 8.0.1xx SDK
dotnet --version                           # confirm (e.g. 8.0.127)
```

This satisfies `global.json` (pinned to `8.0.100`, `rollForward: latestFeature`).
Once installed, `dotnet restore`/`build`/`test` work normally because
`api.nuget.org` is allowlisted. Build with `/p:TreatWarningsAsErrors=true` to
reproduce CI exactly.
