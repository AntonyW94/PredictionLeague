# Task 1: Extend DTOs

**Parent Feature:** [Active Rounds](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Extend the existing DTOs to include round status and prediction outcome, enabling the dashboard to display in-progress rounds with colour-coded match backgrounds.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Contracts/Dashboard/UpcomingRoundDto.cs` | Modify | Add `Status` property using `RoundStatus` enum |
| `PredictionLeague.Contracts/Dashboard/UpcomingMatchDto.cs` | Modify | Add `Outcome` property using `PredictionOutcome` enum |

**Note:** These files will be renamed to `ActiveRoundDto.cs` and `ActiveMatchDto.cs` in Task 5.

## Implementation Steps

### Step 1: Update UpcomingRoundDto

Add a `Status` property using the `RoundStatus` enum to indicate whether the round is Published or InProgress.

**Current:**
```csharp
namespace PredictionLeague.Contracts.Dashboard;

public record UpcomingRoundDto(
    int Id,
    string SeasonName,
    int RoundNumber,
    DateTime DeadlineUtc,
    bool HasUserPredicted,
    IEnumerable<UpcomingMatchDto> Matches
);
```

**Updated:**
```csharp
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Dashboard;

/// <summary>
/// DTO for displaying active rounds (upcoming + in-progress) on the dashboard tile.
/// </summary>
public record UpcomingRoundDto(
    int Id,
    string SeasonName,
    int RoundNumber,
    DateTime DeadlineUtc,
    bool HasUserPredicted,
    RoundStatus Status,
    IEnumerable<UpcomingMatchDto> Matches
);
```

### Step 2: Update UpcomingMatchDto

Add an `Outcome` property to carry the pre-calculated prediction outcome from the `UserPredictions` table. This avoids recalculating the outcome in the UI since it's already stored in the database.

**Current:**
```csharp
namespace PredictionLeague.Contracts.Dashboard;

public record UpcomingMatchDto(
    int MatchId,
    string? HomeTeamLogoUrl,
    string? AwayTeamLogoUrl,
    int? PredictedHomeScore,
    int? PredictedAwayScore
);
```

**Updated:**
```csharp
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Contracts.Dashboard;

/// <summary>
/// Lightweight DTO for displaying match predictions on the dashboard active rounds tile.
/// Contains only the data needed for the compact match preview (logos, scores, and outcome).
/// </summary>
public record UpcomingMatchDto(
    int MatchId,
    string? HomeTeamLogoUrl,
    string? AwayTeamLogoUrl,
    int? PredictedHomeScore,
    int? PredictedAwayScore,
    PredictionOutcome? Outcome
);
```

## Code Patterns to Follow

The `PredictionOutcome` enum already exists in `PredictionLeague.Domain.Common.Enumerations`:

```csharp
public enum PredictionOutcome
{
    Pending = 0,        // Match not yet started or in progress without result
    Incorrect = 1,      // Wrong result/score
    CorrectResult = 2,  // Got the winner/draw right
    ExactScore = 3      // Perfect prediction
}
```

The `RoundStatus` enum:

```csharp
public enum RoundStatus
{
    Draft,        // Not yet published
    Published,    // Published, awaiting deadline
    InProgress,   // Matches are being played
    Completed     // All matches finished
}
```

## Verification

- [ ] Solution builds without errors
- [ ] `UpcomingRoundDto` has `RoundStatus Status` property
- [ ] `UpcomingMatchDto` has `PredictionOutcome? Outcome` property
- [ ] Both DTOs have correct `using` statements for the enums

## Edge Cases to Consider

- `Outcome` is nullable because:
  - User may not have made a prediction for this match (no UserPrediction row exists)
  - For upcoming rounds, outcome will be `Pending` (or `null` if no prediction)
- Published rounds will have `Outcome = Pending` for all matches (not yet scored)
- In-progress rounds may have a mix of `Pending`, `Incorrect`, `CorrectResult`, and `ExactScore`

## Notes

- Using the pre-calculated `Outcome` from `UserPredictions` table is more efficient than recalculating in the UI
- The outcome is already calculated by the `SetOutcome` method when match scores are updated
- Nullable `Outcome` handles the case where user didn't predict (no UserPrediction row)
- Using the `RoundStatus` enum directly provides type safety and avoids string comparisons
