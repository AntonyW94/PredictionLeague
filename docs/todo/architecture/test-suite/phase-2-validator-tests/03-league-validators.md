# Task 3: League Validator Tests

**Parent Plan:** [Phase 2: Validator Unit Tests](./README.md)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped - league validator tests are present in `ThePredictions.Validators.Tests.Unit`.

## Goal

Write unit tests for all 4 league validators covering league creation, updates, prize structure definition, and league joining.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Leagues/CreateLeagueRequestValidatorTests.cs` | Create | League creation validation tests |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Leagues/UpdateLeagueRequestValidatorTests.cs` | Create | League update validation tests |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Leagues/DefinePrizeStructureRequestValidatorTests.cs` | Create | Prize structure validation tests |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Leagues/JoinLeagueRequestValidatorTests.cs` | Create | League joining validation tests |

## Implementation Steps

### Step 1: CreateLeagueRequestValidatorTests

**Validator:** `CreateLeagueRequestValidator`
**Validates:** `CreateLeagueRequest`

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Fully valid request |
| `Validate_ShouldFail_WhenNameIsEmpty` | Name | Empty string |
| `Validate_ShouldFail_WhenNameIsTooShort` | Name | 2 characters (min is 3) |
| `Validate_ShouldFail_WhenNameIsTooLong` | Name | 101 characters (max is 100) |
| `Validate_ShouldFail_WhenNameContainsHtmlTags` | Name | e.g. `"<script>alert</script>"` |
| `Validate_ShouldPass_WhenNameContainsAllowedPunctuation` | Name | e.g. `"Smith's League (2024-25)!"` |
| `Validate_ShouldFail_WhenSeasonIdIsZero` | SeasonId | Value 0 |
| `Validate_ShouldFail_WhenSeasonIdIsNegative` | SeasonId | Negative number |
| `Validate_ShouldFail_WhenPriceIsNegative` | Price | Value -1 |
| `Validate_ShouldFail_WhenPriceExceeds10000` | Price | Value 10001 |
| `Validate_ShouldPass_WhenPriceIsZero` | Price | Value 0 (free league) |
| `Validate_ShouldPass_WhenPriceIs10000` | Price | Value 10000 (boundary) |
| `Validate_ShouldFail_WhenEntryDeadlineIsInThePast` | EntryDeadlineUtc | Past date |
| `Validate_ShouldFail_WhenPointsForExactScoreIsZero` | PointsForExactScore | Value 0 (min is 1) |
| `Validate_ShouldFail_WhenPointsForExactScoreExceeds100` | PointsForExactScore | Value 101 |
| `Validate_ShouldFail_WhenPointsForCorrectResultIsZero` | PointsForCorrectResult | Value 0 |
| `Validate_ShouldFail_WhenPointsForCorrectResultExceeds100` | PointsForCorrectResult | Value 101 |

### Step 2: UpdateLeagueRequestValidatorTests

**Validator:** `UpdateLeagueRequestValidator`
**Validates:** `UpdateLeagueRequest`

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Fully valid request |
| `Validate_ShouldFail_WhenNameIsEmpty` | Name | Empty string |
| `Validate_ShouldFail_WhenNameIsTooShort` | Name | 2 characters (min is 3) |
| `Validate_ShouldFail_WhenNameIsTooLong` | Name | 101 characters |
| `Validate_ShouldFail_WhenNameContainsHtmlTags` | Name | XSS characters |
| `Validate_ShouldFail_WhenPriceIsNegative` | Price | Value -1 |
| `Validate_ShouldFail_WhenPriceExceeds10000` | Price | Value 10001 |
| `Validate_ShouldFail_WhenEntryDeadlineIsInThePast` | EntryDeadlineUtc | Past date |
| `Validate_ShouldFail_WhenPointsForExactScoreIsZero` | PointsForExactScore | Value 0 |
| `Validate_ShouldFail_WhenPointsForExactScoreExceeds100` | PointsForExactScore | Value 101 |
| `Validate_ShouldFail_WhenPointsForCorrectResultIsZero` | PointsForCorrectResult | Value 0 |
| `Validate_ShouldFail_WhenPointsForCorrectResultExceeds100` | PointsForCorrectResult | Value 101 |

**Note:** `UpdateLeagueRequest` does not have `SeasonId` — that is immutable after creation.

### Step 3: DefinePrizeStructureRequestValidatorTests

**Validator:** `DefinePrizeStructureRequestValidator` (with nested `DefinePrizeSettingDtoValidator`)
**Validates:** `DefinePrizeStructureRequest` containing a collection of `DefinePrizeSettingDto`

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenPrizeSettingsAreValid` | PrizeSettings | Collection of valid settings |
| `Validate_ShouldPass_WhenPrizeSettingsIsEmpty` | PrizeSettings | Empty collection (clearing prizes) |
| `Validate_ShouldFail_WhenPrizeTypeIsInvalid` | PrizeSettings[].PrizeType | Invalid enum value |
| `Validate_ShouldFail_WhenPrizeAmountIsNegative` | PrizeSettings[].PrizeAmount | Value -1 |
| `Validate_ShouldPass_WhenPrizeAmountIsZero` | PrizeSettings[].PrizeAmount | Value 0 |
| `Validate_ShouldFail_WhenRankIsZero` | PrizeSettings[].Rank | Value 0 |
| `Validate_ShouldFail_WhenMultiplierIsZero` | PrizeSettings[].Multiplier | Value 0 |
| `Validate_ShouldFail_WhenPrizeDescriptionExceeds200Characters` | PrizeSettings[].PrizeDescription | 201 characters |
| `Validate_ShouldPass_WhenPrizeDescriptionIsEmpty` | PrizeSettings[].PrizeDescription | Empty string (conditional) |

### Step 4: JoinLeagueRequestValidatorTests

**Validator:** `JoinLeagueRequestValidator`
**Validates:** `JoinLeagueRequest`

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenEntryCodeIsValid` | EntryCode | 6 alphanumeric characters e.g. `"ABC123"` |
| `Validate_ShouldFail_WhenEntryCodeIsEmpty` | EntryCode | Empty string |
| `Validate_ShouldFail_WhenEntryCodeIsTooShort` | EntryCode | 5 characters |
| `Validate_ShouldFail_WhenEntryCodeIsTooLong` | EntryCode | 7 characters |
| `Validate_ShouldFail_WhenEntryCodeContainsSpecialCharacters` | EntryCode | e.g. `"ABC!@#"` |
| `Validate_ShouldPass_WhenEntryCodeIsLowercase` | EntryCode | e.g. `"abc123"` (regex is case insensitive) |

## Code Patterns to Follow

Use shared builders from `ThePredictions.Tests.Builders`. For testing boundary values, use `[Theory]` with `[InlineData]`:

```csharp
[Theory]
[InlineData(0)]
[InlineData(101)]
public void Validate_ShouldFail_WhenPointsForExactScoreIsOutOfRange(int points)
{
    var request = new CreateLeagueRequestBuilder()
        .WithPointsForExactScore(points)
        .Build();

    var result = _validator.TestValidate(request);

    result.ShouldHaveValidationErrorFor(x => x.PointsForExactScore);
}
```

The builder provides valid defaults — each test only overrides the property under test:

```csharp
// Happy path — builder defaults are all valid
var request = new CreateLeagueRequestBuilder().Build();

// Failure test — override one property
var request = new CreateLeagueRequestBuilder()
    .WithName("")
    .Build();

// Multiple overrides for related tests
var request = new CreateLeagueRequestBuilder()
    .WithPrice(-1)
    .Build();
```

## Verification

- [ ] All tests pass
- [ ] Every validation rule has at least one test
- [ ] Boundary values tested for numeric ranges (Price 0, 10000, PointsForExactScore 1, 100)
- [ ] League name safety tested (allowed punctuation, XSS rejection)
- [ ] Nested validator (DefinePrizeSettingDto) rules individually tested
- [ ] JoinLeague entry code regex tested with valid, invalid, and edge cases
- [ ] `dotnet build` succeeds with no warnings

## Edge Cases to Consider

- `CreateLeagueRequest.EntryDeadlineUtc` compared to `DateTime.UtcNow` — tests may need a future date far enough ahead to avoid flakiness
- `Price` boundary: 0 is valid (free league), 10000 is valid (max), -0.01 and 10000.01 should fail
- `PointsForExactScore` and `PointsForCorrectResult` use `InclusiveBetween(1, 100)` — both boundaries are valid
- `JoinLeagueRequest.EntryCode` regex `^[A-Z0-9]{6}$` is case insensitive — lowercase should pass
- `DefinePrizeStructureRequest` allows an empty `PrizeSettings` collection (removing all prizes)

## Notes

- League validators are the most business-critical validators as they guard league creation and modification.
- The `MustBeASafeLeagueName` extension allows letters, numbers, spaces, and common punctuation but rejects HTML/script characters.
- `CreateLeagueRequest` has `SeasonId` but `UpdateLeagueRequest` does not.
