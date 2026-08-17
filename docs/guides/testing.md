# Testing & Code Coverage

This guide covers the testing tools, conventions, and coverage requirements for the project.

## Coverage Requirement

**Every project targets 100% line and branch coverage**, with `[ExcludeFromCodeCoverage]` for what is deliberately not tested - see [Coverage: 100% Everywhere](#coverage-100-everywhere-with-deliberate-exclusions) for why, and for the exclusion policy.

**Enforced on all nine unit test projects** - Domain, Validators, Contracts, Hosting.Shared, API, Infrastructure, Persistence.SqlServer, Web.Client and Application. None may regress. Run the coverage report after any code or test change.

## Tools

| Tool | Purpose | Install Command |
|------|---------|-----------------|
| [xUnit](https://xunit.net/) | Test framework | Via NuGet (already in test projects) |
| [FluentAssertions](https://fluentassertions.com/) | Assertion library | Via NuGet (already in test projects) |
| [Coverlet](https://github.com/coverlet-coverage/coverlet) | Code coverage collector | Via NuGet (`coverlet.collector`, already in test projects) |
| [ReportGenerator](https://github.com/danielpalme/ReportGenerator) | HTML coverage reports | `dotnet tool install --global dotnet-reportgenerator-globaltool` |

## Running Coverage

### Automated (Recommended)

Run the batch script from the repository root:

```bash
tools\Test Coverage\coverage-unit.bat
```

This script:
1. Cleans previous coverage data from the `coverage/` folder
2. Discovers and runs all `*.csproj` files under `tests\Unit\`
3. Collects coverage using coverlet with settings from `tools\Test Coverage\coverage.runsettings`
4. Generates an HTML report at `coverage\report\index.html`
5. Opens the report in your default browser

### Manual

If you need to run coverage manually (e.g. for a single test project):

```bash
# Run tests with coverage
dotnet test "tests\Unit\ThePredictions.Domain.Tests.Unit\ThePredictions.Domain.Tests.Unit.csproj" ^
    --collect:"XPlat Code Coverage" ^
    --results-directory .\coverage ^
    --settings "tools\Test Coverage\coverage.runsettings"

# Generate HTML report
reportgenerator "-reports:.\coverage\**\coverage.cobertura.xml" "-targetdir:.\coverage\report" "-reporttypes:Html"

# Open the report
start .\coverage\report\index.html
```

## Coverage Configuration

The `tools\Test Coverage\coverage.runsettings` file configures coverlet to:
- Output Cobertura XML format
- Exclude code marked with `[GeneratedCode]` or `[CompilerGenerated]` attributes
- Exclude generated files (`*.g.cs`) and build output (`obj/`)

The `coverage/` folder is gitignored — it is generated locally and never committed.

## Interpreting the Report

### Summary Section

Shows overall line coverage and branch coverage percentages. Both must be **100%**.

### Risk Hotspots

Lists methods with high **CRAP scores** (Change Risk Anti-Patterns). CRAP score combines cyclomatic complexity with code coverage:
- **CRAP = complexity** when coverage is 100% (ideal)
- **CRAP increases exponentially** as coverage drops

To reduce CRAP scores, extract complex methods into smaller private helpers to distribute the cyclomatic complexity.

**The report currently says "No risk hotspots found", and that is the state to keep it in.** The last two were both the same shape: a method that made no decisions of its own but assembled a reply out of thirty-odd `x?.Field` and `?? 0` expressions, each one a branch. Extracting a resolver per group - one that answers "there is no active round" or "nobody holds this record yet" once, and returns a small record of settled values - moves those branches into methods small enough to read. Where a method is complex because it is *deciding* something, that is a rule and it belongs in the Domain instead.

### Coverage Table

Shows per-class breakdown with columns:

| Column | Meaning |
|--------|---------|
| **Covered** | Number of lines/branches executed by tests |
| **Uncovered** | Lines/branches not hit — these need tests or `[ExcludeFromCodeCoverage]` |
| **Coverable** | Total lines/branches that coverlet can instrument |
| **Total** | All lines in the file (including non-coverable like braces, blank lines) |
| **Percentage** | Covered / Coverable — must be **100%** |

Click any class name to see a line-by-line view with green (covered) and red (uncovered) highlighting.

### Branch Coverage

Branch coverage tracks every possible path through `if`, `switch`, `??`, ternary (`? :`), and pattern matching expressions. A single `if` statement creates 2 branches (true and false). The `??` operator also creates 2 branches (null and non-null).

## Coverage: 100% Everywhere, With Deliberate Exclusions

The target is **100% line and branch on every project**, not just Domain.

The reason is not that 100% means more confidence than 95%. It is that **100% is a ratchet with no
slack**. Any lower threshold leaves a buffer where new untested code lands silently until the buffer
fills, and nobody can tell which commit spent it. At 100%, untested code fails the build at once,
and the only way past is to type `[ExcludeFromCodeCoverage]` - which appears in a diff where it can
be questioned. The exclusion turns "we forgot" into "we decided".

### `[ExcludeFromCodeCoverage]` means deliberately not worth testing

It does **not** mean "not written yet". The moment it becomes a to-do marker, the ratchet stops
meaning anything. **Always give a reason, via the attribute's own `Justification` property** - not a
comment, so the reason travels with the attribute and is visible to tooling and in review:

```csharp
[ExcludeFromCodeCoverage(Justification = "SQL is verified by tools/ThePredictions.SchemaCheck; the mapping is exercised E2E.")]
public class GetOverallLeaderboardQueryHandler(...)
```

Every exclusion in `src/` carries one, and **`ThePredictions.Conventions.Tests.Unit` enforces both
that and the agreed wording** - a missing `Justification`, or a new phrasing not added to that
project's `ApprovedJustifications` list, fails the build. No off-the-shelf analyser can express this
rule (see EctManager's ADR-0082, which investigated and found none), so it lives in a test.

Be clear about what that buys: it stops an unexplained exclusion and stops the wordings drifting into
near-duplicates. It **cannot** tell that a justification is *wrong* for the code it sits on - the
August 2026 audit found five exclusions carrying well-formed justifications that described code they
did not match. That is still a reading job.

The recurring reasons are worded consistently so they can be grepped and counted:

| Reason | Applies to |
|--------|-----------|
| `Data-only contract: properties only, no logic to test.` | `ThePredictions.Contracts` DTOs, requests and responses |
| `Data-only type: properties only, no logic to test.` | Data-only types elsewhere (API DTOs, Domain snapshots, prize breakdown types) |
| `Parameterless constructor for Dapper hydration: no logic to test.` | The private/public `Entity()` constructors Dapper needs |
| `Dapper row type: properties only, no logic to test.` | Private `XxxRow` types a handler materialises into |
| `Options type bound from configuration: properties only, no logic to test.` | `*Settings` / `*Options` classes bound from `appsettings` |

A one-off exclusion should say something specific rather than reuse a row above. The other recurring
reasons cover query handlers, MediatR request records, repositories, Identity stores, database
plumbing, controllers, middleware, health checks, image renderers, third-party API clients, Blazor
components, typed `HttpClient` wrappers and browser interop.

**Never exclude a type that already has a test file.** If someone wrote `FooTests.cs`, `Foo` is
something we test, and excluding it hides passing tests and makes them look pointless. This rule
matters most for query handlers: the category is excluded wholesale, but seven of them have real
tests because they do more than SQL plus a mapping, and those stay measured.

Reasonable exclusions: positional result records, data-only Contracts DTOs, ORM-only constructors,
`Program.cs` wiring, controllers that only forward to MediatR, and query handlers whose body is a
SQL string plus a mapping - a unit test there mocks `IApplicationReadDbConnection`, so it verifies
neither the SQL nor the `SELECT`-to-record alignment.

Every data-only type in `ThePredictions.Contracts` carries the attribute. A DTO that grows a
computed property or a method has stopped being data-only - **remove the attribute and test it**
rather than letting real logic hide behind it. The types in Contracts that already have logic
(`PagedResult<T>`, `PageSizes`, `UserDto`, `RoundCompletionPlayerDto`, `BadgeGlyphs`) are
deliberately *not* excluded, so they show up as a genuine gap until they are covered.

### Async method bodies must stay measured

`ExcludeByAttribute` lists **`GeneratedCodeAttribute` only**. Do not add `CompilerGeneratedAttribute`
back to it, however tempting it looks for suppressing record boilerplate.

Every `async` method compiles into a state machine type carrying `[CompilerGenerated]`. While that
attribute was excluded, **the body of every async method in the solution was invisible to coverage**.
`UpdateRoundCommandHandler` - forty-odd lines with match updates, deletions and a business-rule
throw - reported as *one* coverable line, its constructor, and therefore as fully covered. Five
projects were sitting on a 100% badge that did not mean what it said.

Removing it roughly tripled Application's coverable lines (1,530 to 4,485) and moved the honest
figures to:

| Project | Line coverage | Gated |
|---------|---------------|-------|
| Domain, Validators, Contracts, Hosting.Shared, API, Infrastructure, Web.Client | 100% | yes |
| Application | ~55% | no |

Those seven were taken to a genuine 100% *after* the exclusion was removed, so the figure now counts
async bodies. Application is the only one left.

The cost is that compiler-synthesised record members (`Equals`, `GetHashCode`, `ToString`, the copy
constructor) now count. For a data-only record that is already handled by `[ExcludeFromCodeCoverage]`.
For a record that carries logic and so cannot be excluded, test its value-equality contract - and
note that a record holding a collection compares that member **by reference**, so two instances with
equal-but-separate lists are not equal.

`**/Program.cs` is excluded by file: host wiring, verified by the app starting.

### Generated Razor markup is excluded at the measurement level

`**/*.razor` is in `ExcludeByFile` in both `ci.yml` and `tools/Test Coverage/coverage.runsettings`.
The Razor compiler turns component markup into `BuildRenderTree` methods, and coverlet counted those
as ~4,200 uncovered lines in `Web.Client` - about 95% of that project's coverable lines, and roughly
two thirds of every uncovered line in the solution. It made the report unreadable: the number moved
when markup changed and stood still when logic did.

The cost is that `@code` blocks share the file and are excluded too. All 118 components keep their
logic there (no `.razor.cs` code-behind, and bUnit is not referenced), so none of it is testable
today in any case. **To bring component logic back under measurement, move it into a `.razor.cs`
code-behind** - that file is measured normally.

### Enforcement is per project

`/p:Threshold=100` in `ci.yml` is only honoured by **`coverlet.msbuild`**. Every unit test project
references it, so **Domain, Validators, Contracts, Hosting.Shared, API, Infrastructure, Persistence.SqlServer, Web.Client
and Application** are all gated. Application was the last to join, in August 2026.

**The gate measures each test project's own run, not the merged report.** A class covered
incidentally by another project's tests still counts as uncovered for the gate, so read the
standalone figure - `dotnet test <one project> --collect:"XPlat Code Coverage"` - before assuming a
project is ready to gate. The merged report is for reading; the per-project run is what CI enforces.

A gated test project whose subject assembly has dependencies must scope the measurement with an
`<Include>` property, or the threshold sees those dependencies too and fails on their coverage.
`ThePredictions.Validators.Tests.Unit` sets `<Include>[ThePredictions.Validators]*</Include>` so
only the Validators assembly counts, not the Contracts and Domain assemblies it pulls in. Domain
needs no filter: it depends on nothing outside the global `[ThePredictions.Tests.*]*` exclusion.

Adding a new test project to the gate is one package reference plus an `<Include>`. **Do it only
when that project has actually reached 100%**, in the same PR - never as a big-bang flip, which
would leave CI red for weeks and destroy the signal the gate exists to give. Update the rule in the
root `CLAUDE.md` as each project locks, so the documented rule always matches what CI enforces.

## Integration Tests Against Real SQL Server

`tests/Integration/ThePredictions.Persistence.SqlServer.Tests.Integration` runs the SQL Server adapter's SQL against a
throwaway SQL Server. It exists because three kinds of rule are **structurally invisible** to the unit
suite, however high its coverage goes: rules expressed in a SQL predicate, the aggregate diffing inside
repositories, and predicates duplicated across two call sites. Handler unit tests mock
`IApplicationReadDbConnection`, so no SQL ever runs, and `SchemaCheck` proves only that a result set
*can materialise* - never that a query returns the right rows. See
[`docs/todo/architecture/test-suite/high-risk-integration-targets.md`](../todo/architecture/test-suite/high-risk-integration-targets.md)
for the worst-first list.

### How the harness works

| Piece | What it does |
|-------|--------------|
| **Testcontainers** | Starts one SQL Server container per test run (`Testcontainers.MsSql`, image pinned). |
| **DbUp** | Builds the schema by running the committed migrations, read from `ThePredictions.Persistence.SqlServer` via `MigrationScripts` — the same assembly and journal identity `ThePredictions.DatabaseTools` uses, so the two cannot disagree. |
| **Respawn** | Deletes every row between tests and leaves the schema alone, so each test arranges from empty. |

**Not SQLite**, and that is not a detail. The queries under test use `RANK() OVER`, `CROSS APPLY`,
`MERGE`, `GETUTCDATE()` and `CAST(... AS bit)`; SQLite either rejects those or evaluates them
differently, so a SQLite suite would go green while proving nothing. It also cannot reproduce the
`int`/`bigint` distinction behind the July 2026 leaderboard outage.

Building the schema from the migrations rather than a schema script kept beside the tests has a second
payoff: every run is now continuous proof that the migration set **applies cleanly from empty**, which
nothing else checks. The three real databases were baselined from a schema that already existed, so
`0001_Baseline.sql` has only ever run there as a no-op.

### Running them

Needs a **running Docker daemon with Linux containers** (on Docker Desktop for Windows, that means the
Linux engine, not Windows containers). The first run pulls ~1.5GB.

If Docker is in Windows-container mode the failure is misleading: Testcontainers reports a **missing
image** rather than an engine mismatch, so it reads as a bad tag. `docker pull` is what tells the
truth (`image operating system "linux" cannot be used on this platform`). Check with
`docker info --format '{{.OSType}}'` before assuming the tag is wrong. CI on `ubuntu-latest` is
unaffected.

```bash
dotnet test tests\Integration\ThePredictions.Persistence.SqlServer.Tests.Integration
```

### Conventions specific to this project

- **Every test class carries `[Trait("Category", "Integration")]`.** CI runs the unit suite with
  `--filter "Category!=Integration"` and this project in its own job, so an untraited class would be run
  by neither. `IntegrationTraitConventionTests` fails the build if one is missing.
- **Derive from `DatabaseTestBase`**, which wipes the database before each test and hands over the same
  seams the application is wired with.
- **Arrange with `TestDataSeeder`, assert with raw SQL.** The seeder writes columns directly rather than
  going through a repository, so a test never depends on the write path it is about to assert on - and it
  can arrange states the domain forbids (an unconfirmed fixture, a postponed match), which is exactly
  what these predicates have to cope with.
- **Never assume an identity value.** Respawn does not reseed identities, so every seeder method returns
  the id the database generated.
- **Substitute the collaborators, never the database.** A query handler's membership check is separate SQL
  in its own class, so NSubstitute stands in for it and keeps the test about the predicate. The read
  connection is always the real `DapperReadDbConnection`.
- **Prove the test bites.** A test that passes against a broken predicate is worse than no test, because
  it reads as cover. Break the rule under test, watch the expected tests fail and nothing else, then put
  it back. Every rule pinned so far has been verified this way, and the count of failures is recorded in
  the commit message.

### It does not affect the 100% gate

This project references `coverlet.collector` but **not** `coverlet.msbuild`: it owns no assembly of its
own, so there is nothing for the threshold to measure - the same shape as
`ThePredictions.Composition.Tests.Unit` and `ThePredictions.Conventions.Tests.Unit`.

The types it covers - `RoundRepository` and the query handlers - **stay `[ExcludeFromCodeCoverage]`**, and
that is correct rather than a compromise. The gate measures each unit test project's own run, which can
never include a container-backed suite, so removing the attribute would simply turn the gate red.
`NoExcludedType_ShouldAlreadyHaveAUnitTestFile` is scoped to `tests/Unit` for exactly this reason.

## End-to-End Journeys Against An Isolated Stack

`tests/E2E/ThePredictions.Web.Tests.E2E` drives real Chromium through user journeys against a stack it builds
from nothing on every run. It exists because of a specific failure: on 30 July 2026 a leaderboard query's
`SELECT` had drifted from its result record, so the page threw for a real user with 1,647 green unit tests and
a happy compiler. `SchemaCheck` now catches that class of drift, but nothing was **loading a page**.

**Currently one journey**, deliberately - a player signs in and reaches an authenticated dashboard. The
expensive part of a browser suite is the stack underneath it, so nothing was proven until one journey drove it
end to end. See [the plan](../todo/architecture/e2e-testing/README.md#outstanding) for what comes next.

### What a run builds, and in what order

| Step | Piece | Note |
|------|-------|------|
| 1 | SQL Server container | `Testcontainers.MsSql`, image pinned to the same tag the integration suite pulls, so one download serves both |
| 2 | A database with production's collation | `0001_Baseline.sql` omits every `COLLATE` clause on purpose (ADR-0013), so a different default would give the whole schema different comparison and sort semantics |
| 3 | The committed DbUp migrations | Read from `MigrationScripts` in the adapter, so this suite, the integration suite and `DatabaseTools` cannot disagree about journal keys |
| 4 | One seeded user | SQL, not the registration endpoint - a test should not arrange through the path it asserts on |
| 5 | The **published** web application | Launched as its own process against the container |
| 6 | Chromium | Drives it |

**Order 3-before-5 matters.** The `DatabaseInitialiser` hosted service writes the Identity roles from the
`ApplicationUserRole` enum when the app starts, so it needs a schema to write them into. Roles are therefore
never seeded by hand.

**Published, not `dotnet run`.** Publishing materialises the Blazor client into wwwroot and runs the CSS
bundling target, so the browser is handed what a deployment hands it. `dotnet run` would depend on static web
assets, which only resolve automatically in the Development environment - and Development is the one
environment this cannot use, because `Program.cs` then demands an `appsettings.Development.Secrets.json` for
Key Vault.

**The site is not containerised**, deliberately. A runner is already an ephemeral environment destroyed after
the run, so a Dockerfile would add an image to maintain and a build per run for isolation that is already
there. Docker is needed for SQL Server only.

### Configuration the application needs to run anywhere but its own environments

Worked out by reading `Program.cs`, and each one is commented in `WebApplicationProcess` with what breaks
without it:

| Variable | Why |
|----------|-----|
| `ASPNETCORE_ENVIRONMENT=E2E` | An environment with no `appsettings` file of its own, so only the base file loads, no `KeyVaultUri` is present, and the Key Vault branch is skipped without asking for a secrets file |
| `AllowedHosts=*` | `appsettings.json` restricts it to the two production hostnames, so host filtering would 400 every request to localhost |
| `ApiBaseUrl` | Drives CORS, `SiteSettings.BaseUrl`, and through `${ApiBaseUrl}` substitution the JWT issuer and audience |
| `JwtSettings__Secret` | Otherwise the signing key stays the unresolved `${Jwt-Secret}` placeholder at 13 characters, under the 128 bits HS256 demands, and the first sign-in throws instead of returning a token |

`EnableSubstitutions` silently skips a placeholder it cannot resolve, so the remaining `${...}` secrets stay
literal and do not fail startup. That was checked, not assumed.

### Levels, and choosing them per run

Every test class carries exactly one level, and a run selects by combining them:

| Level | Meaning |
|-------|---------|
| `Smoke` | Cannot break without the site being unusable |
| `Core` | Features used most weeks |
| `Extended` | Rarely used, but still has to work |

```bash
dotnet test tests/E2E/ThePredictions.Web.Tests.E2E --filter "Category=E2E&(Level=Smoke|Level=Core)"
```

`e2e.yml` takes Smoke and Core on a pull request, everything on the 03:00 schedule, and three
`workflow_dispatch` tickboxes when run by hand.

**Two traps, both guarded, both of which produce a green run that tested nothing:**

- `dotnet test` **exits 0 when a filter matches nothing** - it prints "No test matches the given testcase
  filter" and reports success. So a renamed level, or ticking Extended before any Extended journey exists,
  would look like a pass. The workflow reads the executed count back from a TRX and fails on zero.
- The obvious way to default a tickbox, `${{ inputs.smoke || 'true' }}`, is broken: `false` is falsy in GitHub
  expression syntax, so `false || 'true'` is `'true'` and an **unticked** box silently turns back on. The
  filter is built in a shell step that branches on `github.event_name` instead.

### Seeing what ran

The workflow renders a table on its own summary page - the selection, every test with outcome and duration,
and the totals - on success as much as failure, because "what did it check?" is the question a green tick does
not answer. A TRX is uploaded on every run; Playwright traces only on failure, opened with
`playwright show-trace <file>`.

### Running it locally

Needs a **running Docker daemon with Linux containers** (on Docker Desktop for Windows, the Linux engine, not
Windows containers - `docker info --format '{{.OSType}}'` tells the truth). Then:

```bash
dotnet publish src/ThePredictions.Web -c Release -o artifacts/e2e-web
pwsh tests/E2E/ThePredictions.Web.Tests.E2E/bin/Release/net8.0/playwright.ps1 install chromium
dotnet test tests/E2E/ThePredictions.Web.Tests.E2E
```

| Variable | Default | Purpose |
|----------|---------|---------|
| `E2E_WEB_APP_DLL` | `artifacts/e2e-web/ThePredictions.Web.dll` | The published application to launch |
| `E2E_PORT` | `5099` | Port the application listens on |
| `E2E_HEADED` | `false` | `true` to watch the browser |
| `E2E_SLOW_MO_MS` | `0` | Pause between actions; only useful headed |
| `E2E_ARTIFACTS_DIR` | beside the build output | Where per-test traces are written |

### Conventions specific to this project

- **`data-test-id` for everything, never a CSS class.** A class is a styling hook and renaming one is an
  ordinary thing to do to a stylesheet; a test id is a stated contract. Ids live as constants in `TestIds`,
  and `TestIdConventionTests` reads the markup and fails the build when one is referenced that the markup does
  not carry - the half a compiler cannot see. See
  [the Blazor guidelines](../../src/ThePredictions.Web.Client/CLAUDE.md#test-hooks-data-test-id).
- **Hyphenated, not `data-testid`.** That is Playwright's default rather than ours, so an element carrying it
  looks annotated and is unfindable. A convention test rejects it.
- **Browser assertions go through `LocatorAssertionExtensions`**, which wrap Playwright's *web-first*
  assertions. `(await locator.CountAsync()).Should().Be(0)` reads fine and samples the page once, which races
  a Blazor render and becomes the intermittent failure nobody can reproduce. `AwesomeAssertions` is right for
  the convention tests, where there is no page to settle.
- **`SiteLayout` owns what the layout renders** - navigation, consent banner, error panels. A page object owns
  only its own page. The consent banner started on `LoginPage` and was wrong there: it is fixed to the foot of
  every page, so the first journey starting elsewhere would have had a click silently swallowed.
- **Every test class carries `[Trait("Category", "E2E")]`** (inherited from `E2ETestBase`) **and exactly one
  `Level`**, on the class and not on a `[Fact]`. Convention tests enforce both. A method-level level *does*
  filter correctly, which is the trap - it would work while being invisible to the check guaranteeing it.
- **`AwesomeAssertions`, not FluentAssertions.** This project moved first; the rest follow (roadmap Track 1
  item 3).

### It does not affect the 100% gate

`coverlet.collector` but **not** `coverlet.msbuild`: it owns no assembly for the threshold to measure, the
same shape as the integration and conventions projects. CI's unit job filters
`Category!=Integration&Category!=E2E`, and both deploy workflows filter `Category!=E2E`.

## Composition / Container Validation

A MediatR handler that depends on a service the host never registers is **invisible to `dotnet build` and to handler unit tests** (those construct the handler with mocks). The gap only surfaces at app startup, via the Development host's `ValidateOnBuild`, i.e. on deploy.

`tests/Unit/ThePredictions.Composition.Tests.Unit/ContainerValidationTests.cs` closes that gap: it builds the **real** DI container (`AddInfrastructureServices` + `AddApiServices` with representative configuration) and resolves every `IRequestHandler<>` / `IRequestHandler<,>`, so a missing registration fails in CI with the offending handler and its unregistered dependency named, instead of on deploy.

When you add a handler with a new dependency, run this project. If it needs a configuration value read at registration time (or in an eagerly-built singleton constructor), add that key to the in-memory configuration in `BuildConfiguration()`.

## Dapper Result Mapping / Schema Check

The same blind spot applies to SQL. A query whose `SELECT` no longer matches its result record is
**invisible to `dotnet build` and to handler unit tests**, for exactly the reason above: those tests
mock `IApplicationReadDbConnection`, so no SQL runs. The failure surfaces as an
`InvalidOperationException` when a real user loads the page.

`tools/ThePredictions.SchemaCheck` closes that gap by asking SQL Server what each read actually
returns and comparing it with the type being materialised:

```bash
dotnet run --project tools/ThePredictions.SchemaCheck
```

It reads the connection string from `PREDICTIONS_DEV_DB`. Exit `0` is clean, `1` means a read cannot
materialise, `2` means the tool could not run. `--changed <paths>` narrows the sweep, and
`git config core.hooksPath .githooks` enables a pre-push hook that checks only the reads your commits
touch.

Some reads cannot be verified and are reported as skipped rather than failures: `QueryMultiple`
batches (SQL Server only describes a batch's first statement), multi-mapping overloads that split
columns with `splitOn`, and types whose base class sits outside the scanned source. Treat those as
still needing care by hand.

## Test Conventions

### Test Project Structure

```
tests/
├── Shared/
│   └── ThePredictions.Tests.Shared/
│       └── Helpers/
│           └── TestDateTimeProvider.cs  → Shared test doubles
└── Unit/
    ├── ThePredictions.Domain.Tests.Unit/
    │   ├── Common/
    │   │   ├── Exceptions/        → Exception class tests
    │   │   └── Guards/            → Guard clause extension tests
    │   ├── Models/                → Entity tests (one file per entity)
    │   └── Services/              → Domain service tests
    ├── ThePredictions.Infrastructure.Tests.Unit/
    │   └── Data/                  → Read/write connection behaviour (no database required)
    └── ThePredictions.API.Tests.Unit/
        └── Middleware/            → Exception-to-status mapping (see ADR-0016)
tests/
└── Integration/
    ├── ThePredictions.Persistence.Conformance/      → adapter-NEUTRAL. Abstract bases, no dialect.
    │   └── Repositories/                            → what any IXxxRepository must do
    └── ThePredictions.Persistence.SqlServer.Tests.Integration/
        ├── Harness/               → Container, migrations, Respawn reset, seeder, inspector
        ├── Queries/               → Rules that live in a SQL predicate (SQL Server-specific for now)
        ├── Repositories/          → Concrete subclasses that run the conformance suite
        └── Schema/                → The migrations applying cleanly from empty
tests/
└── E2E/
    └── ThePredictions.Web.Tests.E2E/
        ├── Harness/               → Container, migrations, seed, app process, browser, conventions
        ├── Pages/                 → Page objects: locators and navigation, never assertions
        └── *JourneyTests.cs       → One class per journey, each carrying exactly one Level
```

### The conformance suite

`ThePredictions.Persistence.Conformance` holds **abstract** test classes: the behaviour any adapter must
exhibit, written once, with no SQL and no dialect. Each adapter's own project derives a concrete class that
supplies three members - the repository under test, an `ITestDataSeeder` and an `ITestDataInspector` - and
inherits every test. `SqlServerRoundRepositoryTests` is that class for SQL Server and is about thirty lines
long; a second adapter gets the same seven tests by writing one the same size.

Arrangement and assertion both go behind interfaces because **neither can be dialect-free**: the schema
belongs to the adapter, so there is no portable way to insert or read a row. That keeps the rule that a test
never arranges or asserts through the code it is testing, while still letting the test itself be portable.

`ConformanceSuite_ShouldNotReferenceAnyAdapter` fails the build if the conformance project ever references
a concrete adapter, which is the one change that would silently destroy its value.

A conformance test asserts **only what the port promises**. `BoostCatalogueQueryConformanceTests`
deliberately does not assert row order, because the port promises none - `ORDER BY` defers to the database's
collation, so ordering is a rule the handler applies in C#. Asserting order there would fail an adapter that
is perfectly correct.

**Not everything can live there yet.** `BoostUsageSecrecyTests` and `PredictableMatchPredicateTests` stay
SQL Server-specific because the handlers they exercise still contain T-SQL. They move across as the
persistence split reaches their feature area. `MigrationsFromEmptyTests` stays adapter-specific by nature -
each adapter has its own migration set.

### Test Naming

Use the pattern: `MethodName_ShouldX_WhenY()`

```csharp
[Fact]
public void Create_ShouldSetName_WhenValidNameProvided()

[Fact]
public void AddMember_ShouldThrowException_WhenUserIdIsNull()

[Fact]
public void GetMostExactScoresWinners_ShouldReturnEmptyList_WhenAllMembersHaveZeroExactScores()
```

### Test Structure

Use Arrange/Act/Assert with comments:

```csharp
[Fact]
public void Create_ShouldSetName_WhenValidNameProvided()
{
    // Arrange
    var name = "Test League";

    // Act
    var league = League.Create(seasonId, name, userId, deadline, 3, 1, 0, season, dateTimeProvider);

    // Assert
    league.Name.Should().Be(name);
}
```

### Creating Entities in Tests

**Testing factory methods** — use `Entity.Create(...)`:

```csharp
// Testing that Create() sets properties correctly
var league = League.Create(seasonId, name, userId, deadline, 3, 1, 0, season, dateTimeProvider);
league.Name.Should().Be(name);
```

**Testing methods that require an ID** (e.g. `AddMember`, `AddMatch`) — use the public constructor with an explicit ID, because `Create()` leaves `Id` as 0 and child entity factories validate that the parent ID is greater than 0:

```csharp
// Use the public/database constructor with an explicit ID
var league = new League(
    id: 1, name: "Test League", seasonId: 1,
    administratorUserId: "admin-user", entryCode: "ABC123",
    createdAtUtc: dateTimeProvider.UtcNow,
    entryDeadlineUtc: futureDeadline,
    pointsForExactScore: 3, pointsForCorrectResult: 1,
    price: 0, isFree: true, hasPrizes: false,
    prizeFundOverride: null,
    members: null, prizeSettings: null);

// Now AddMember won't throw — league.Id is 1, not 0
league.AddMember("user-1", dateTimeProvider);
```

### Organising Tests

Group related tests using `#region` blocks matching the method under test:

```csharp
#region Create — Happy Path
// ...
#endregion

#region Create — Validation
// ...
#endregion

#region AddMember
// ...
#endregion
```

## Maintaining 100% Coverage

### When to Write Tests

Write tests for:
- All factory method validation (guard clauses, business rules)
- All factory method property assignment (happy path)
- All public domain methods and their edge cases
- Constructor property mapping (when the constructor has logic like null-filtering)

### When to Use `[ExcludeFromCodeCoverage]`

Use the attribute **only** when code genuinely cannot or should not be tested:

| Scenario | Example | Action |
|----------|---------|--------|
| ORM-only parameterless constructor | `public LeagueRoundResult() { }` | Add `[ExcludeFromCodeCoverage]` to the constructor |
| Data-only snapshot/DTO class | `BoostWindowSnapshot` (properties only, no logic) | Add `[ExcludeFromCodeCoverage]` to the class |
| Compiler-generated code | Regex source generators (`*.g.cs`) | Already excluded via `coverage.runsettings` |

**Never** use `[ExcludeFromCodeCoverage]` to hide untested logic. If code has branches or behaviour, it needs tests.

### When to Remove Code

If coverage reveals a branch that is genuinely unreachable (dead code), **remove it** rather than excluding it. For example, `key.ToString() ?? string.Empty` where `key` is always `int` or `string` — the `?? string.Empty` can never execute, so remove it.

### Reducing CRAP Scores

If a method has a high CRAP score due to cyclomatic complexity:
1. Extract logical groups of conditions into private helper methods
2. This distributes the complexity so no single method exceeds the risk hotspot threshold
3. Ensure all existing tests still pass after refactoring
