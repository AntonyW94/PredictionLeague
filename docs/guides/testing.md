# Testing & Code Coverage

This guide covers the testing tools, conventions, and coverage requirements for the project.

## Coverage Requirement

The Domain project **must maintain 100% line and 100% branch coverage**. This is enforced by running the coverage report after any code or test changes.

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

## Test Conventions

### Test Project Structure

```
tests/
├── Shared/
│   └── ThePredictions.Tests.Shared/
│       └── Helpers/
│           └── TestDateTimeProvider.cs  → Shared test doubles
└── Unit/
    └── ThePredictions.Domain.Tests.Unit/
        ├── Common/
        │   ├── Exceptions/        → Exception class tests
        │   └── Guards/            → Guard clause extension tests
        ├── Models/                → Entity tests (one file per entity)
        └── Services/              → Domain service tests
```

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
