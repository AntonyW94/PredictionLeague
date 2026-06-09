# Task 7: Admin User & Account Validator Tests

**Parent Plan:** [Phase 2: Validator Unit Tests](./README.md)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped - admin user and account validator tests are present in `ThePredictions.Validators.Tests.Unit`.

## Goal

Write unit tests for the 3 admin user and account management validators covering user deletion, role updates, and profile details.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Admin/Users/DeleteUserRequestValidatorTests.cs` | Create | User deletion validation tests |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Admin/Users/UpdateUserRoleRequestValidatorTests.cs` | Create | Role update validation tests |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Account/UpdateUserDetailsRequestValidatorTests.cs` | Create | Profile update validation tests |

## Implementation Steps

### Step 1: DeleteUserRequestValidatorTests

**Validator:** `DeleteUserRequestValidator`
**Validates:** `DeleteUserRequest`

This validator has conditional logic — it only validates `NewAdministratorId` when it is not null.

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenNewAdministratorIdIsNull` | NewAdministratorId | Null (no admin transfer needed) |
| `Validate_ShouldPass_WhenNewAdministratorIdIsValid` | NewAdministratorId | Non-empty string |
| `Validate_ShouldFail_WhenNewAdministratorIdIsEmptyButNotNull` | NewAdministratorId | Empty string (not null) |

```csharp
public class DeleteUserRequestValidatorTests
{
    private readonly DeleteUserRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenNewAdministratorIdIsNull()
    {
        var request = new DeleteUserRequestBuilder().Build();
        // Default has NewAdministratorId = null

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNewAdministratorIdIsEmptyButNotNull()
    {
        var request = new DeleteUserRequestBuilder()
            .WithNewAdministratorId("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.NewAdministratorId);
    }
}
```

### Step 2: UpdateUserRoleRequestValidatorTests

**Validator:** `UpdateUserRoleRequestValidator`
**Validates:** `UpdateUserRoleRequest`

This validator uses a custom `.Must()` rule to check if the role is a valid `ApplicationUserRole` enum name.

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenRoleIsValid` | NewRole | Valid enum name (e.g. `"Admin"`, `"User"`) |
| `Validate_ShouldFail_WhenRoleIsEmpty` | NewRole | Empty string |
| `Validate_ShouldFail_WhenRoleIsInvalidValue` | NewRole | e.g. `"SuperAdmin"` (not a valid enum) |
| `Validate_ShouldPass_WhenRoleIsCaseInsensitive` | NewRole | e.g. `"admin"` (lowercase should pass) |
| `Validate_ShouldFail_WhenRoleIsNull` | NewRole | Null value |

To write accurate tests, check the `ApplicationUserRole` enum values in the codebase. Use the actual enum names in the test data.

```csharp
public class UpdateUserRoleRequestValidatorTests
{
    private readonly UpdateUserRoleRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldFail_WhenRoleIsInvalidValue()
    {
        var request = new UpdateUserRoleRequestBuilder()
            .WithNewRole("SuperAdmin")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.NewRole);
    }

    [Fact]
    public void Validate_ShouldPass_WhenRoleIsCaseInsensitive()
    {
        var request = new UpdateUserRoleRequestBuilder()
            .WithNewRole("admin")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
```

### Step 3: UpdateUserDetailsRequestValidatorTests

**Validator:** `UpdateUserDetailsRequestValidator`
**Validates:** `UpdateUserDetailsRequest`

This is the most complex validator in this group, with name safety validation and conditional phone number validation.

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Valid first name, last name, and phone |
| `Validate_ShouldPass_WhenPhoneNumberIsEmpty` | PhoneNumber | Empty string (phone is optional) |
| `Validate_ShouldFail_WhenFirstNameIsEmpty` | FirstName | Empty string |
| `Validate_ShouldFail_WhenFirstNameIsTooShort` | FirstName | 1 character (min is 2) |
| `Validate_ShouldFail_WhenFirstNameIsTooLong` | FirstName | 51 characters (max is 50) |
| `Validate_ShouldFail_WhenFirstNameContainsUnsafeCharacters` | FirstName | e.g. `"John<script>"` |
| `Validate_ShouldPass_WhenFirstNameContainsAccentedCharacters` | FirstName | e.g. `"Jérôme"` |
| `Validate_ShouldPass_WhenFirstNameContainsApostrophe` | FirstName | e.g. `"O'Brien"` |
| `Validate_ShouldPass_WhenFirstNameContainsHyphen` | FirstName | e.g. `"Anne-Marie"` |
| `Validate_ShouldFail_WhenLastNameIsEmpty` | LastName | Empty string |
| `Validate_ShouldFail_WhenLastNameIsTooShort` | LastName | 1 character |
| `Validate_ShouldFail_WhenLastNameIsTooLong` | LastName | 51 characters |
| `Validate_ShouldFail_WhenLastNameContainsUnsafeCharacters` | LastName | XSS characters |
| `Validate_ShouldPass_WhenPhoneNumberIsValidUkMobile` | PhoneNumber | e.g. `"07123456789"` (11 digits, starts with 07) |
| `Validate_ShouldFail_WhenPhoneNumberDoesNotStartWith07` | PhoneNumber | e.g. `"08123456789"` |
| `Validate_ShouldFail_WhenPhoneNumberIsTooShort` | PhoneNumber | e.g. `"0712345678"` (10 digits) |
| `Validate_ShouldFail_WhenPhoneNumberIsTooLong` | PhoneNumber | e.g. `"071234567890"` (12 digits) |
| `Validate_ShouldFail_WhenPhoneNumberContainsLetters` | PhoneNumber | e.g. `"07123abcdef"` |

```csharp
public class UpdateUserDetailsRequestValidatorTests
{
    private readonly UpdateUserDetailsRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenPhoneNumberIsEmpty()
    {
        var request = new UpdateUserDetailsRequestBuilder()
            .WithPhoneNumber("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenPhoneNumberDoesNotStartWith07()
    {
        var request = new UpdateUserDetailsRequestBuilder()
            .WithPhoneNumber("08123456789")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PhoneNumber);
    }
}
```

## Code Patterns to Follow

Use shared builders from `ThePredictions.Tests.Builders`. For testing enum-based validation with `.Must()`, check actual enum values:

```csharp
[Theory]
[InlineData("Admin")]
[InlineData("User")]
public void Validate_ShouldPass_WhenRoleIsValid(string role)
{
    var request = new UpdateUserRoleRequestBuilder()
        .WithNewRole(role)
        .Build();

    var result = _validator.TestValidate(request);

    result.ShouldNotHaveAnyValidationErrors();
}
```

## Verification

- [ ] All tests pass
- [ ] `DeleteUserRequest` conditional validation tested (null vs empty)
- [ ] `UpdateUserRole` enum parsing tested (valid names, invalid names, case insensitivity)
- [ ] Name safety rules tested with accented characters, hyphens, apostrophes, and XSS characters
- [ ] UK mobile phone regex `^07\d{9}$` tested with valid and invalid numbers
- [ ] Phone number conditional validation tested (empty string skips validation)
- [ ] `dotnet build` succeeds with no warnings

## Edge Cases to Consider

- `DeleteUserRequest.NewAdministratorId` is nullable — null is valid (no transfer), empty string is invalid
- `UpdateUserRoleRequest.NewRole` uses `Enum.TryParse` with `ignoreCase: true` — case insensitive
- Phone number regex `^07\d{9}$` requires exactly 11 digits starting with `07` — no spaces, dashes, or country codes
- Phone number validation only runs when the phone number is not empty (`.When()` condition)
- Name validation uses the `MustBeASafeName` extension (person name rules) — allows `\p{L}`, `\p{M}`, `'`, `-`, `\s`, `.`
- Whitespace-only phone numbers like `"   "` may or may not match the regex — test this edge case

## Notes

- The `DeleteUserRequestValidator` is the simplest validator with only one conditional rule.
- The `UpdateUserRoleRequestValidator` uses a custom `.Must()` with `Enum.TryParse` rather than `.IsInEnum()` because the request uses a string, not an enum.
- The `UpdateUserDetailsRequestValidator` shares the same name rules as `RegisterRequestValidator` (both use `MustBeASafeName`).
- Check the `ApplicationUserRole` enum in the codebase to use the correct values in tests.
