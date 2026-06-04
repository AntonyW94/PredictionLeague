# Task 6: Admin Enter Results Round‑Trip

**Parent Feature:** [Displaying Knockout Results (Extra Time & Penalties)](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Ensure that when an admin saves results on the "Enter Results" screen, any
sync‑captured ET/penalty values are **preserved** (not wiped). The admin edits
only the 90′ score in phase 1; the ET/pens round‑trip read‑only.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Web.Client/ViewModels/Admin/Rounds/MatchViewModel.cs` | Modify | Hold ET/pens (read‑only) from the DTO |
| `src/ThePredictions.Web.Client/ViewModels/Admin/Rounds/EnterResultsViewModel.cs` | Modify | Include ET/pens in the saved `MatchResultDto` |

## Why

`UpdateScore` (Task 2) sets the ET/penalty columns from whatever the command
carries. If the admin save sent `null`, it would wipe values captured by the
sync. Round‑tripping the existing values keeps them intact without adding admin
UI.

## Implementation Steps

### Step 1: `MatchViewModel` — carry the values

`MatchViewModel(MatchInRoundDto match)` already projects fields from the DTO
(which now carries ET/pens after Tasks 3 & 5). Add read‑only properties:

```csharp
public int? AfterExtraTimeHomeScore { get; } = match.AfterExtraTimeHomeScore;
public int? AfterExtraTimeAwayScore { get; } = match.AfterExtraTimeAwayScore;
public int? PenaltyHomeScore { get; } = match.PenaltyHomeScore;
public int? PenaltyAwayScore { get; } = match.PenaltyAwayScore;
```

### Step 2: `EnterResultsViewModel.HandleSaveResultsAsync` — include them

```csharp
var resultsToUpdate = Matches.Select(m => new MatchResultDto(
    m.MatchId,
    m.HomeScore,
    m.AwayScore,
    m.Status,
    m.AfterExtraTimeHomeScore,
    m.AfterExtraTimeAwayScore,
    m.PenaltyHomeScore,
    m.PenaltyAwayScore)).ToList();
```

## Verification

- [ ] Editing a knockout's 90′ score in the admin screen and saving leaves the ET/penalty columns unchanged in the database.
- [ ] Saving a non‑knockout match continues to send `null` ET/pens (no regression).

## Notes / Out of Scope

- Manual entry/editing of ET & penalty scores by an admin (steppers on
  `EnterResults.razor` + validation) is a deliberate **future enhancement**, not
  part of this task.
</content>
