# Task: BoostEligibilityEvaluator Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the `BoostEligibilityEvaluator.Evaluate()` static method — the most complex domain logic with 12+ conditional branches covering eligibility checks, window-based limits, and remaining use calculations.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Services/Boosts/BoostEligibilityEvaluatorTests.cs` | Create | All evaluator tests |

## Implementation Steps

### Step 1: Create early-exit rejection tests

These test the guard conditions at the top of `Evaluate()`:

| Test | Parameters | Expected |
|------|-----------|----------|
| `Evaluate_ShouldReturnNotAllowed_WhenRoundNotInLeagueSeason` | `isRoundInLeagueSeason: false` | CanUse = false, reason contains "season" |
| `Evaluate_ShouldReturnNotAllowed_WhenUserIsNotMember` | `isUserMemberOfLeague: false` | CanUse = false, reason contains "member" |
| `Evaluate_ShouldReturnNotAllowed_WhenBoostIsDisabled` | `isEnabled: false` | CanUse = false, reason contains "not enabled" |
| `Evaluate_ShouldReturnNotAllowed_WhenTotalUsesPerSeasonIsZero` | `totalUsesPerSeason: 0` | CanUse = false |
| `Evaluate_ShouldReturnNotAllowed_WhenTotalUsesPerSeasonIsNegative` | `totalUsesPerSeason: -1` | CanUse = false |

### Step 2: Create round-level rejection tests

| Test | Parameters | Expected |
|------|-----------|----------|
| `Evaluate_ShouldReturnAlreadyUsed_WhenHasUsedThisRound` | `hasUsedThisRound: true` | CanUse = false, AlreadyUsedThisRound = true |
| `Evaluate_ShouldReturnNotAllowed_WhenSeasonLimitReached` | `seasonUses: 3, totalUsesPerSeason: 3` | CanUse = false, reason contains "Season limit" |
| `Evaluate_ShouldReturnNotAllowed_WhenSeasonUsesExceedLimit` | `seasonUses: 5, totalUsesPerSeason: 3` | CanUse = false, reason contains "Season limit" |

### Step 3: Create window-based rejection tests

| Test | Parameters | Expected |
|------|-----------|----------|
| `Evaluate_ShouldReturnNotAllowed_WhenRoundNotInAnyWindow` | `roundNumber: 15`, windows: `[{1-5}, {10-12}]` | CanUse = false, reason contains "not available" |
| `Evaluate_ShouldReturnNotAllowed_WhenWindowMaxUsesIsZero` | window `MaxUsesInWindow: 0` | CanUse = false, reason contains "cannot be used" |
| `Evaluate_ShouldReturnNotAllowed_WhenWindowMaxUsesIsNegative` | window `MaxUsesInWindow: -1` | CanUse = false, reason contains "cannot be used" |
| `Evaluate_ShouldReturnNotAllowed_WhenWindowLimitReached` | `windowUses: 2`, window `MaxUsesInWindow: 2` | CanUse = false, reason contains "Window limit" |
| `Evaluate_ShouldReturnNotAllowed_WhenWindowUsesExceedLimit` | `windowUses: 5`, window `MaxUsesInWindow: 2` | CanUse = false, reason contains "Window limit" |

### Step 4: Create success (Allowed) tests

| Test | Parameters | Expected |
|------|-----------|----------|
| `Evaluate_ShouldReturnAllowed_WhenNoWindowsDefined` | `windows: null`, valid base params | CanUse = true, Reason = null |
| `Evaluate_ShouldReturnAllowed_WhenEmptyWindowsList` | `windows: []`, valid base params | CanUse = true, Reason = null |
| `Evaluate_ShouldReturnAllowed_WhenRoundIsInWindowAndLimitsNotReached` | `roundNumber: 3`, window `{1-5, max: 2}`, `windowUses: 1` | CanUse = true |
| `Evaluate_ShouldReturnAllowed_WhenRoundIsAtWindowBoundaryStart` | `roundNumber: 1`, window `{1-5}` | CanUse = true |
| `Evaluate_ShouldReturnAllowed_WhenRoundIsAtWindowBoundaryEnd` | `roundNumber: 5`, window `{1-5}` | CanUse = true |
| `Evaluate_ShouldReturnAllowed_WhenRoundIsInSecondWindow` | `roundNumber: 11`, windows: `[{1-5, max:2}, {10-15, max:3}]` | CanUse = true |
| `Evaluate_ShouldReturnAllowed_WhenSeasonUsesLessThanLimit` | `seasonUses: 1, totalUsesPerSeason: 3` | CanUse = true |

### Step 5: Create remaining uses calculation tests

| Test | Parameters | Expected Remaining |
|------|-----------|-------------------|
| `Evaluate_ShouldCalculateSeasonRemaining_WhenNoWindows` | `totalUsesPerSeason: 3, seasonUses: 1`, no windows | `RemainingSeasonUses = 2` |
| `Evaluate_ShouldCalculateWindowRemaining_WhenWindowExists` | `maxUsesInWindow: 3, windowUses: 1` | `RemainingWindowUses = 2` |
| `Evaluate_ShouldSetWindowRemainingToSeasonRemaining_WhenNoWindows` | `totalUsesPerSeason: 3, seasonUses: 1`, no windows | `RemainingWindowUses = 2 (same as season)` |
| `Evaluate_ShouldCalculateSeasonRemainingAsZero_WhenAllUsesConsumedMinusOne` | `totalUsesPerSeason: 3, seasonUses: 2` | `RemainingSeasonUses = 1` |
| `Evaluate_ShouldReturnCorrectRemaining_WhenWindowRemainingDiffersFromSeason` | `seasonUses: 0, windowUses: 1, maxWindow: 2, totalSeason: 5` | `RemainingSeasonUses = 5, RemainingWindowUses = 1` |

### Step 6: Verify result object properties

| Test | Scenario | Expected |
|------|----------|----------|
| `Evaluate_ShouldSetAlreadyUsedThisRoundTrue_WhenUsedThisRound` | `hasUsedThisRound: true` | `AlreadyUsedThisRound = true`, `CanUse = false` |
| `Evaluate_ShouldSetAlreadyUsedThisRoundFalse_WhenNotUsedAndNotAllowed` | Other NotAllowed reason | `AlreadyUsedThisRound = false` |
| `Evaluate_ShouldSetReasonToNull_WhenAllowed` | Valid allowed case | `Reason = null` |
| `Evaluate_ShouldSetRemainingToZero_WhenNotAllowed` | Any NotAllowed case | `RemainingSeasonUses = 0, RemainingWindowUses = 0` |

### Step 7: Verify guard evaluation order

The guards are checked in sequence. Earlier guards should short-circuit later checks:

| Test | Parameters | Expected |
|------|-----------|----------|
| `Evaluate_ShouldCheckRoundInSeasonFirst_WhenMultipleConditionsFail` | `isRoundInLeagueSeason: false, isEnabled: false` | Reason contains "season" (not "enabled") |
| `Evaluate_ShouldCheckMembershipBeforeEnabled_WhenBothFail` | `isUserMemberOfLeague: false, isEnabled: false` | Reason contains "member" (not "enabled") |
| `Evaluate_ShouldCheckEnabledBeforeUsesPerSeason_WhenBothFail` | `isEnabled: false, totalUsesPerSeason: 0` | Reason contains "not enabled" (not "cannot be used") |

## Code Patterns to Follow

```csharp
public class BoostEligibilityEvaluatorTests
{
    // Common valid base parameters for success-path tests
    private const bool DefaultEnabled = true;
    private const int DefaultTotalUses = 3;
    private const int DefaultSeasonUses = 0;
    private const int DefaultWindowUses = 0;
    private const bool DefaultHasUsedThisRound = false;
    private const int DefaultRoundNumber = 3;
    private const bool DefaultIsMember = true;
    private const bool DefaultIsRoundInSeason = true;

    [Fact]
    public void Evaluate_ShouldReturnNotAllowed_WhenBoostIsDisabled()
    {
        // Act
        var result = BoostEligibilityEvaluator.Evaluate(
            isEnabled: false,
            totalUsesPerSeason: DefaultTotalUses,
            seasonUses: DefaultSeasonUses,
            windowUses: DefaultWindowUses,
            hasUsedThisRound: DefaultHasUsedThisRound,
            roundNumber: DefaultRoundNumber,
            windows: null,
            isUserMemberOfLeague: DefaultIsMember,
            isRoundInLeagueSeason: DefaultIsRoundInSeason);

        // Assert
        result.CanUse.Should().BeFalse();
        result.Reason.Should().Contain("not enabled");
    }

    private static List<BoostWindowSnapshot> CreateWindows(int start, int end, int maxUses) =>
    [
        new BoostWindowSnapshot
        {
            StartRoundNumber = start,
            EndRoundNumber = end,
            MaxUsesInWindow = maxUses
        }
    ];
}
```

## Verification

- [ ] All early-exit guard conditions tested (round not in season, not a member, disabled, zero/negative uses)
- [ ] AlreadyUsedThisRound distinct from other NotAllowed results
- [ ] Window boundary conditions tested (start, end, outside, multiple windows)
- [ ] Window limit tested at boundary (equal) and beyond (exceeds)
- [ ] Remaining use calculations verified for both season and window
- [ ] Multiple windows scenario: round in second window returns Allowed
- [ ] Result properties verified (Reason null when Allowed, zero remaining when NotAllowed)
- [ ] Guard evaluation order verified (earlier guards short-circuit later ones)
- [ ] `dotnet test` passes

## Edge Cases to Consider

- Order matters: guards are checked in sequence, test that earlier guards short-circuit
- Multiple windows: only the first matching window applies (`FirstOrDefault`)
- `RemainingWindowUses` falls back to `RemainingSeasonUses` when no windows defined
- Window with `MaxUsesInWindow: 0` or negative is effectively disabled
- Season uses exceeding the limit (not just equal to it)
