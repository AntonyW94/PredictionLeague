# Task 2: Authentication Validator Tests

**Parent Plan:** [Phase 2: Validator Unit Tests](./README.md)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped - authentication validator tests are present in `ThePredictions.Validators.Tests.Unit`.

## Goal

Write unit tests for all 5 authentication validators covering login, registration, token refresh, and password reset flows.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Authentication/LoginRequestValidatorTests.cs` | Create | Login validation tests |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Authentication/RegisterRequestValidatorTests.cs` | Create | Registration validation tests |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Authentication/RefreshTokenRequestValidatorTests.cs` | Create | Token refresh validation tests |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Authentication/RequestPasswordResetRequestValidatorTests.cs` | Create | Password reset request validation tests |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Authentication/ResetPasswordRequestValidatorTests.cs` | Create | Password reset completion validation tests |

## Implementation Steps

### Step 1: LoginRequestValidatorTests

**Validator:** `LoginRequestValidator`
**Validates:** `LoginRequest`

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Valid email and password |
| `Validate_ShouldFail_WhenEmailIsEmpty` | Email | Empty string |
| `Validate_ShouldFail_WhenEmailIsInvalid` | Email | Not a valid email format (e.g. `"not-an-email"`) |
| `Validate_ShouldFail_WhenPasswordIsEmpty` | Password | Empty string |
| `Validate_ShouldFail_WhenPasswordExceeds100Characters` | Password | 101 characters |
| `Validate_ShouldNotValidateEmailFormat_WhenEmailIsEmpty` | Email | Empty string should only trigger NotEmpty, not EmailAddress |

```csharp
public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new LoginRequestBuilder().Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenEmailIsEmpty()
    {
        var request = new LoginRequestBuilder()
            .WithEmail("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
```

### Step 2: RegisterRequestValidatorTests

**Validator:** `RegisterRequestValidator`
**Validates:** `RegisterRequest`

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Valid registration data |
| `Validate_ShouldFail_WhenFirstNameIsEmpty` | FirstName | Empty string |
| `Validate_ShouldFail_WhenFirstNameIsTooShort` | FirstName | 1 character (min is 2) |
| `Validate_ShouldFail_WhenFirstNameIsTooLong` | FirstName | 51 characters (max is 50) |
| `Validate_ShouldFail_WhenFirstNameContainsUnsafeCharacters` | FirstName | Contains `<script>` or similar |
| `Validate_ShouldFail_WhenLastNameIsEmpty` | LastName | Empty string |
| `Validate_ShouldFail_WhenLastNameIsTooShort` | LastName | 1 character |
| `Validate_ShouldFail_WhenLastNameIsTooLong` | LastName | 51 characters |
| `Validate_ShouldFail_WhenLastNameContainsUnsafeCharacters` | LastName | Contains unsafe characters |
| `Validate_ShouldPass_WhenNameContainsAccentedCharacters` | FirstName/LastName | e.g. `"Jérôme"`, `"O'Brien"` |
| `Validate_ShouldFail_WhenEmailIsEmpty` | Email | Empty string |
| `Validate_ShouldFail_WhenEmailIsInvalid` | Email | Not a valid email format |
| `Validate_ShouldFail_WhenPasswordIsEmpty` | Password | Empty string |
| `Validate_ShouldFail_WhenPasswordIsTooShort` | Password | 7 characters (min is 8) |
| `Validate_ShouldFail_WhenPasswordIsTooLong` | Password | 101 characters (max is 100) |

### Step 3: RefreshTokenRequestValidatorTests

**Validator:** `RefreshTokenRequestValidator`
**Validates:** `RefreshTokenRequest`

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenTokenIsValid` | Token | Non-empty string within length |
| `Validate_ShouldPass_WhenTokenIsNull` | Token | Null (conditional validation) |
| `Validate_ShouldFail_WhenTokenIsEmptyButNotNull` | Token | Empty string (When not null, must be NotEmpty) |
| `Validate_ShouldFail_WhenTokenExceeds500Characters` | Token | 501 characters |

### Step 4: RequestPasswordResetRequestValidatorTests

**Validator:** `RequestPasswordResetRequestValidator`
**Validates:** `RequestPasswordResetRequest`

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenEmailIsValid` | Email | Valid email |
| `Validate_ShouldFail_WhenEmailIsEmpty` | Email | Empty string |
| `Validate_ShouldFail_WhenEmailIsInvalid` | Email | Not a valid email format |
| `Validate_ShouldNotValidateEmailFormat_WhenEmailIsEmpty` | Email | Empty triggers only NotEmpty |

### Step 5: ResetPasswordRequestValidatorTests

**Validator:** `ResetPasswordRequestValidator`
**Validates:** `ResetPasswordRequest`

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Valid token, matching passwords |
| `Validate_ShouldFail_WhenTokenIsEmpty` | Token | Empty string |
| `Validate_ShouldFail_WhenNewPasswordIsEmpty` | NewPassword | Empty string |
| `Validate_ShouldFail_WhenNewPasswordIsTooShort` | NewPassword | 7 characters (min is 8) |
| `Validate_ShouldFail_WhenConfirmPasswordIsEmpty` | ConfirmPassword | Empty string |
| `Validate_ShouldFail_WhenConfirmPasswordDoesNotMatch` | ConfirmPassword | Different from NewPassword |
| `Validate_ShouldPass_WhenPasswordsMatch` | ConfirmPassword | Same as NewPassword |

## Code Patterns to Follow

Use shared builders from `ThePredictions.Tests.Builders` with FluentValidation's `TestValidate()`:

```csharp
public class SomeValidatorTests
{
    private readonly SomeValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        // Arrange
        var request = new SomeRequestBuilder().Build();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenFieldIsInvalid()
    {
        // Arrange
        var request = new SomeRequestBuilder()
            .WithField(invalidValue)
            .Build();

        // Act
        var result = _validator.TestValidate(request);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Field);
    }
}
```

## Verification

- [ ] All tests pass
- [ ] Each validator has a happy-path test (`ShouldPass_WhenAllFieldsAreValid`)
- [ ] Every validation rule has at least one failure test
- [ ] Conditional rules (`.When()`) tested for both active and inactive conditions
- [ ] Name safety rules tested with accented characters (should pass) and HTML characters (should fail)
- [ ] `dotnet build` succeeds with no warnings

## Edge Cases to Consider

- Email validation only runs when email is not empty (`.When()` condition)
- `RefreshTokenRequest.Token` is nullable — validate only triggers when token is not null
- Name validation uses Unicode categories (`\p{L}`, `\p{M}`) — test with accented characters like `Jérôme`, `O'Brien`, `Smith-Jones`
- Password length boundaries: exactly 8 chars should pass, exactly 7 should fail

## Notes

- Authentication validators are high-priority because they guard the entry points to the application.
- The `MustBeASafeName` extension (used for FirstName/LastName) allows letters, spaces, hyphens, apostrophes, and periods.
- The `RegisterRequestValidator` is the most complex in this group with ~15 test cases.
