# Fix Plan: Boost Deadline Enforcement

## Overview

| Attribute | Value |
|-----------|-------|
| Priority | P2 - Medium |
| Severity | Medium |
| Type | Business Logic |
| CWE | CWE-840: Business Logic Errors |
| OWASP | Business Logic Flaws |

---

## Vulnerability Details

### Description
The boost application logic does not verify that the round deadline has not passed. Users can apply boosts to rounds after the prediction deadline has closed.

### Affected Files
- `PredictionLeague.Infrastructure/Services/BoostService.cs` (lines 81-131)
- `PredictionLeague.Domain/Services/Boosts/BoostEligibilityEvaluator.cs`

### Attack Vector
```
Round deadline: 2025-01-25 15:00 UTC
1. User submits predictions at 14:59 UTC
2. Round deadline passes at 15:00 UTC
3. User applies boost at 15:05 UTC (AFTER deadline)
4. Boost is applied retroactively to predictions
Result: Unfair advantage - other users cannot respond
```

### Impact
- Competitive unfairness
- Users can see match kick-offs before deciding to boost
- Undermines integrity of the prediction system

---

## Fix Implementation

### Step 1: Add Deadline Check to BoostService

**File:** `PredictionLeague.Infrastructure/Services/BoostService.cs`

**Add round deadline validation in ApplyBoostAsync:**

```csharp
public async Task<ApplyBoostResultDto> ApplyBoostAsync(
    string boostCode,
    string userId,
    int leagueId,
    int roundId,
    CancellationToken cancellationToken)
{
    // NEW: Check round deadline first
    var round = await _roundRepository.GetByIdAsync(roundId, cancellationToken);
    if (round is null)
    {
        return new ApplyBoostResultDto(false, "Round not found");
    }

    if (round.DeadlineUtc < DateTime.UtcNow)
    {
        return new ApplyBoostResultDto(false, "Cannot apply boost after round deadline has passed");
    }

    // Existing eligibility check
    var eligibility = await GetEligibilityAsync(boostCode, userId, leagueId, roundId, cancellationToken);

    if (!eligibility.CanUse)
    {
        return new ApplyBoostResultDto(false, eligibility.Reason ?? "Boost cannot be applied");
    }

    // Continue with existing logic...
    var (inserted, error) = await _boostWriteRepository.InsertUserBoostUsageAsync(
        boostCode,
        userId,
        leagueId,
        roundId,
        eligibility.SeasonId,
        cancellationToken);

    if (!inserted)
    {
        return new ApplyBoostResultDto(false, error ?? "Failed to apply boost");
    }

    return new ApplyBoostResultDto(true, null);
}
```

### Step 2: Add Deadline Check to BoostEligibilityEvaluator

For consistency, also add the check in the eligibility evaluator:

**File:** `PredictionLeague.Domain/Services/Boosts/BoostEligibilityEvaluator.cs`

```csharp
public BoostEligibility Evaluate(
    BoostEligibilitySnapshot snapshot,
    string boostCode,
    int roundId,
    DateTime roundDeadlineUtc)  // NEW PARAMETER
{
    // NEW: Check deadline first
    if (roundDeadlineUtc < DateTime.UtcNow)
    {
        return BoostEligibility.NotEligible("Round deadline has passed");
    }

    // Existing evaluation logic...
}
```

### Step 3: Update GetEligibilityAsync to Include Deadline

**File:** `PredictionLeague.Infrastructure/Services/BoostService.cs`

```csharp
public async Task<BoostEligibilityDto> GetEligibilityAsync(
    string boostCode,
    string userId,
    int leagueId,
    int roundId,
    CancellationToken cancellationToken)
{
    // Get round to check deadline
    var round = await _roundRepository.GetByIdAsync(roundId, cancellationToken);
    if (round is null)
    {
        return new BoostEligibilityDto(false, "Round not found", 0, 0, 0);
    }

    // Check deadline
    if (round.DeadlineUtc < DateTime.UtcNow)
    {
        return new BoostEligibilityDto(false, "Round deadline has passed", 0, 0, 0);
    }

    // Existing snapshot and evaluation logic...
    var snapshot = await _boostReadRepository.GetUserBoostUsageSnapshotAsync(
        boostCode, userId, leagueId, roundId, cancellationToken);

    if (snapshot is null)
    {
        return new BoostEligibilityDto(false, "Unable to load boost configuration", 0, 0, 0);
    }

    var eligibility = _eligibilityEvaluator.Evaluate(snapshot, boostCode, roundId);

    return new BoostEligibilityDto(
        eligibility.CanUse,
        eligibility.Reason,
        snapshot.SeasonId,
        eligibility.UsesRemaining,
        eligibility.TotalUses);
}
```

---

## UI Update (Optional)

Update the Blazor component to show deadline status:

**File:** `PredictionLeague.Web.Client/Components/Pages/Predictions/Predictions.razor`

```razor
@if (_boostEligibility?.CanUse == true)
{
    <div class="boost-section">
        <button class="btn btn-primary" @onclick="ApplyBoost">Apply Double Up Boost</button>
    </div>
}
else if (_pageData.IsPastDeadline)
{
    <div class="alert alert-info">
        Boosts cannot be applied after the round deadline.
    </div>
}
```

---

## Testing

### Manual Test Steps

1. Create a test round with a deadline in the past
2. Attempt to apply boost via API:
   ```
   POST /api/boosts/apply
   {
     "boostCode": "DOUBLE_UP",
     "leagueId": 1,
     "roundId": {pastDeadlineRoundId}
   }
   ```
3. Verify response: `{ "success": false, "error": "Cannot apply boost after round deadline has passed" }`

4. Create a round with future deadline
5. Apply boost and verify it succeeds
6. Wait for deadline to pass
7. Attempt to apply boost again (to test duplicate prevention)

### Edge Case Testing

- Boost applied exactly at deadline time (should fail - use `<` not `<=`)
- Server clock skew scenarios
- Timezone handling (all times should be UTC)

---

## Rollback Plan

If the deadline check causes unexpected issues:
1. Remove the deadline check from BoostService.ApplyBoostAsync
2. Keep other validation intact
3. Investigate the specific edge case causing problems

---

## Notes

- This fix should be combined with the race condition fix (11-boost-race-condition.md)
- Consider adding a small grace period (e.g., 30 seconds) if needed for UX
- The check occurs at service level, not repository level, for cleaner separation
