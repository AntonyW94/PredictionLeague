# Task 6: Admin Season & Team Validator Tests

**Parent Plan:** [Phase 2: Validator Unit Tests](./README.md)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped - admin season and team validator tests are present in `ThePredictions.Validators.Tests.Unit`.

## Goal

Write unit tests for the 4 admin season and team validators, including testing inherited base rules through both concrete implementations for each.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Admin/Seasons/CreateSeasonRequestValidatorTests.cs` | Create | Create season validation tests (includes base rules) |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Admin/Seasons/UpdateSeasonRequestValidatorTests.cs` | Create | Update season validation tests (includes base rules) |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Admin/Teams/CreateTeamRequestValidatorTests.cs` | Create | Create team validation tests (includes base rules) |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Admin/Teams/UpdateTeamRequestValidatorTests.cs` | Create | Update team validation tests (includes base rules) |

## Implementation Steps

### Step 1: CreateSeasonRequestValidatorTests

**Validator:** `CreateSeasonRequestValidator` (inherits `BaseSeasonRequestValidator<CreateSeasonRequest>`)
**Validates:** `CreateSeasonRequest`

Tests all rules from `BaseSeasonRequestValidator`:

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Fully valid season |
| `Validate_ShouldFail_WhenNameIsEmpty` | Name | Empty string |
| `Validate_ShouldFail_WhenNameIsTooShort` | Name | 3 characters (min is 4) |
| `Validate_ShouldFail_WhenNameIsTooLong` | Name | 51 characters (max is 50) |
| `Validate_ShouldFail_WhenNameContainsHtmlTags` | Name | e.g. `"<b>Season</b>"` |
| `Validate_ShouldPass_WhenNameContainsAllowedPunctuation` | Name | e.g. `"Premier League 2024-25"` |
| `Validate_ShouldNotValidateNameFormat_WhenNameIsEmpty` | Name | Empty triggers only NotEmpty |
| `Validate_ShouldFail_WhenStartDateUtcIsDefault` | StartDateUtc | `default(DateTime)` |
| `Validate_ShouldFail_WhenEndDateUtcIsDefault` | EndDateUtc | `default(DateTime)` |
| `Validate_ShouldFail_WhenEndDateIsBeforeStartDate` | EndDateUtc | Earlier than StartDateUtc |
| `Validate_ShouldFail_WhenNumberOfRoundsIsZero` | NumberOfRounds | Value 0 (min is 1) |
| `Validate_ShouldFail_WhenNumberOfRoundsExceeds52` | NumberOfRounds | Value 53 (max is 52) |
| `Validate_ShouldPass_WhenNumberOfRoundsIs1` | NumberOfRounds | Value 1 (lower boundary) |
| `Validate_ShouldPass_WhenNumberOfRoundsIs52` | NumberOfRounds | Value 52 (upper boundary) |

### Step 2: UpdateSeasonRequestValidatorTests

**Validator:** `UpdateSeasonRequestValidator` (inherits `BaseSeasonRequestValidator<UpdateSeasonRequest>`)
**Validates:** `UpdateSeasonRequest`

Tests the same base rules through `UpdateSeasonRequestValidator`:

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Fully valid update |
| `Validate_ShouldFail_WhenNameIsEmpty` | Name | Empty string |
| `Validate_ShouldFail_WhenNameIsTooShort` | Name | 3 characters |
| `Validate_ShouldFail_WhenNameIsTooLong` | Name | 51 characters |
| `Validate_ShouldFail_WhenNameContainsHtmlTags` | Name | XSS characters |
| `Validate_ShouldFail_WhenStartDateUtcIsDefault` | StartDateUtc | `default(DateTime)` |
| `Validate_ShouldFail_WhenEndDateUtcIsDefault` | EndDateUtc | `default(DateTime)` |
| `Validate_ShouldFail_WhenEndDateIsBeforeStartDate` | EndDateUtc | Earlier than StartDateUtc |
| `Validate_ShouldFail_WhenNumberOfRoundsIsZero` | NumberOfRounds | Value 0 |
| `Validate_ShouldFail_WhenNumberOfRoundsExceeds52` | NumberOfRounds | Value 53 |

### Step 3: CreateTeamRequestValidatorTests

**Validator:** `CreateTeamRequestValidator` (inherits `BaseTeamRequestValidator<CreateTeamRequest>`)
**Validates:** `CreateTeamRequest`

Tests all rules from `BaseTeamRequestValidator`:

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Fully valid team |
| `Validate_ShouldFail_WhenNameIsEmpty` | Name | Empty string |
| `Validate_ShouldFail_WhenNameIsTooShort` | Name | 1 character (min is 2) |
| `Validate_ShouldFail_WhenNameIsTooLong` | Name | 101 characters (max is 100) |
| `Validate_ShouldFail_WhenNameContainsHtmlTags` | Name | e.g. `"<script>Team</script>"` |
| `Validate_ShouldPass_WhenNameContainsAllowedPunctuation` | Name | e.g. `"AFC Bournemouth"` |
| `Validate_ShouldNotValidateNameFormat_WhenNameIsEmpty` | Name | Empty triggers only NotEmpty |
| `Validate_ShouldFail_WhenShortNameIsEmpty` | ShortName | Empty string |
| `Validate_ShouldFail_WhenShortNameIsTooShort` | ShortName | 1 character (min is 2) |
| `Validate_ShouldFail_WhenShortNameIsTooLong` | ShortName | 51 characters (max is 50) |
| `Validate_ShouldFail_WhenShortNameContainsHtmlTags` | ShortName | XSS characters |
| `Validate_ShouldNotValidateShortNameFormat_WhenShortNameIsEmpty` | ShortName | Empty triggers only NotEmpty |
| `Validate_ShouldFail_WhenLogoUrlIsEmpty` | LogoUrl | Empty string |
| `Validate_ShouldFail_WhenLogoUrlIsInvalid` | LogoUrl | e.g. `"not-a-url"` |
| `Validate_ShouldPass_WhenLogoUrlIsValidHttps` | LogoUrl | e.g. `"https://example.com/logo.png"` |
| `Validate_ShouldPass_WhenLogoUrlIsValidHttp` | LogoUrl | e.g. `"http://example.com/logo.png"` |
| `Validate_ShouldNotValidateLogoUrlFormat_WhenLogoUrlIsEmpty` | LogoUrl | Empty triggers only NotEmpty |
| `Validate_ShouldFail_WhenAbbreviationIsEmpty` | Abbreviation | Empty string |
| `Validate_ShouldFail_WhenAbbreviationIsNot3Characters` | Abbreviation | 2 or 4 characters |
| `Validate_ShouldPass_WhenAbbreviationIsExactly3Characters` | Abbreviation | e.g. `"MUN"` |
| `Validate_ShouldNotValidateAbbreviationLength_WhenAbbreviationIsEmpty` | Abbreviation | Empty triggers only NotEmpty |

### Step 4: UpdateTeamRequestValidatorTests

**Validator:** `UpdateTeamRequestValidator` (inherits `BaseTeamRequestValidator<UpdateTeamRequest>`)
**Validates:** `UpdateTeamRequest`

Tests the same base rules through `UpdateTeamRequestValidator`:

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Fully valid update |
| `Validate_ShouldFail_WhenNameIsEmpty` | Name | Empty string |
| `Validate_ShouldFail_WhenNameIsTooShort` | Name | 1 character |
| `Validate_ShouldFail_WhenNameIsTooLong` | Name | 101 characters |
| `Validate_ShouldFail_WhenNameContainsHtmlTags` | Name | XSS characters |
| `Validate_ShouldFail_WhenShortNameIsEmpty` | ShortName | Empty string |
| `Validate_ShouldFail_WhenShortNameIsTooShort` | ShortName | 1 character |
| `Validate_ShouldFail_WhenShortNameIsTooLong` | ShortName | 51 characters |
| `Validate_ShouldFail_WhenLogoUrlIsEmpty` | LogoUrl | Empty string |
| `Validate_ShouldFail_WhenLogoUrlIsInvalid` | LogoUrl | Not a valid URL |
| `Validate_ShouldFail_WhenAbbreviationIsEmpty` | Abbreviation | Empty string |
| `Validate_ShouldFail_WhenAbbreviationIsNot3Characters` | Abbreviation | Wrong length |

## Code Patterns to Follow

Use shared builders from `ThePredictions.Tests.Builders`:

```csharp
public class CreateTeamRequestValidatorTests
{
    private readonly CreateTeamRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new CreateTeamRequestBuilder().Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        var request = new CreateTeamRequestBuilder()
            .WithName("")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_ShouldFail_WhenLogoUrlIsInvalid()
    {
        var request = new CreateTeamRequestBuilder()
            .WithLogoUrl("not-a-url")
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.LogoUrl);
    }
}
```

## Verification

- [ ] All tests pass
- [ ] Base season rules tested through both `CreateSeasonRequestValidator` and `UpdateSeasonRequestValidator`
- [ ] Base team rules tested through both `CreateTeamRequestValidator` and `UpdateTeamRequestValidator`
- [ ] Name safety tested (safe name extension with `MustBeASafeName("field description")`)
- [ ] URL validation tested (valid HTTP/HTTPS, invalid strings)
- [ ] Abbreviation exact-length rule tested (exactly 3 characters required)
- [ ] `.When()` conditions tested — format rules skip when field is empty
- [ ] Boundary values tested for NumberOfRounds (1 and 52)
- [ ] `dotnet build` succeeds with no warnings

## Edge Cases to Consider

- Season name uses `MustBeASafeName("Season name")` — the custom field description affects the error message
- Team name uses `MustBeASafeName("Team name")` — different field description
- ShortName uses `MustBeASafeName("Short name")` — another field description
- All name/format rules have `.When(x => !string.IsNullOrEmpty(x.Field))` — when the field is empty, only NotEmpty should fire (not format/length rules)
- LogoUrl uses a custom `BeAValidUrl` method (`Uri.TryCreate` with `UriKind.Absolute`) — both HTTP and HTTPS are valid
- Abbreviation length is exactly 3 — not a range but an exact match. Both 2 and 4 should fail
- `EndDateUtc` equal to `StartDateUtc` should fail (rule is GreaterThan, not GreaterThanOrEqual)

## Notes

- The `BaseTeamRequestValidator` is the most complex base validator with 4 properties and multiple rules per property.
- URL validation is lenient — it only checks `Uri.TryCreate` which accepts any absolute URI scheme, not just HTTP/HTTPS.
- The `.When()` conditions are important to test: they prevent cascading errors when a required field is empty.
