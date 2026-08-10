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

### NuGet Package Versions

When adding a **new** NuGet package, reference the **highest available version** - **including pre-release / preview** versions - as long as it is compatible with the project's target framework (`net8.0`) and restores and builds cleanly (including under `/p:TreatWarningsAsErrors=true`). **Never settle for the lowest version** that merely satisfies a transitive dependency's floor. Always pin an explicit version (no floating ranges) so builds stay reproducible. If the latest preview turns out to be incompatible or breaks the build, drop to the highest version that does work.

### Database Schema Documentation

Any database changes (new tables, new columns, modified constraints, new indexes) **must** be reflected in [`docs/guides/database-schema.md`](docs/guides/database-schema.md). This file is the single source of truth for the database schema.

Any new tables must also be added to the database refresh tool in [`tools/ThePredictions.DatabaseTools/`](tools/ThePredictions.DatabaseTools/):
- Add the table to the correct position in the `TableCopyOrder` or `TablesToSkip` arrays in `DatabaseRefresher.cs` (respecting foreign key dependencies)
- If the table contains personal data, add anonymisation rules to `DataAnonymiser.cs`
- If the table contains sensitive tokens, add verification to `PersonalDataVerifier.cs`

### Migration SQL — committed only under the DbUp Migrations folder

Database schema changes are managed by **DbUp** (ADR-0013). Committed `.sql` files are allowed in **one** place only: [`tools/ThePredictions.DatabaseTools/Migrations/`](tools/ThePredictions.DatabaseTools/Migrations/) — embedded, numbered `NNNN_PascalCase.sql`, applied in order, and **immutable once applied** (add a new script rather than editing one). Write new schema changes as a new migration script there.

**Everywhere else, still do NOT create `.sql` files** (no `tools/sql/`, no loose scripts on disk). For any genuinely ad-hoc / one-off SQL outside the migration set, **present it directly in the chat window** in a fenced code block for the user to run manually.

- State whether a migration is **additive** (new tables, new nullable/`DEFAULT`-ed columns — safe to apply ahead of the code deploy) or **destructive** (drop/rename/retype — must ship with or after the matching deploy).
- Migrations are forward-only: to reverse a change, add a new forward "undo" script; take a backup (`backup-prod-db.yml`) before anything destructive.
- Still update `docs/guides/database-schema.md` and `DatabaseTools` as above.

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

### Dapper Result Mapping — SELECT column order MUST match the record constructor

Query handlers read into **positional `record` result types**, and Dapper matches the constructor to the result set **positionally**: parameter *N* must line up with `SELECT` column *N* by **both name and type**. A mismatch is **not** caught by the compiler or unit tests — it only throws at **runtime**:

> `InvalidOperationException: A parameterless default constructor or one matching signature (...) is required for <Handler>+<Result> materialization`

**The rule:** whenever you add, remove, reorder, or retype a column in a `SELECT`, make the **identical** change to the result `record` (and vice versa). Keep the two lists in lockstep, top to bottom.

```csharp
// SELECT order:  Id, FullName, Email, EmailConfirmed, IsAdmin
//                 1      2       3         4              5

// CORRECT — record parameters in the same order
private record UserQueryResult(string Id, string FullName, string Email, bool EmailConfirmed, bool IsAdmin);

// WRONG — EmailConfirmed/IsAdmin moved; column 4 (bool EmailConfirmed) now hits
//         parameter 4 (bool IsAdmin) — passes build & tests, throws at runtime
private record UserQueryResult(string Id, string FullName, string Email, bool IsAdmin, bool EmailConfirmed);
```

Computed/`CASE`/`COALESCE` columns must be aliased (`... AS HasSeasonPass`) and counted as a column in this ordering. The C# mapping from the result record to the outward DTO is by name and is **not** affected — only the `SELECT`↔`record` pairing is positional.

**Query handlers materialise into private result records, never directly into Contracts DTOs.** The generic argument to `QueryAsync<T>` / `QuerySingleOrDefaultAsync<T>` must be a `private record XxxQueryResult(...)` co-located in the handler (or another Application-owned row type), kept in lockstep with the SELECT column order. Map from the result record to the outward Contracts DTO by name afterwards. Scalars (`int`, `bool`, `string`) are exempt. This keeps the fragile positional coupling inside a single file, next to its SQL, instead of extending it into the shared Contracts assembly where a UI-motivated constructor reorder would break queries at runtime.

**Type each parameter from the column, not from the DTO.** Dapper widens nothing in a positional match: an `int` column will **not** fill a `long` parameter (`Nullable<T>` aside). Classes mapped by name *do* convert, so copying a type across from the outward DTO is exactly how this breaks:

```csharp
// [SnapshotOverallRank] is an int column; LeaderboardEntryDto.SnapshotRank is long?
private record OverallLeaderboardQueryResult(..., long? SnapshotRank, ...);  // WRONG - throws at runtime
private record OverallLeaderboardQueryResult(..., int? SnapshotRank, ...);   // CORRECT - widens in the C# mapping
```

`RANK()` / `ROW_NUMBER()` / `COUNT_BIG()` are `bigint` (`long`); `COUNT()` and `SUM()` over an `int` column are `int`.

**Do not check this by hand - run the schema check.** `dotnet run --project tools/ThePredictions.SchemaCheck` verifies *every* Dapper read in `src/` against the shape SQL Server actually returns, and exits non-zero on anything that cannot materialise. Unit tests cannot catch this class of bug because handler tests mock `IApplicationReadDbConnection`, so no SQL ever runs. Enable the pre-push hook once per clone with `git config core.hooksPath .githooks` and it checks the reads your commits touch before they reach a shared branch. To inspect a single query manually, `EXEC sys.sp_describe_first_result_set @tsql = N'...', @params = N'...'` and read `system_type_name`. Read failures are server faults, logged as Errors and returned as 500s. Business rules throw `BusinessRuleViolationException` for the 400/Warning bucket, so a materialisation bug can never be mistaken for a client mistake - see [ADR-0016](docs/decisions/0016-business-rule-exception-classification.md) and [`docs/guides/database.md`](docs/guides/database.md#result-mapping).

### Testing & Code Coverage

Every project **must maintain 100% line and branch coverage** - Domain, Validators, Contracts, Hosting.Shared, API, Infrastructure, Persistence.SqlServer, Web.Client and Application are all gated in CI, so a drop fails the build. See [`docs/guides/testing.md`](docs/guides/testing.md#enforcement-is-per-project-and-rolls-out-gradually) for how the gate is wired up. After writing or modifying code:

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
| Brevo email templates (HTML copies, house style, merge tags, ids) | [`docs/email-templates/README.md`](docs/email-templates/README.md) |

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

## Decision Records (ADRs)

Significant product, business, legal, and technical decisions are recorded in [`docs/decisions/`](docs/decisions/) (ADR-style). **Read the relevant record before changing a decision**, and when reversing one, add a new record and mark the old one `Superseded by NNNN` rather than rewriting it. See [`docs/decisions/README.md`](docs/decisions/README.md) for the index and template.

## Operational Processes

Repeatable operational procedures (external-service access, credential retrieval, manual steps, runbooks) live in [`docs/processes/`](docs/processes/). Check there before assuming a process needs manual UI work. Notably: **Brevo email templates are managed via the API, not the Brevo UI** - see [`docs/processes/brevo-template-management.md`](docs/processes/brevo-template-management.md). Never commit secret values to these docs; document where a secret lives and how it is accessed, not the value.

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
10. **NEVER create `.sql` files in the repository EXCEPT under `tools/ThePredictions.DatabaseTools/Migrations/`** (the DbUp migration set, ADR-0013; numbered, embedded, immutable once applied) - everywhere else, present ad-hoc SQL in the chat for the user to run manually
11. **NEVER let a query's `SELECT` column order drift from its result `record` constructor** - Dapper maps them positionally (name + type per position); a mismatch compiles and passes tests but throws at runtime. See [Dapper Result Mapping](#dapper-result-mapping--select-column-order-must-match-the-record-constructor).

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
├── ThePredictions.Persistence.SqlServer → SQL Server adapter (see docs/todo/architecture/persistence-split)
├── ThePredictions.Infrastructure   → External services (email, payments, football API, images)
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
├── Unit/                             → Unit tests (xUnit + FluentAssertions)
└── Integration/                      → Real SQL Server via Testcontainers (see docs/guides/testing.md)
```

### Useful Commands

```bash
dotnet run --project src/ThePredictions.API      # Run API
dotnet run --project src/ThePredictions.Web      # Run Blazor client
dotnet build ThePredictions.sln                  # Build all
tools\Test Coverage\coverage-unit.bat              # Run unit tests + coverage report
dotnet run --project tools/ThePredictions.SchemaCheck   # Verify every Dapper read against SQL Server
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
