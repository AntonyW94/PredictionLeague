# Fix 09: Missing Validators

> **Parent Plan**: [Security Fixes Overview](./security-fixes-overview.md)

## Priority
**P2 - Medium** - Fix soon

## Severity
**Medium** - Input validation gaps

## CWE Reference
[CWE-20: Improper Input Validation](https://cwe.mitre.org/data/definitions/20.html)

---

## Problem Description

Several request types are missing FluentValidation validators, and some existing validators have incomplete validation rules.

### Missing Validators

| Request Type | File | Risk |
|-------------|------|------|
| `ApplyBoostRequest` | `Contracts/Boosts/ApplyBoostRequest.cs` | Invalid IDs, empty boost codes |
| `DefinePrizeStructureRequest` | `Contracts/Leagues/DefinePrizeStructureRequest.cs` | Invalid prize configurations |

### Incomplete Validators

| Validator | File | Missing Rules |
|-----------|------|---------------|
| `CreateLeagueRequestValidator` | `Validators/Leagues/CreateLeagueRequestValidator.cs` | Price >= 0, deadline validation |
| `UpdateLeagueRequestValidator` | `Validators/Leagues/UpdateLeagueRequestValidator.cs` | Price >= 0, deadline validation |

---

## Solution

### 1. Create ApplyBoostRequestValidator

**File**: `PredictionLeague.Validators/Boosts/ApplyBoostRequestValidator.cs`

```csharp
using FluentValidation;
using PredictionLeague.Contracts.Boosts;

namespace PredictionLeague.Validators.Boosts;

public class ApplyBoostRequestValidator : AbstractValidator<ApplyBoostRequest>
{
    public ApplyBoostRequestValidator()
    {
        RuleFor(x => x.LeagueId)
            .GreaterThan(0)
            .WithMessage("League ID must be greater than 0.");

        RuleFor(x => x.RoundId)
            .GreaterThan(0)
            .WithMessage("Round ID must be greater than 0.");

        RuleFor(x => x.BoostCode)
            .NotEmpty()
            .WithMessage("Boost code is required.")
            .MaximumLength(50)
            .WithMessage("Boost code must not exceed 50 characters.")
            .Matches("^[A-Z_]+$")
            .WithMessage("Boost code must contain only uppercase letters and underscores.");
    }
}
```

### 2. Create DefinePrizeStructureRequestValidator

**File**: `PredictionLeague.Validators/Leagues/DefinePrizeStructureRequestValidator.cs`

```csharp
using FluentValidation;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Validators.Leagues;

public class DefinePrizeStructureRequestValidator : AbstractValidator<DefinePrizeStructureRequest>
{
    public DefinePrizeStructureRequestValidator()
    {
        RuleFor(x => x.PrizeSettings)
            .NotEmpty()
            .WithMessage("At least one prize setting is required.");

        RuleForEach(x => x.PrizeSettings)
            .SetValidator(new PrizeSettingValidator());
    }

    private class PrizeSettingValidator : AbstractValidator<PrizeSettingRequest>
    {
        public PrizeSettingValidator()
        {
            RuleFor(x => x.PrizeType)
                .NotEmpty()
                .WithMessage("Prize type is required.")
                .Must(BeValidPrizeType)
                .WithMessage("Prize type must be a valid value (Round, Monthly, Overall, MostExactScores).");

            RuleFor(x => x.Amount)
                .GreaterThanOrEqualTo(0)
                .WithMessage("Prize amount must be 0 or greater.");

            RuleFor(x => x.Position)
                .GreaterThan(0)
                .When(x => x.Position.HasValue)
                .WithMessage("Position must be greater than 0 when specified.");

            RuleFor(x => x.Month)
                .InclusiveBetween(1, 12)
                .When(x => x.Month.HasValue)
                .WithMessage("Month must be between 1 and 12 when specified.");
        }

        private static bool BeValidPrizeType(string prizeType)
        {
            var validTypes = new[] { "Round", "Monthly", "Overall", "MostExactScores" };
            return validTypes.Contains(prizeType, StringComparer.OrdinalIgnoreCase);
        }
    }
}
```

### 3. Update CreateLeagueRequestValidator

**File**: `PredictionLeague.Validators/Leagues/CreateLeagueRequestValidator.cs`

```csharp
using FluentValidation;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Validators.Leagues;

public class CreateLeagueRequestValidator : AbstractValidator<CreateLeagueRequest>
{
    public CreateLeagueRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("League name is required.")
            .MaximumLength(100)
            .WithMessage("League name must not exceed 100 characters.")
            .Matches(@"^[\p{L}\p{N}\s\-']+$")
            .WithMessage("League name contains invalid characters.");

        RuleFor(x => x.SeasonId)
            .GreaterThan(0)
            .WithMessage("Season ID must be greater than 0.");

        // ADD: Price validation
        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Price must be 0 or greater.")
            .LessThanOrEqualTo(10000)
            .WithMessage("Price must not exceed 10,000.");

        // ADD: Entry deadline validation
        RuleFor(x => x.EntryDeadlineUtc)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Entry deadline must be in the future.");

        RuleFor(x => x.PointsForExactScore)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Points for exact score must be 0 or greater.");

        RuleFor(x => x.PointsForCorrectResult)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Points for correct result must be 0 or greater.");

        // ADD: Logical validation
        RuleFor(x => x)
            .Must(x => x.PointsForExactScore >= x.PointsForCorrectResult)
            .WithMessage("Points for exact score should be greater than or equal to points for correct result.");
    }
}
```

### 4. Update UpdateLeagueRequestValidator

**File**: `PredictionLeague.Validators/Leagues/UpdateLeagueRequestValidator.cs`

```csharp
using FluentValidation;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Validators.Leagues;

public class UpdateLeagueRequestValidator : AbstractValidator<UpdateLeagueRequest>
{
    public UpdateLeagueRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("League name is required.")
            .MaximumLength(100)
            .WithMessage("League name must not exceed 100 characters.")
            .Matches(@"^[\p{L}\p{N}\s\-']+$")
            .WithMessage("League name contains invalid characters.");

        // ADD: Price validation
        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Price must be 0 or greater.")
            .LessThanOrEqualTo(10000)
            .WithMessage("Price must not exceed 10,000.");

        // ADD: Entry deadline validation
        RuleFor(x => x.EntryDeadlineUtc)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Entry deadline must be in the future.");
    }
}
```

---

## Additional Validators to Consider

### DeleteUserRequestValidator

**File**: `PredictionLeague.Validators/Admin/Users/DeleteUserRequestValidator.cs`

```csharp
using FluentValidation;
using PredictionLeague.Contracts.Admin.Users;

namespace PredictionLeague.Validators.Admin.Users;

public class DeleteUserRequestValidator : AbstractValidator<DeleteUserRequest>
{
    public DeleteUserRequestValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.NewAdministratorId)
            .NotEmpty()
            .When(x => !string.IsNullOrEmpty(x.NewAdministratorId))
            .WithMessage("New administrator ID cannot be empty when specified.");

        RuleFor(x => x)
            .Must(x => x.UserId != x.NewAdministratorId)
            .When(x => !string.IsNullOrEmpty(x.NewAdministratorId))
            .WithMessage("New administrator cannot be the same as the user being deleted.");
    }
}
```

### RefreshTokenRequestValidator

**File**: `PredictionLeague.Validators/Authentication/RefreshTokenRequestValidator.cs`

```csharp
using FluentValidation;
using PredictionLeague.Contracts.Authentication;

namespace PredictionLeague.Validators.Authentication;

public class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty()
            .When(x => x.RefreshToken != null)
            .WithMessage("Refresh token cannot be empty when provided.")
            .MaximumLength(500)
            .WithMessage("Refresh token is too long.");
    }
}
```

---

## Registering Validators

Ensure validators are registered with the DI container. If using automatic registration:

**File**: `PredictionLeague.Validators/DependencyInjection.cs`

```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace PredictionLeague.Validators;

public static class DependencyInjection
{
    public static IServiceCollection AddValidators(this IServiceCollection services)
    {
        // Register all validators from this assembly
        services.AddValidatorsFromAssemblyContaining<CreateLeagueRequestValidator>();

        return services;
    }
}
```

---

## Testing Requirements

> **Note**: Automated testing is deferred until test project infrastructure is in place.
> Test code is preserved below for future implementation.

### Manual Testing (Required for this implementation)

1. Test each validator by submitting invalid requests via API
2. Verify 400 Bad Request is returned with appropriate error messages
3. Verify valid requests pass through

### Future: Unit Tests

<details>
<summary>Click to expand test code for future implementation</summary>

```csharp
public class ApplyBoostRequestValidatorTests
{
    private readonly ApplyBoostRequestValidator _validator = new();

    [Fact]
    public void Validate_ValidRequest_Succeeds()
    {
        var request = new ApplyBoostRequest(
            LeagueId: 1,
            RoundId: 5,
            BoostCode: "DOUBLE_UP");

        var result = _validator.Validate(request);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 1, "DOUBLE_UP")]  // Invalid LeagueId
    [InlineData(1, 0, "DOUBLE_UP")]  // Invalid RoundId
    [InlineData(1, 1, "")]           // Empty BoostCode
    [InlineData(1, 1, "invalid")]    // Lowercase BoostCode
    public void Validate_InvalidRequest_Fails(int leagueId, int roundId, string boostCode)
    {
        var request = new ApplyBoostRequest(leagueId, roundId, boostCode);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
    }
}

public class CreateLeagueRequestValidatorTests
{
    private readonly CreateLeagueRequestValidator _validator = new();

    [Fact]
    public void Validate_NegativePrice_Fails()
    {
        var request = new CreateLeagueRequest(
            Name: "Test League",
            SeasonId: 1,
            Price: -10,
            EntryDeadlineUtc: DateTime.UtcNow.AddDays(7),
            PointsForExactScore: 3,
            PointsForCorrectResult: 1);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Price");
    }

    [Fact]
    public void Validate_PastDeadline_Fails()
    {
        var request = new CreateLeagueRequest(
            Name: "Test League",
            SeasonId: 1,
            Price: 10,
            EntryDeadlineUtc: DateTime.UtcNow.AddDays(-1),  // Past
            PointsForExactScore: 3,
            PointsForCorrectResult: 1);

        var result = _validator.Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "EntryDeadlineUtc");
    }
}
```

</details>

---

## Checklist

- [ ] Create `ApplyBoostRequestValidator`
- [ ] Create `DefinePrizeStructureRequestValidator`
- [ ] Update `CreateLeagueRequestValidator` with Price/Deadline rules
- [ ] Update `UpdateLeagueRequestValidator` with Price/Deadline rules
- [ ] Create `DeleteUserRequestValidator` (optional)
- [ ] Create `RefreshTokenRequestValidator` (optional)
- [ ] Verify validators are registered in DI
- [ ] Manual testing - validation errors return 400 Bad Request
- [ ] Code review approved
- [ ] Deployed to production

### Future (when test projects added)
- [ ] Write unit tests for all validators
