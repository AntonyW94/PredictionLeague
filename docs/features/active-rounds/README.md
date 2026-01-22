# Feature: Active Rounds (In-Progress Rounds on Dashboard)

## Status

**Not Started** | In Progress | Complete

## Summary

Enhance the "Upcoming Rounds" tile on the dashboard to also show in-progress rounds, providing users with instant visual feedback on their prediction performance as matches are played. The tile will be renamed to "Active Rounds" and will display colour-coded match backgrounds (green for exact score, yellow for correct result, red for wrong) once matches have started or completed.

## User Story

As a league member, I want to see my in-progress rounds on the dashboard so that I can instantly visualise how my predictions are performing as matches are played, without having to navigate to the league dashboard.

## Design / Mockup

### Current State (Upcoming Rounds)

```
┌─────────────────────────────────────────┐
│         Premier League 2024/25          │
│              Round 23                   │
│                                         │
│   [🔴 Deadline: 15 Jan 3:00pm]          │
│                                         │
│   ┌──────────┐  ┌──────────┐            │
│   │ 🏠 2 v 1 🏃 │  │ 🏠 0 v 0 🏃 │            │ ← Dark background
│   └──────────┘  └──────────┘            │
│   ┌──────────┐  ┌──────────┐            │
│   │ 🏠 1 v 2 🏃 │  │ 🏠 3 v 1 🏃 │            │
│   └──────────┘  └──────────┘            │
│                                         │
│   [ ✏️ Edit Predictions ]               │
└─────────────────────────────────────────┘
```

### New State (In-Progress Round)

```
┌─────────────────────────────────────────┐
│         Premier League 2024/25          │
│              Round 22                   │
│                                         │
│   [🔵 ▶ In Progress]                    │  ← Blue badge, play icon
│                                         │
│   ┌──────────┐  ┌──────────┐            │
│   │ 🏠 2 v 1 🏃 │  │ 🏠 0 v 0 🏃 │            │ ← Green = exact, Yellow = correct
│   └──────────┘  └──────────┘            │
│   ┌──────────┐  ┌──────────┐            │
│   │ 🏠 1 v 2 🏃 │  │ 🏠 3 v 1 🏃 │            │ ← Red = wrong, Dark = not started
│   └──────────┘  └──────────┘            │
│                                         │
│                                         │  ← No button (removed)
└─────────────────────────────────────────┘
```

### Match Background Colours

| Prediction Outcome | Background Colour | When Applied |
|--------------------|-------------------|--------------|
| Not yet started / Pending | `--black-alpha-25` (current) | Match hasn't kicked off or no result yet |
| Exact Score | Green (`--green-600`) | Actual score matches prediction |
| Correct Result | Yellow (`--yellow`) | Got winner/draw correct, but not exact score |
| Incorrect | Red (`--red`) | Got result wrong |
| No prediction | `--black-alpha-25` (current) | User didn't predict this match |

### Badge Styling

| Round Status | Badge Class | Icon | Text |
|--------------|-------------|------|------|
| Published (deadline in future) | `badge-group--red` | `bi-alarm` | Deadline date/time |
| Published (< 24 hours to deadline) | (countdown) | - | Countdown timer |
| In Progress | `badge-group--blue` | `bi-play-fill` | "In Progress" |

## Acceptance Criteria

- [ ] Tile is renamed from "Upcoming Rounds" to "Active Rounds"
- [ ] In-progress rounds appear in the carousel alongside upcoming rounds
- [ ] In-progress rounds appear first (before upcoming rounds) in the carousel
- [ ] In-progress rounds show a blue "In Progress" badge with play icon
- [ ] The Predict/Edit button is hidden for in-progress rounds
- [ ] Match backgrounds use standard border-radius (`--bs-border-radius`) instead of `4px`
- [ ] Matches not yet started show the current dark background
- [ ] Matches in progress or completed show coloured backgrounds based on prediction outcome:
  - Green (`--green-600`) for exact score
  - Yellow (`--yellow`) for correct result
  - Red (`--red`) for incorrect prediction
- [ ] Completed rounds (status = Completed) do NOT appear in the tile
- [ ] Works correctly on mobile and tablet viewports

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | [Extend DTOs](./01-extend-dtos.md) | Add `RoundStatus` to round DTO and `PredictionOutcome` to match DTO | Not Started |
| 2 | [Update Query Handler](./02-update-query-handler.md) | Modify SQL to include in-progress rounds and fetch outcome from UserPredictions | Not Started |
| 3 | [Update RoundCard Component](./03-update-round-card.md) | Add conditional UI for in-progress rounds | Not Started |
| 4 | [Add CSS Styles](./04-add-css-styles.md) | Add match outcome background colours and update border-radius | Not Started |
| 5 | [Rename to Active Rounds](./05-rename-tile.md) | Rename all layers: DTOs, queries, services, and components | Not Started |

## Dependencies

- [x] `RoundStatus` enum exists with `InProgress` value
- [x] `PredictionOutcome` enum exists with correct values
- [x] Blue badge class (`badge-group--blue`) already exists in `badges.css`
- [x] `UserPredictions.Outcome` column stores pre-calculated prediction outcome
- [x] Colour variables exist in `variables.css`

## Technical Notes

### Using Pre-Calculated Outcome

The `UserPredictions` table already stores the calculated `Outcome` for each prediction. This is updated by the `SetOutcome` method when match scores are updated. We use this directly rather than recalculating in the UI.

**Benefits:**
- Simpler code - no calculation logic in the Razor component
- Consistent - same outcome value used everywhere in the app
- Efficient - no need to fetch actual match scores

### Using RoundStatus Enum

The DTO uses the `RoundStatus` enum directly for type safety:

```csharp
public record UpcomingRoundDto(
    // ...
    RoundStatus Status,
    // ...
);
```

This allows type-safe comparisons in the component:
```csharp
private bool IsInProgress => Round.Status == RoundStatus.InProgress;
```

### Query Changes

The existing query filters by:
```sql
WHERE r.[Status] = @PublishedStatus AND r.[DeadlineUtc] > GETUTCDATE()
```

This changes to simply exclude Draft and Completed:
```sql
WHERE r.[Status] NOT IN (@DraftStatus, @CompletedStatus)
```

### Ordering

In-progress rounds appear first, then upcoming rounds by deadline:
```sql
ORDER BY
    CASE WHEN r.[Status] = @InProgressStatus THEN 0 ELSE 1 END,
    r.[DeadlineUtc] ASC
```

### Existing Colour Scheme Reference

From `PredictionStatusBadge.razor`:
- Incorrect: `badge-group--red`
- Correct Result: `badge-group--yellow`
- Exact Score: `badge-group--green`
- Pending: `badge-group--blue`

## Open Questions

- [x] Should rounds become visible immediately after deadline passes, or only when first match kicks off? → **Immediately after deadline passes**
- [x] Should in-progress matches show live scores alongside predictions? → **No, just use background colour as indicator**
- [x] What to show in footer area for in-progress rounds? → **Nothing (keep it clean)**
- [x] Should tile be renamed? → **Yes, rename to "Active Rounds"**
- [x] Ordering of in-progress vs upcoming? → **In-progress first, then upcoming by deadline**
