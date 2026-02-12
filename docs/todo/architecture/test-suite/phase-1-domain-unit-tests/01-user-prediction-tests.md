# Task: UserPrediction Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the core scoring algorithm (`SetOutcome`) and factory method (`Create`) of the `UserPrediction` entity — the most critical business logic in the system.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/UserPredictionTests.cs` | Create | All UserPrediction unit tests |

## Prerequisites

All tests in this file require a `FakeDateTimeProvider` instance. Create one at a fixed point in time for deterministic results:

```csharp
private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
```

## Implementation Steps

### Step 1: Create factory method tests

Test `UserPrediction.Create(userId, matchId, homeScore, awayScore, dateTimeProvider)`:

| Test | Input | Expected |
|------|-------|----------|
| `Create_ShouldCreatePrediction_WhenValidParametersProvided` | `("user-1", 1, 2, 1)` | Properties set correctly, Outcome = Pending |
| `Create_ShouldSetCreatedAtUtcAndUpdatedAtUtc_WhenCreated` | `("user-1", 1, 2, 1)` | Both timestamps match `dateTimeProvider.UtcNow` exactly |
| `Create_ShouldThrowException_WhenUserIdIsNull` | `(null, 1, 2, 1)` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenUserIdIsEmpty` | `("", 1, 2, 1)` | `ArgumentException` |
| `Create_ShouldThrowException_WhenUserIdIsWhitespace` | `(" ", 1, 2, 1)` | `ArgumentException` |
| `Create_ShouldThrowException_WhenMatchIdIsZero` | `("user-1", 0, 2, 1)` | `ArgumentException` |
| `Create_ShouldThrowException_WhenMatchIdIsNegative` | `("user-1", -1, 2, 1)` | `ArgumentException` |
| `Create_ShouldThrowException_WhenHomeScoreIsNegative` | `("user-1", 1, -1, 1)` | `ArgumentException` |
| `Create_ShouldThrowException_WhenAwayScoreIsNegative` | `("user-1", 1, 2, -1)` | `ArgumentException` |
| `Create_ShouldAllowZeroScores_WhenBothScoresAreZero` | `("user-1", 1, 0, 0)` | Created successfully |
| `Create_ShouldAllowHighScores_WhenLargeScoresProvided` | `("user-1", 1, 9, 0)` | Created successfully |

### Step 2: Create SetOutcome tests — Pending scenarios

Test `SetOutcome(MatchStatus status, int? actualHomeScore, int? actualAwayScore)`:

| Test | Status | Actual Scores | Expected |
|------|--------|---------------|----------|
| `SetOutcome_ShouldReturnPending_WhenStatusIsScheduled` | Scheduled | (2, 1) | Pending |
| `SetOutcome_ShouldReturnPending_WhenHomeScoreIsNull` | Completed | (null, 1) | Pending |
| `SetOutcome_ShouldReturnPending_WhenAwayScoreIsNull` | Completed | (2, null) | Pending |
| `SetOutcome_ShouldReturnPending_WhenBothScoresAreNull` | Completed | (null, null) | Pending |

### Step 3: Create SetOutcome tests — ExactScore scenarios

| Test | Predicted | Actual | Expected |
|------|-----------|--------|----------|
| `SetOutcome_ShouldReturnExactScore_WhenPredictionMatchesExactly` | (2, 1) | (2, 1) | ExactScore |
| `SetOutcome_ShouldReturnExactScore_WhenBothTeamsScoreZero` | (0, 0) | (0, 0) | ExactScore |
| `SetOutcome_ShouldReturnExactScore_WhenHighScoringDraw` | (3, 3) | (3, 3) | ExactScore |
| `SetOutcome_ShouldReturnExactScore_WhenAwayWinMatchesExactly` | (0, 2) | (0, 2) | ExactScore |
| `SetOutcome_ShouldReturnExactScore_WhenHighScoringMatchMatchesExactly` | (5, 4) | (5, 4) | ExactScore |

### Step 4: Create SetOutcome tests — CorrectResult scenarios

The scoring uses `Math.Sign(home - away)` to compare results. Test all three result types:

| Test | Predicted | Actual | Expected |
|------|-----------|--------|----------|
| `SetOutcome_ShouldReturnCorrectResult_WhenHomeWinPredictedCorrectly` | (3, 1) | (2, 0) | CorrectResult |
| `SetOutcome_ShouldReturnCorrectResult_WhenAwayWinPredictedCorrectly` | (0, 2) | (1, 3) | CorrectResult |
| `SetOutcome_ShouldReturnCorrectResult_WhenDrawPredictedCorrectly` | (1, 1) | (0, 0) | CorrectResult |
| `SetOutcome_ShouldReturnCorrectResult_WhenDrawWithDifferentScores` | (0, 0) | (2, 2) | CorrectResult |

### Step 5: Create SetOutcome tests — Incorrect scenarios

The `Math.Sign` logic creates a 3×3 matrix of predicted vs actual outcomes. There are 6 Incorrect combinations (all mismatches):

| Test | Predicted | Actual | Expected |
|------|-----------|--------|----------|
| `SetOutcome_ShouldReturnIncorrect_WhenHomeWinPredictedButAwayWon` | (2, 1) | (0, 3) | Incorrect |
| `SetOutcome_ShouldReturnIncorrect_WhenHomeWinPredictedButDraw` | (2, 1) | (1, 1) | Incorrect |
| `SetOutcome_ShouldReturnIncorrect_WhenDrawPredictedButHomeWon` | (1, 1) | (2, 0) | Incorrect |
| `SetOutcome_ShouldReturnIncorrect_WhenDrawPredictedButAwayWon` | (1, 1) | (0, 2) | Incorrect |
| `SetOutcome_ShouldReturnIncorrect_WhenAwayWinPredictedButHomeWon` | (0, 2) | (3, 0) | Incorrect |
| `SetOutcome_ShouldReturnIncorrect_WhenAwayWinPredictedButDraw` | (0, 2) | (1, 1) | Incorrect |

### Step 6: Create SetOutcome tests — State update verification

| Test | Scenario | Expected |
|------|----------|----------|
| `SetOutcome_ShouldUpdateUpdatedAtUtc_WhenCalled` | Advance `FakeDateTimeProvider.UtcNow` before calling | `UpdatedAtUtc` matches the advanced time exactly |

## Code Patterns to Follow

```csharp
public class UserPredictionTests
{
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Create_ShouldCreatePrediction_WhenValidParametersProvided()
    {
        // Act
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);

        // Assert
        prediction.UserId.Should().Be("user-1");
        prediction.MatchId.Should().Be(1);
        prediction.PredictedHomeScore.Should().Be(2);
        prediction.PredictedAwayScore.Should().Be(1);
        prediction.Outcome.Should().Be(PredictionOutcome.Pending);
        prediction.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
        prediction.UpdatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void SetOutcome_ShouldReturnExactScore_WhenPredictionMatchesExactly()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 2, 1, 5, 3, _dateTimeProvider);

        // Assert
        prediction.Outcome.Should().Be(PredictionOutcome.ExactScore);
    }

    [Fact]
    public void SetOutcome_ShouldUpdateUpdatedAtUtc_WhenCalled()
    {
        // Arrange
        var prediction = UserPrediction.Create("user-1", 1, 2, 1, _dateTimeProvider);
        var laterTime = new DateTime(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        _dateTimeProvider.UtcNow = laterTime;

        // Act
        prediction.SetOutcome(MatchStatus.Completed, 2, 1, 5, 3, _dateTimeProvider);

        // Assert
        prediction.UpdatedAtUtc.Should().Be(laterTime);
    }
}
```

## Verification

- [ ] All factory validation tests pass (null, empty, whitespace, negative, zero boundary)
- [ ] All Pending scenarios covered (Scheduled status, null home score, null away score, both null)
- [ ] All ExactScore scenarios covered (home win, away win, draw, 0-0, high-scoring)
- [ ] All CorrectResult scenarios covered (home win, away win, draw with different scores)
- [ ] All 6 Incorrect scenarios covered (full 3×3 matrix of predicted vs actual mismatches)
- [ ] Timestamps verified (CreatedAtUtc, UpdatedAtUtc set and updated)
- [ ] `dotnet test` passes

## Edge Cases to Consider

- The `Math.Sign` logic: `Sign(0) == 0`, so a predicted draw (any score) matches any actual draw
- Zero scores are valid (0-0 is a common result)
- Very high scores (e.g., 9-0) should still work correctly
- `SetOutcome` always updates `UpdatedAtUtc`, even when outcome doesn't change
- With `FakeDateTimeProvider`, timestamps can be asserted exactly — no "approximately now" needed
