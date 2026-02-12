# Task: Season Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the `Season` entity factory method, validation (including the custom `InvalidSeasonDuration` guard), `UpdateDetails`, and `SetIsActive`.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/PredictionLeague.Domain.Tests/Models/SeasonTests.cs` | Create | All Season unit tests |

## Implementation Steps

### Step 1: Season.Create factory tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateSeason_WhenValidParametersProvided` | All valid | Properties set correctly |
| `Create_ShouldThrowException_WhenNameIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenNameIsWhitespace` | `" "` | `ArgumentException` |
| `Create_ShouldThrowException_WhenStartDateIsDefault` | `default(DateTime)` | `ArgumentException` |
| `Create_ShouldThrowException_WhenEndDateIsDefault` | `default(DateTime)` | `ArgumentException` |
| `Create_ShouldThrowException_WhenEndDateIsBeforeStartDate` | End < Start | `ArgumentException` |
| `Create_ShouldThrowException_WhenDurationExceedsTenMonths` | 11-month span | `ArgumentException` |
| `Create_ShouldAcceptDurationOfExactlyTenMonths` | 10-month span | No exception |
| `Create_ShouldThrowException_WhenNumberOfRoundsIsZero` | `0` | `ArgumentOutOfRangeException` |
| `Create_ShouldThrowException_WhenNumberOfRoundsExceeds52` | `53` | `ArgumentOutOfRangeException` |
| `Create_ShouldAcceptNumberOfRoundsAtBoundaries` | `1` and `52` | No exception (use `[Theory]`) |
| `Create_ShouldAcceptNullApiLeagueId` | `null` | No exception |

### Step 2: Season.UpdateDetails tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDetails_ShouldUpdateAllProperties_WhenValid` | Valid params | All properties updated |
| `UpdateDetails_ShouldThrowException_WhenNameIsEmpty` | `""` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenDurationExceedsTenMonths` | 11-month span | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenNumberOfRoundsOutOfRange` | `0` or `53` | `ArgumentOutOfRangeException` |

### Step 3: Season.SetIsActive tests

| Test | Scenario | Expected |
|------|----------|----------|
| `SetIsActive_ShouldSetToTrue_WhenCalledWithTrue` | `true` | `IsActive = true` |
| `SetIsActive_ShouldSetToFalse_WhenCalledWithFalse` | `false` | `IsActive = false` |

## Code Patterns to Follow

```csharp
public class SeasonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(52)]
    public void Create_ShouldAcceptNumberOfRoundsAtBoundaries(int numberOfRounds)
    {
        // Arrange
        var start = DateTime.UtcNow.AddDays(1);
        var end = start.AddMonths(5);

        // Act
        var season = Season.Create("Test Season", start, end, true, numberOfRounds, null);

        // Assert
        season.NumberOfRounds.Should().Be(numberOfRounds);
    }
}
```

## Verification

- [ ] All validation paths tested
- [ ] Duration boundary (10 months) tested
- [ ] NumberOfRounds boundaries (1 and 52) tested
- [ ] SetIsActive toggles correctly
- [ ] `dotnet test` passes

## Edge Cases to Consider

- Duration of exactly 10 months (should pass) vs 10 months + 1 day (check if guard uses months or days)
- Start and end date on the same day (should fail — end must be after start)
- NumberOfRounds of -1 (should fail)
