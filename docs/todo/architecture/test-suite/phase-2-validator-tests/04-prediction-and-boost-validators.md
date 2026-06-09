# Task 4: Prediction & Boost Validator Tests

**Parent Plan:** [Phase 2: Validator Unit Tests](./README.md)

## Status

Not Started | In Progress | **Complete**

> **Verified June 2026:** Shipped - prediction and boost validator tests are present in `ThePredictions.Validators.Tests.Unit`.

## Goal

Write unit tests for the 3 validators covering prediction submission and boost application.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Predictions/SubmitPredictionsRequestValidatorTests.cs` | Create | Batch prediction submission tests |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Predictions/PredictionSubmissionDtoValidatorTests.cs` | Create | Individual prediction validation tests |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Boosts/ApplyBoostRequestValidatorTests.cs` | Create | Boost application validation tests |

## Implementation Steps

### Step 1: SubmitPredictionsRequestValidatorTests

**Validator:** `SubmitPredictionsRequestValidator`
**Validates:** `SubmitPredictionsRequest`

This validator validates the outer request and delegates to `PredictionSubmissionDtoValidator` for each prediction in the collection.

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Valid RoundId and at least one valid prediction |
| `Validate_ShouldFail_WhenRoundIdIsZero` | RoundId | Value 0 |
| `Validate_ShouldFail_WhenRoundIdIsNegative` | RoundId | Negative number |
| `Validate_ShouldFail_WhenPredictionsIsEmpty` | Predictions | Empty collection |
| `Validate_ShouldFail_WhenPredictionsIsNull` | Predictions | Null collection |
| `Validate_ShouldFail_WhenChildPredictionIsInvalid` | Predictions[] | Collection with an invalid MatchId (tests nested validator runs) |

```csharp
public class SubmitPredictionsRequestValidatorTests
{
    private readonly SubmitPredictionsRequestValidator _validator = new();

    [Fact]
    public void Validate_ShouldPass_WhenAllFieldsAreValid()
    {
        var request = new SubmitPredictionsRequestBuilder().Build();

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ShouldFail_WhenRoundIdIsZero()
    {
        var request = new SubmitPredictionsRequestBuilder()
            .WithRoundId(0)
            .Build();

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.RoundId);
    }
}
```

### Step 2: PredictionSubmissionDtoValidatorTests

**Validator:** `PredictionSubmissionDtoValidator`
**Validates:** `PredictionSubmissionDto`

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Valid MatchId and scores |
| `Validate_ShouldFail_WhenMatchIdIsZero` | MatchId | Value 0 |
| `Validate_ShouldFail_WhenMatchIdIsNegative` | MatchId | Negative number |
| `Validate_ShouldFail_WhenHomeScoreIsNegative` | HomeScore | Value -1 |
| `Validate_ShouldFail_WhenHomeScoreExceedsNine` | HomeScore | Value 10 |
| `Validate_ShouldFail_WhenAwayScoreIsNegative` | AwayScore | Value -1 |
| `Validate_ShouldFail_WhenAwayScoreExceedsNine` | AwayScore | Value 10 |
| `Validate_ShouldPass_WhenScoresAreZero` | HomeScore/AwayScore | Both 0 (valid 0-0 prediction) |
| `Validate_ShouldPass_WhenScoresAreNine` | HomeScore/AwayScore | Both 9 (max boundary) |

### Step 3: ApplyBoostRequestValidatorTests

**Validator:** `ApplyBoostRequestValidator`
**Validates:** `ApplyBoostRequest`

| Test | Property | Scenario |
|------|----------|----------|
| `Validate_ShouldPass_WhenAllFieldsAreValid` | All | Valid LeagueId, RoundId, and BoostCode |
| `Validate_ShouldFail_WhenLeagueIdIsZero` | LeagueId | Value 0 |
| `Validate_ShouldFail_WhenLeagueIdIsNegative` | LeagueId | Negative number |
| `Validate_ShouldFail_WhenRoundIdIsZero` | RoundId | Value 0 |
| `Validate_ShouldFail_WhenRoundIdIsNegative` | RoundId | Negative number |
| `Validate_ShouldFail_WhenBoostCodeIsEmpty` | BoostCode | Empty string |
| `Validate_ShouldFail_WhenBoostCodeExceeds50Characters` | BoostCode | 51 characters |
| `Validate_ShouldPass_WhenBoostCodeIsExactly50Characters` | BoostCode | 50 characters (boundary) |

## Code Patterns to Follow

Use shared builders from `ThePredictions.Tests.Builders`. For testing score boundaries, `[Theory]` with `[InlineData]` is effective:

```csharp
[Theory]
[InlineData(-1)]
[InlineData(10)]
public void Validate_ShouldFail_WhenHomeScoreIsOutOfRange(int score)
{
    var dto = new PredictionSubmissionDtoBuilder()
        .WithHomeScore(score)
        .Build();

    var result = _validator.TestValidate(dto);

    result.ShouldHaveValidationErrorFor(x => x.HomeScore);
}
```

## Verification

- [ ] All tests pass
- [ ] Nested validator delegation tested (invalid child prediction fails parent)
- [ ] Score boundaries tested (0 and 9 pass, -1 and 10 fail)
- [ ] ID validation tested (0 and negative values fail, positive values pass)
- [ ] BoostCode length boundary tested (50 passes, 51 fails)
- [ ] `dotnet build` succeeds with no warnings

## Edge Cases to Consider

- `SubmitPredictionsRequest.Predictions` as null vs empty — both should fail
- Score 0 is a valid prediction (0-0 draw)
- Score 9 is the maximum allowed score
- `BoostCode` exactly 50 characters is valid (boundary)
- The nested validator runs per item — one invalid prediction in a list of valid ones should still fail

## Notes

- These validators guard the core gameplay functionality (submitting predictions and applying boosts).
- The score range 0-9 is a deliberate business rule — football scores rarely exceed 9.
- The `SubmitPredictionsRequestValidator` uses `ForEach` with `SetValidator` to validate each `PredictionSubmissionDto` in the collection.
