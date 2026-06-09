# Phase 2: Validator Unit Tests

**Parent Plan:** [Test Suite Plan](../README.md)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped. `ThePredictions.Validators.Tests.Unit` exists with ~261 test cases covering the validators across all seven planned task areas.

## Summary

Create unit tests for all 27 validators in the `ThePredictions.Validators` project. Validators use FluentValidation and can be tested with `TestValidate()` making them quick wins with high confidence. This phase covers all validation rules across authentication, leagues, predictions, boosts, admin operations, and account management.

## Scope

| Category | Validators | Estimated Tests |
|----------|-----------|-----------------|
| Authentication | 5 (Login, Register, RefreshToken, RequestPasswordReset, ResetPassword) | ~31 |
| Leagues | 4 (CreateLeague, UpdateLeague, DefinePrizeStructure + nested, JoinLeague) | ~40 |
| Predictions & Boosts | 3 (SubmitPredictions, PredictionSubmissionDto, ApplyBoost) | ~17 |
| Admin Matches & Rounds | 5 (BaseMatch via Create/Update, CreateRound, UpdateRound, MatchResultDto) | ~30 |
| Admin Seasons & Teams | 4 (BaseSeason via Create/Update, BaseTeam via Create/Update) | ~24 |
| Admin Users & Account | 3 (DeleteUser, UpdateUserRole, UpdateUserDetails) | ~18 |
| **Total** | **24 concrete + 3 base** | **~160** |

## Test Approach

All validators are tested using FluentValidation's built-in `TestValidate()` method, combined with shared fluent request builders from `ThePredictions.Tests.Builders`:

```csharp
public class CreateLeagueRequestValidatorTests
{
    private readonly CreateLeagueRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        // Arrange
        var request = new CreateLeagueRequestBuilder().Build();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        // Arrange
        var request = new CreateLeagueRequestBuilder()
            .WithName("")
            .Build();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }
}
```

### Base Validator Testing Strategy

Three validators use inheritance (`BaseMatchRequestValidator`, `BaseSeasonRequestValidator`, `BaseTeamRequestValidator`). The base rules are tested through both concrete implementations:

- `CreateMatchRequestValidator` tests prove the base match rules work for creation
- `UpdateMatchRequestValidator` tests prove the same rules work for updates
- Each concrete test class has a full happy-path test and all individual rule failure tests

### Common Extension Testing Strategy

The custom validation extensions (`MustBeASafeName`, `MustBeASafeLeagueName`) are tested indirectly through the validators that use them. Each validator test class includes specific test cases for the character safety rules (XSS characters rejected, valid characters accepted).

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | [Project Setup](./01-project-setup.md) | Create test project, add references, configure solution | Not Started |
| 2 | [Authentication Validators](./02-authentication-validators.md) | Tests for Login, Register, RefreshToken, RequestPasswordReset, ResetPassword | Not Started |
| 3 | [League Validators](./03-league-validators.md) | Tests for CreateLeague, UpdateLeague, DefinePrizeStructure, JoinLeague | Not Started |
| 4 | [Prediction & Boost Validators](./04-prediction-and-boost-validators.md) | Tests for SubmitPredictions, PredictionSubmissionDto, ApplyBoost | Not Started |
| 5 | [Admin Match & Round Validators](./05-admin-match-and-round-validators.md) | Tests for Create/UpdateMatch (base rules), CreateRound, UpdateRound, MatchResultDto | Not Started |
| 6 | [Admin Season & Team Validators](./06-admin-season-and-team-validators.md) | Tests for Create/UpdateSeason (base rules), Create/UpdateTeam (base rules) | Not Started |
| 7 | [Admin User & Account Validators](./07-admin-user-and-account-validators.md) | Tests for DeleteUser, UpdateUserRole, UpdateUserDetails | Not Started |

## Dependencies

- [x] Phase 1: Domain Unit Tests (complete - 462 tests)
- [x] `ThePredictions.Validators` project exists with all 27 validators
- [x] `ThePredictions.Contracts` project exists with all request/DTO classes
- [ ] `FluentValidation.TestHelper` namespace available (built into FluentValidation 12.1.1, comes transitively via Validators reference)

## Coverage

### How Coverage Works

The `tools/Test Coverage/coverage-unit.bat` script discovers and runs **all** `.csproj` files under `tests/Unit/`. The `coverage.runsettings` file has no assembly inclusion/exclusion filters — coverlet measures whatever assemblies each test project references. Adding `ThePredictions.Validators.Tests.Unit` with a `ProjectReference` to `ThePredictions.Validators` will automatically include the Validators assembly in coverage reports.

### Coverage Target

The Validators project must achieve **100% line and branch coverage**, matching the Domain project standard. This is achievable because:

- All concrete validators have parameterised constructors only (no parameterless ORM constructors)
- All base validators have protected constructors only
- Extension methods are pure static (no untestable code)
- `[GeneratedRegex]` source-generated code is already excluded by the `[CompilerGeneratedAttribute]` filter in `coverage.runsettings`
- Private helper methods (e.g. `BeAValidUrl`) are fully reachable through the public validator API
- No `[ExcludeFromCodeCoverage]` should be needed anywhere in the Validators project

### Verifying Coverage

After completing all tasks, run:

```bash
tools\Test Coverage\coverage-unit.bat
```

The HTML report at `coverage/report/index.html` should show 100% line and 100% branch coverage for both the Domain and Validators assemblies.

## Shared Request Builders

### Fluent Builder Pattern

All request/DTO types get a fluent builder class in a dedicated `ThePredictions.Tests.Builders` project. Each builder:

- Has private fields with **valid defaults** so `new XxxBuilder().Build()` always produces a valid object
- Exposes `.WithProperty()` methods for each validated property
- Returns `this` for fluent chaining
- Has a `.Build()` method returning the request/DTO
- Follows one-public-type-per-file (one builder per `.cs` file)

```csharp
// Builder definition (in shared project)
public class CreateLeagueRequestBuilder
{
    private string _name = "Test League";
    private int _seasonId = 1;
    private decimal _price = 10.00m;
    // ... all fields with valid defaults

    public CreateLeagueRequestBuilder WithName(string name) { _name = name; return this; }
    public CreateLeagueRequestBuilder WithSeasonId(int id) { _seasonId = id; return this; }
    public CreateLeagueRequestBuilder WithPrice(decimal price) { _price = price; return this; }
    // ... all With methods

    public CreateLeagueRequest Build() => new()
    {
        Name = _name, SeasonId = _seasonId, Price = _price, ...
    };
}

// Usage in tests
var request = new CreateLeagueRequestBuilder().WithName("").Build();
var request = new CreateLeagueRequestBuilder().WithPrice(-1).Build();
```

### Why a Separate Project

The builders live in `tests/Shared/ThePredictions.Tests.Builders/`, separate from the existing `ThePredictions.Tests.Shared` project. This keeps dependencies clean:

| Project | References | Contains |
|---------|-----------|----------|
| `ThePredictions.Tests.Builders` | `ThePredictions.Contracts` only | Fluent request/DTO builders |
| `ThePredictions.Tests.Shared` | `ThePredictions.Domain` only | Test doubles (`TestDateTimeProvider`, etc.) |

Benefits:
- **Cross-project reuse:** Validator tests need them now; Application handler tests and API controller tests will reuse them in later phases
- **Single source of truth:** If a request type's properties change, only one builder needs updating
- **Standard pattern:** Establishes a consistent approach for all test projects in the solution
- **Clean dependencies:** Each shared project references only what it needs

### Builder Inventory

| Builder Class | Request/DTO | Builder File |
|--------------|-------------|--------------|
| `LoginRequestBuilder` | `LoginRequest` | `LoginRequestBuilder.cs` |
| `RegisterRequestBuilder` | `RegisterRequest` | `RegisterRequestBuilder.cs` |
| `RefreshTokenRequestBuilder` | `RefreshTokenRequest` | `RefreshTokenRequestBuilder.cs` |
| `RequestPasswordResetRequestBuilder` | `RequestPasswordResetRequest` | `RequestPasswordResetRequestBuilder.cs` |
| `ResetPasswordRequestBuilder` | `ResetPasswordRequest` | `ResetPasswordRequestBuilder.cs` |
| `CreateLeagueRequestBuilder` | `CreateLeagueRequest` | `CreateLeagueRequestBuilder.cs` |
| `UpdateLeagueRequestBuilder` | `UpdateLeagueRequest` | `UpdateLeagueRequestBuilder.cs` |
| `DefinePrizeStructureRequestBuilder` | `DefinePrizeStructureRequest` | `DefinePrizeStructureRequestBuilder.cs` |
| `DefinePrizeSettingDtoBuilder` | `DefinePrizeSettingDto` | `DefinePrizeSettingDtoBuilder.cs` |
| `JoinLeagueRequestBuilder` | `JoinLeagueRequest` | `JoinLeagueRequestBuilder.cs` |
| `SubmitPredictionsRequestBuilder` | `SubmitPredictionsRequest` | `SubmitPredictionsRequestBuilder.cs` |
| `PredictionSubmissionDtoBuilder` | `PredictionSubmissionDto` | `PredictionSubmissionDtoBuilder.cs` |
| `ApplyBoostRequestBuilder` | `ApplyBoostRequest` | `ApplyBoostRequestBuilder.cs` |
| `CreateMatchRequestBuilder` | `CreateMatchRequest` | `CreateMatchRequestBuilder.cs` |
| `UpdateMatchRequestBuilder` | `UpdateMatchRequest` | `UpdateMatchRequestBuilder.cs` |
| `CreateRoundRequestBuilder` | `CreateRoundRequest` | `CreateRoundRequestBuilder.cs` |
| `UpdateRoundRequestBuilder` | `UpdateRoundRequest` | `UpdateRoundRequestBuilder.cs` |
| `MatchResultDtoBuilder` | `MatchResultDto` | `MatchResultDtoBuilder.cs` |
| `CreateSeasonRequestBuilder` | `CreateSeasonRequest` | `CreateSeasonRequestBuilder.cs` |
| `UpdateSeasonRequestBuilder` | `UpdateSeasonRequest` | `UpdateSeasonRequestBuilder.cs` |
| `CreateTeamRequestBuilder` | `CreateTeamRequest` | `CreateTeamRequestBuilder.cs` |
| `UpdateTeamRequestBuilder` | `UpdateTeamRequest` | `UpdateTeamRequestBuilder.cs` |
| `DeleteUserRequestBuilder` | `DeleteUserRequest` | `DeleteUserRequestBuilder.cs` |
| `UpdateUserRoleRequestBuilder` | `UpdateUserRoleRequest` | `UpdateUserRoleRequestBuilder.cs` |
| `UpdateUserDetailsRequestBuilder` | `UpdateUserDetailsRequest` | `UpdateUserDetailsRequestBuilder.cs` |

## Technical Notes

### Package Requirements

The `TestValidate()` extension method and assertion helpers (`ShouldHaveValidationErrorFor()`, `ShouldNotHaveAnyValidationErrors()`) live in the `FluentValidation.TestHelper` namespace, which is built into the main FluentValidation package. No separate NuGet package is needed — the test project gets these via its transitive reference to FluentValidation 12.1.1 through the Validators project.

### Test File Structure

```
tests/Unit/ThePredictions.Validators.Tests.Unit/
├── Account/
│   └── UpdateUserDetailsRequestValidatorTests.cs
├── Admin/
│   ├── Matches/
│   │   ├── CreateMatchRequestValidatorTests.cs
│   │   ├── MatchResultDtoValidatorTests.cs
│   │   └── UpdateMatchRequestValidatorTests.cs
│   ├── Rounds/
│   │   ├── CreateRoundRequestValidatorTests.cs
│   │   └── UpdateRoundRequestValidatorTests.cs
│   ├── Seasons/
│   │   ├── CreateSeasonRequestValidatorTests.cs
│   │   └── UpdateSeasonRequestValidatorTests.cs
│   ├── Teams/
│   │   ├── CreateTeamRequestValidatorTests.cs
│   │   └── UpdateTeamRequestValidatorTests.cs
│   └── Users/
│       ├── DeleteUserRequestValidatorTests.cs
│       └── UpdateUserRoleRequestValidatorTests.cs
├── Authentication/
│   ├── LoginRequestValidatorTests.cs
│   ├── RefreshTokenRequestValidatorTests.cs
│   ├── RegisterRequestValidatorTests.cs
│   ├── RequestPasswordResetRequestValidatorTests.cs
│   └── ResetPasswordRequestValidatorTests.cs
├── Boosts/
│   └── ApplyBoostRequestValidatorTests.cs
├── Leagues/
│   ├── CreateLeagueRequestValidatorTests.cs
│   ├── UpdateLeagueRequestValidatorTests.cs
│   ├── DefinePrizeStructureRequestValidatorTests.cs
│   └── JoinLeagueRequestValidatorTests.cs
└── Predictions/
    ├── SubmitPredictionsRequestValidatorTests.cs
    └── PredictionSubmissionDtoValidatorTests.cs
```

### Naming Convention

All tests follow: `Validate_Should{Behaviour}_When{Conditions}()`

Examples:
- `Validate_ShouldPass_WhenAllFieldsAreValid()`
- `Validate_ShouldFail_WhenNameIsEmpty()`
- `Validate_ShouldFail_WhenNameContainsHtmlTags()`
- `Validate_ShouldFail_WhenPriceIsNegative()`
