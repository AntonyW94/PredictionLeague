# Task: BoostEligibilityEvaluator Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the `BoostEligibilityEvaluator.Evaluate()` static method — the most complex domain logic with 12+ conditional branches covering eligibility checks, window-based limits, and remaining use calculations.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/ThePredictions.Domain.Tests/Services/Boosts/BoostEligibilityEvaluatorTests.cs` | Create | All evaluator tests |

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
| `Evaluate_ShouldReturnNotAllowed_WhenSeasonUsesExceedLimit` | `seasonUses: 5, totalUsesPerSeason: 3` | CanUse = false |

### Step 3: Create window-based rejection tests

| Test | Parameters | Expected |
|------|-----------|----------|
| `Evaluate_ShouldReturnNotAllowed_WhenRoundNotInAnyWindow` | `roundNumber: 15`, windows: `[{1-5}, {10-12}]` | CanUse = false, reason contains "not available" |
| `Evaluate_ShouldReturnNotAllowed_WhenWindowMaxUsesIsZero` | window `MaxUsesInWindow: 0` | CanUse = false |
| `Evaluate_ShouldReturnNotAllowed_WhenWindowLimitReached` | `windowUses: 2`, window `MaxUsesInWindow: 2` | CanUse = false, reason contains "Window limit" |

### Step 4: Create success (Allowed) tests

| Test | Parameters | Expected |
|------|-----------|----------|
| `Evaluate_ShouldReturnAllowed_WhenNoWindowsDefined` | `windows: null`, valid base params | CanUse = true |
| `Evaluate_ShouldReturnAllowed_WhenEmptyWindowsList` | `windows: []`, valid base params | CanUse = true |
| `Evaluate_ShouldReturnAllowed_WhenRoundIsInWindowAndLimitsNotReached` | `roundNumber: 3`, window `{1-5, max: 2}`, `windowUses: 1` | CanUse = true |
| `Evaluate_ShouldReturnAllowed_WhenRoundIsAtWindowBoundaryStart` | `roundNumber: 1`, window `{1-5}` | CanUse = true |
| `Evaluate_ShouldReturnAllowed_WhenRoundIsAtWindowBoundaryEnd` | `roundNumber: 5`, window `{1-5}` | CanUse = true |

### Step 5: Create remaining uses calculation tests

| Test | Expected Remaining |
|------|-------------------|
| `Evaluate_ShouldCalculateSeasonRemaining_WhenNoWindows` | `RemainingSeasonUses = totalUsesPerSeason - seasonUses` |
| `Evaluate_ShouldCalculateWindowRemaining_WhenWindowExists` | `RemainingWindowUses = maxUsesInWindow - windowUses` |
| `Evaluate_ShouldSetWindowRemainingToSeasonRemaining_WhenNoWindows` | `RemainingWindowUses == RemainingSeasonUses` |

### Step 6: Verify AlreadyUsedThisRound result properties

| Test | Expected |
|------|----------|
| `Evaluate_ShouldSetAlreadyUsedThisRoundTrue_WhenUsedThisRound` | `AlreadyUsedThisRound = true`, `CanUse = false` |
| `Evaluate_ShouldSetAlreadyUsedThisRoundFalse_WhenNotUsedAndNotAllowed` | `AlreadyUsedThisRound = false` (for other NotAllowed reasons) |

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

- [ ] All early-exit guard conditions tested
- [ ] AlreadyUsedThisRound distinct from other NotAllowed results
- [ ] Window boundary conditions tested (start, end, outside)
- [ ] Remaining use calculations verified
- [ ] Multiple windows scenario tested
- [ ] `dotnet test` passes

## Edge Cases to Consider

- Order matters: guards are checked in sequence, test that earlier guards short-circuit
- Multiple windows: only the matching window applies
- `RemainingWindowUses` falls back to `RemainingSeasonUses` when no windows defined
- Window with `MaxUsesInWindow: 0` is effectively disabled
