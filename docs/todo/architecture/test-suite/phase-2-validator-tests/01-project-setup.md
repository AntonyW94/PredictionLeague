# Task 1: Project Setup

**Parent Plan:** [Phase 2: Validator Unit Tests](./README.md)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped - the `ThePredictions.Validators.Tests.Unit` project exists and is wired into the solution and CI.

## Goal

Create the `ThePredictions.Validators.Tests.Unit` test project and the `ThePredictions.Tests.Builders` shared builders project, with all required references and fluent request builders.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Validators.Tests.Unit/ThePredictions.Validators.Tests.Unit.csproj` | Create | New test project |
| `tests/Shared/ThePredictions.Tests.Builders/ThePredictions.Tests.Builders.csproj` | Create | New shared builders project |
| `tests/Shared/ThePredictions.Tests.Builders/Authentication/LoginRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Authentication/RegisterRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Authentication/RefreshTokenRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Authentication/RequestPasswordResetRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Authentication/ResetPasswordRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Leagues/CreateLeagueRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Leagues/UpdateLeagueRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Leagues/DefinePrizeStructureRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Leagues/DefinePrizeSettingDtoBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Leagues/JoinLeagueRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Predictions/SubmitPredictionsRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Predictions/PredictionSubmissionDtoBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Boosts/ApplyBoostRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Admin/Matches/CreateMatchRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Admin/Matches/UpdateMatchRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Admin/Rounds/CreateRoundRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Admin/Rounds/UpdateRoundRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Admin/Matches/MatchResultDtoBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Admin/Seasons/CreateSeasonRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Admin/Seasons/UpdateSeasonRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Admin/Teams/CreateTeamRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Admin/Teams/UpdateTeamRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Admin/Users/DeleteUserRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Admin/Users/UpdateUserRoleRequestBuilder.cs` | Create | Builder |
| `tests/Shared/ThePredictions.Tests.Builders/Account/UpdateUserDetailsRequestBuilder.cs` | Create | Builder |
| `ThePredictions.sln` | Modify | Add both projects to solution |

## Implementation Steps

### Step 1: Create the Builders Project

Create `tests/Shared/ThePredictions.Tests.Builders/ThePredictions.Tests.Builders.csproj`. This project only references `ThePredictions.Contracts`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\src\ThePredictions.Contracts\ThePredictions.Contracts.csproj" />
  </ItemGroup>
</Project>
```

### Step 2: Create the Test Project

Create `tests/Unit/ThePredictions.Validators.Tests.Unit/ThePredictions.Validators.Tests.Unit.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="xunit.v3" Version="3.0.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
    <PackageReference Include="FluentAssertions" Version="7.0.0" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\..\src\ThePredictions.Validators\ThePredictions.Validators.csproj" />
    <ProjectReference Include="..\..\Shared\ThePredictions.Tests.Builders\ThePredictions.Tests.Builders.csproj" />
  </ItemGroup>
</Project>
```

**Key note:** The `FluentValidation.TestHelper` namespace (providing `TestValidate()`, `ShouldHaveValidationErrorFor()`, and `ShouldNotHaveAnyValidationErrors()`) is built into the main FluentValidation package. No separate NuGet package is needed — it comes transitively via the Validators project reference (FluentValidation 12.1.1).

### Step 3: Add Both Projects to Solution

```bash
dotnet sln ThePredictions.sln add tests/Shared/ThePredictions.Tests.Builders/ThePredictions.Tests.Builders.csproj --solution-folder "Tests\Shared"
dotnet sln ThePredictions.sln add tests/Unit/ThePredictions.Validators.Tests.Unit/ThePredictions.Validators.Tests.Unit.csproj --solution-folder "Tests\Unit"
```

### Step 4: Create Folder Structures

**Builders project** (mirrors the Contracts project structure):

```
tests/Shared/ThePredictions.Tests.Builders/
├── Account/
├── Admin/
│   ├── Matches/
│   ├── Rounds/
│   ├── Seasons/
│   ├── Teams/
│   └── Users/
├── Authentication/
├── Boosts/
├── Leagues/
└── Predictions/
```

**Test project** (mirrors the Validators project structure):

```
tests/Unit/ThePredictions.Validators.Tests.Unit/
├── Account/
├── Admin/
│   ├── Matches/
│   ├── Rounds/
│   ├── Seasons/
│   ├── Teams/
│   └── Users/
├── Authentication/
├── Boosts/
├── Leagues/
└── Predictions/
```

### Step 5: Create Fluent Request Builders

Each builder follows this pattern:

1. **Private fields** with valid defaults — `new XxxBuilder().Build()` always returns a valid object
2. **`With*()`** methods for each validated property — returns `this` for chaining
3. **`Build()`** method — returns a new request/DTO instance

#### Full Example: `CreateLeagueRequestBuilder`

```csharp
public class CreateLeagueRequestBuilder
{
    private string _name = "Test League";
    private int _seasonId = 1;
    private decimal _price = 10.00m;
    private DateTime _entryDeadlineUtc = new(2099, 6, 1, 12, 0, 0, DateTimeKind.Utc);
    private int _pointsForExactScore = 3;
    private int _pointsForCorrectResult = 1;

    public CreateLeagueRequestBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CreateLeagueRequestBuilder WithSeasonId(int seasonId)
    {
        _seasonId = seasonId;
        return this;
    }

    public CreateLeagueRequestBuilder WithPrice(decimal price)
    {
        _price = price;
        return this;
    }

    public CreateLeagueRequestBuilder WithEntryDeadlineUtc(DateTime entryDeadlineUtc)
    {
        _entryDeadlineUtc = entryDeadlineUtc;
        return this;
    }

    public CreateLeagueRequestBuilder WithPointsForExactScore(int pointsForExactScore)
    {
        _pointsForExactScore = pointsForExactScore;
        return this;
    }

    public CreateLeagueRequestBuilder WithPointsForCorrectResult(int pointsForCorrectResult)
    {
        _pointsForCorrectResult = pointsForCorrectResult;
        return this;
    }

    public CreateLeagueRequest Build() => new()
    {
        Name = _name,
        SeasonId = _seasonId,
        Price = _price,
        EntryDeadlineUtc = _entryDeadlineUtc,
        PointsForExactScore = _pointsForExactScore,
        PointsForCorrectResult = _pointsForCorrectResult
    };
}
```

#### Full Example: `CreateMatchRequestBuilder`

```csharp
public class CreateMatchRequestBuilder
{
    private int _homeTeamId = 1;
    private int _awayTeamId = 2;
    private DateTime _matchDateTimeUtc = new(2025, 6, 15, 15, 0, 0, DateTimeKind.Utc);

    public CreateMatchRequestBuilder WithHomeTeamId(int homeTeamId)
    {
        _homeTeamId = homeTeamId;
        return this;
    }

    public CreateMatchRequestBuilder WithAwayTeamId(int awayTeamId)
    {
        _awayTeamId = awayTeamId;
        return this;
    }

    public CreateMatchRequestBuilder WithMatchDateTimeUtc(DateTime matchDateTimeUtc)
    {
        _matchDateTimeUtc = matchDateTimeUtc;
        return this;
    }

    public CreateMatchRequest Build() => new()
    {
        HomeTeamId = _homeTeamId,
        AwayTeamId = _awayTeamId,
        MatchDateTimeUtc = _matchDateTimeUtc
    };
}
```

#### Full Example: `RegisterRequestBuilder`

```csharp
public class RegisterRequestBuilder
{
    private string _firstName = "John";
    private string _lastName = "Smith";
    private string _email = "john.smith@example.com";
    private string _password = "ValidPass1";

    public RegisterRequestBuilder WithFirstName(string firstName)
    {
        _firstName = firstName;
        return this;
    }

    public RegisterRequestBuilder WithLastName(string lastName)
    {
        _lastName = lastName;
        return this;
    }

    public RegisterRequestBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public RegisterRequestBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    public RegisterRequest Build() => new()
    {
        FirstName = _firstName,
        LastName = _lastName,
        Email = _email,
        Password = _password
    };
}
```

#### Remaining Builders — Default Values

All remaining builders follow the same pattern. The table below lists each builder and its default field values:

| Builder | Fields (default values) |
|---------|------------------------|
| `LoginRequestBuilder` | Email = `"user@example.com"`, Password = `"ValidPass1"` |
| `RefreshTokenRequestBuilder` | Token = `"valid-refresh-token"` |
| `RequestPasswordResetRequestBuilder` | Email = `"user@example.com"` |
| `ResetPasswordRequestBuilder` | Token = `"valid-reset-token"`, NewPassword = `"NewValidPass1"`, ConfirmPassword = `"NewValidPass1"` |
| `UpdateLeagueRequestBuilder` | Name = `"Updated League"`, Price = `10.00m`, EntryDeadlineUtc = `2099-06-01`, PointsForExactScore = `3`, PointsForCorrectResult = `1` |
| `DefinePrizeStructureRequestBuilder` | PrizeSettings = `[new DefinePrizeSettingDtoBuilder().Build()]` |
| `DefinePrizeSettingDtoBuilder` | PrizeType = valid enum, PrizeAmount = `50.00m`, Rank = `1`, Multiplier = `1.0m`, PrizeDescription = `"First place"` |
| `JoinLeagueRequestBuilder` | EntryCode = `"ABC123"` |
| `SubmitPredictionsRequestBuilder` | RoundId = `1`, Predictions = `[new PredictionSubmissionDtoBuilder().Build()]` |
| `PredictionSubmissionDtoBuilder` | MatchId = `1`, HomeScore = `2`, AwayScore = `1` |
| `ApplyBoostRequestBuilder` | LeagueId = `1`, RoundId = `1`, BoostCode = `"DOUBLE"` |
| `UpdateMatchRequestBuilder` | HomeTeamId = `1`, AwayTeamId = `2`, MatchDateTimeUtc = `2025-06-15 15:00 UTC` |
| `CreateRoundRequestBuilder` | SeasonId = `1`, RoundNumber = `5`, StartDateUtc = `2025-01-01 12:00 UTC`, DeadlineUtc = `2025-01-02 12:00 UTC`, Matches = `[new CreateMatchRequestBuilder().Build()]` |
| `UpdateRoundRequestBuilder` | StartDateUtc = `2025-01-01 12:00 UTC`, DeadlineUtc = `2025-01-02 12:00 UTC`, Matches = `[new UpdateMatchRequestBuilder().Build()]` |
| `MatchResultDtoBuilder` | MatchId = `1`, HomeScore = `2`, AwayScore = `1`, Status = valid enum |
| `CreateSeasonRequestBuilder` | Name = `"Premier League 2025-26"`, StartDateUtc = `2025-08-01`, EndDateUtc = `2026-05-31`, NumberOfRounds = `38` |
| `UpdateSeasonRequestBuilder` | Same defaults as CreateSeasonRequestBuilder |
| `CreateTeamRequestBuilder` | Name = `"Manchester United"`, ShortName = `"Man United"`, LogoUrl = `"https://example.com/logo.png"`, Abbreviation = `"MUN"` |
| `UpdateTeamRequestBuilder` | Same defaults as CreateTeamRequestBuilder |
| `DeleteUserRequestBuilder` | NewAdministratorId = `null` |
| `UpdateUserRoleRequestBuilder` | NewRole = valid `ApplicationUserRole` enum name |
| `UpdateUserDetailsRequestBuilder` | FirstName = `"John"`, LastName = `"Smith"`, PhoneNumber = `"07123456789"` |

**Note:** Builders that contain child collections (e.g. `CreateRoundRequestBuilder`, `SubmitPredictionsRequestBuilder`) use the child builder's `Build()` for their defaults. This ensures changes to child builder defaults cascade automatically.

### Step 6: Verify Build

```bash
dotnet build ThePredictions.sln
```

Confirm both new projects compile with all dependencies resolved.

## Code Patterns to Follow

Match the existing domain test project for package versions:

```xml
<!-- From ThePredictions.Domain.Tests.Unit.csproj -->
<PackageReference Include="coverlet.collector" Version="6.0.4">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
<PackageReference Include="xunit.v3" Version="3.0.0" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.0.0" />
<PackageReference Include="FluentAssertions" Version="7.0.0" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.0" />
```

## Verification

- [ ] Both projects compile without errors
- [ ] `dotnet test` runs on the test project (0 tests initially is fine)
- [ ] Test project appears under `Tests\Unit` solution folder
- [ ] Builders project appears under `Tests\Shared` solution folder
- [ ] `using FluentValidation.TestHelper;` resolves (comes transitively from Validators project)
- [ ] All folder directories are created in both projects
- [ ] `new CreateLeagueRequestBuilder().Build()` produces a valid `CreateLeagueRequest`
- [ ] `tools\Test Coverage\coverage-unit.bat` discovers the new test project
- [ ] Builders project does NOT reference `ThePredictions.Domain`
- [ ] `ThePredictions.Tests.Shared` is NOT modified (remains Domain-only)

## Notes

- The `FluentValidation.TestHelper` namespace is built into the main FluentValidation package (12.1.1). No separate NuGet package is needed — it comes transitively via the Validators project reference.
- The builders project references `ThePredictions.Contracts` only. The existing `ThePredictions.Tests.Shared` project continues to reference `ThePredictions.Domain` only. This separation keeps dependencies clean.
- The builders folder structure mirrors the Contracts project (where the request/DTO types live). The test folder structure mirrors the Validators project (where the validators live). Both projects now use `Admin/Matches/` for all match-related types including `MatchResultDto`/`MatchResultDtoValidator`.
- Coverlet will automatically measure the Validators assembly because the test project has a `ProjectReference` to it via the Validators project reference. No changes to `coverage.runsettings` are needed.
- No `[ExcludeFromCodeCoverage]` should be needed — all validator code is testable through the public API.
- Each builder has exactly one public type per file, matching the codebase convention.
