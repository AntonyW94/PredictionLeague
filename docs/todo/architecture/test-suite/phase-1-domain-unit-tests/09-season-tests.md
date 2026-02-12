# Task: Season Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the `Season` entity factory method, validation (including the custom `InvalidSeasonDuration` guard), `UpdateDetails`, and `SetIsActive`.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/SeasonTests.cs` | Create | All Season unit tests |

## Implementation Steps

### Step 1: Season.Create factory tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateSeason_WhenValidParametersProvided` | All valid | Properties set correctly |
| `Create_ShouldSetAllProperties_WhenCreated` | All valid | Name, StartDateUtc, EndDateUtc, IsActive, NumberOfRounds, ApiLeagueId all set |
| `Create_ShouldThrowException_WhenNameIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenNameIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenNameIsWhitespace` | `" "` | `ArgumentException` |
| `Create_ShouldThrowException_WhenStartDateIsDefault` | `default(DateTime)` | `ArgumentException` |
| `Create_ShouldThrowException_WhenEndDateIsDefault` | `default(DateTime)` | `ArgumentException` |
| `Create_ShouldThrowException_WhenEndDateIsBeforeStartDate` | End < Start | `ArgumentException` |
| `Create_ShouldThrowException_WhenEndDateEqualsStartDate` | End == Start | `ArgumentException` |
| `Create_ShouldThrowException_WhenDurationExceedsTenMonths` | 11-month span | `ArgumentException` |
| `Create_ShouldAcceptDurationOfExactlyTenMonths` | 10-month span | No exception |
| `Create_ShouldThrowException_WhenNumberOfRoundsIsZero` | `0` | `ArgumentOutOfRangeException` |
| `Create_ShouldThrowException_WhenNumberOfRoundsIsNegative` | `-1` | `ArgumentOutOfRangeException` |
| `Create_ShouldThrowException_WhenNumberOfRoundsExceeds52` | `53` | `ArgumentOutOfRangeException` |
| `Create_ShouldAcceptNumberOfRoundsAtLowerBoundary` | `1` | No exception |
| `Create_ShouldAcceptNumberOfRoundsAtUpperBoundary` | `52` | No exception |
| `Create_ShouldAcceptNullApiLeagueId` | `null` | No exception |
| `Create_ShouldSetApiLeagueId_WhenProvided` | `apiLeagueId: 42` | `ApiLeagueId = 42` |
| `Create_ShouldSetIsActiveTrue_WhenTrue` | `isActive: true` | `IsActive = true` |
| `Create_ShouldSetIsActiveFalse_WhenFalse` | `isActive: false` | `IsActive = false` |

### Step 2: Season.UpdateDetails tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDetails_ShouldUpdateAllProperties_WhenValid` | Valid params | All properties updated |
| `UpdateDetails_ShouldThrowException_WhenNameIsNull` | `null` | `ArgumentNullException` |
| `UpdateDetails_ShouldThrowException_WhenNameIsEmpty` | `""` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenEndDateBeforeStartDate` | End < Start | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenEndDateEqualsStartDate` | End == Start | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenDurationExceedsTenMonths` | 11-month span | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenStartDateIsDefault` | `default(DateTime)` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenEndDateIsDefault` | `default(DateTime)` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenNumberOfRoundsIsZero` | `0` | `ArgumentOutOfRangeException` |
| `UpdateDetails_ShouldThrowException_WhenNumberOfRoundsExceeds52` | `53` | `ArgumentOutOfRangeException` |
| `UpdateDetails_ShouldAcceptNullApiLeagueId` | `null` | `ApiLeagueId = null` |
| `UpdateDetails_ShouldNotChangeId_WhenUpdating` | Valid params | `Id` unchanged |

### Step 3: Season.SetIsActive tests

| Test | Scenario | Expected |
|------|----------|----------|
| `SetIsActive_ShouldSetToTrue_WhenCalledWithTrue` | `true` | `IsActive = true` |
| `SetIsActive_ShouldSetToFalse_WhenCalledWithFalse` | `false` | `IsActive = false` |
| `SetIsActive_ShouldBeIdempotent_WhenCalledWithSameValue` | `true` then `true` | `IsActive = true` (no error) |

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

- [ ] All validation paths tested (null, empty, whitespace for name)
- [ ] Start/end date validation (default, equal, end before start)
- [ ] Duration boundary (10 months) tested — exactly 10 months passes, over 10 months fails
- [ ] NumberOfRounds boundaries (0, 1, 52, 53, -1) all tested
- [ ] SetIsActive toggles correctly and is idempotent
- [ ] UpdateDetails validates same rules as Create
- [ ] UpdateDetails does not change Id
- [ ] ApiLeagueId nullable handling tested
- [ ] `dotnet test` passes

## Edge Cases to Consider

- Duration of exactly 10 months (should pass) vs 10 months + 1 day (check if guard uses `AddMonths`)
- Start and end date on the same day (should fail — end must be after start)
- NumberOfRounds of -1 (should fail)
- End date exactly at the 10-month boundary (`startDate.AddMonths(10)`) — should fail because guard uses `>`
