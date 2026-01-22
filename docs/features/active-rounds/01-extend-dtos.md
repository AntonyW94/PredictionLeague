# Task 1: Extend DTOs

**Parent Feature:** [Active Rounds](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Extend the `UpcomingRoundDto` and `UpcomingMatchDto` to include round status and actual match scores, enabling the UI to display in-progress rounds with colour-coded prediction outcomes.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Contracts/Dashboard/UpcomingRoundDto.cs` | Modify | Add `Status` property |
| `PredictionLeague.Contracts/Dashboard/UpcomingMatchDto.cs` | Modify | Add actual score properties |

## Implementation Steps

### Step 1: Update UpcomingRoundDto

Add a `Status` property to indicate whether the round is Published or InProgress.

**File:** `PredictionLeague.Contracts/Dashboard/UpcomingRoundDto.cs`

```csharp
namespace PredictionLeague.Contracts.Dashboard;

/// <summary>
/// DTO for displaying rounds on the dashboard active rounds tile.
/// Includes both upcoming (Published) and in-progress rounds.
/// </summary>
public record UpcomingRoundDto(
    int Id,
    string SeasonName,
    int RoundNumber,
    DateTime DeadlineUtc,
    bool HasUserPredicted,
    string Status,  // NEW: "Published" or "InProgress"
    IEnumerable<UpcomingMatchDto> Matches
);
```

**Note:** Using `string` for status to avoid coupling the Contracts project to Domain enums. The handler will convert `RoundStatus` enum to string.

### Step 2: Update UpcomingMatchDto

Add actual score properties so the UI can calculate prediction outcomes.

**File:** `PredictionLeague.Contracts/Dashboard/UpcomingMatchDto.cs`

```csharp
namespace PredictionLeague.Contracts.Dashboard;

/// <summary>
/// Lightweight DTO for displaying match predictions on the dashboard active rounds tile.
/// Contains logos, predicted scores, and actual scores (for in-progress/completed matches).
/// </summary>
public record UpcomingMatchDto(
    int MatchId,
    string? HomeTeamLogoUrl,
    string? AwayTeamLogoUrl,
    int? PredictedHomeScore,
    int? PredictedAwayScore,
    int? ActualHomeScore,   // NEW: Null if match hasn't started
    int? ActualAwayScore    // NEW: Null if match hasn't started
);
```

## Code Patterns to Follow

Existing DTO pattern from the codebase:

```csharp
// DTOs are records with init-only properties via constructor
public record MyLeagueDto(
    int Id,
    string Name,
    int? CurrentRoundId,
    // ... other properties
);
```

- Use C# record types for immutability
- Constructor parameters for all properties
- Nullable types (`int?`, `string?`) where values may be absent
- XML documentation comments for clarity

## Verification

- [ ] Solution builds without errors
- [ ] `UpcomingRoundDto` has new `Status` property (string type)
- [ ] `UpcomingMatchDto` has new `ActualHomeScore` and `ActualAwayScore` properties
- [ ] No breaking changes to existing code that uses these DTOs (handler will be updated in next task)

## Edge Cases to Consider

- Existing code calling these DTOs will need updating (handled in Task 2)
- `ActualHomeScore` and `ActualAwayScore` will be `null` for matches that haven't started
- Status will always be either "Published" or "InProgress" (Completed rounds are excluded from query)

## Notes

- The `Status` property uses `string` rather than importing the `RoundStatus` enum to keep Contracts project free of Domain dependencies
- The alternative would be to create a separate enum in Contracts, but string is simpler for this use case
- Consider future scenarios: if we ever need to expose Draft or Completed rounds, the string approach allows easy extension
