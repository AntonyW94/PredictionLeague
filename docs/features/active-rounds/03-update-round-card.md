# Task 3: Update RoundCard Component

**Parent Feature:** [Active Rounds](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Modify the `RoundCard.razor` component to handle in-progress rounds by showing a blue "In Progress" badge, hiding the edit button, and applying colour-coded backgrounds to matches based on the pre-calculated prediction outcome.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Web/PredictionLeague.Web.Client/Components/Pages/Dashboard/RoundCard.razor` | Modify | Add conditional UI for in-progress state |

## Implementation Steps

### Step 1: Add Helper Method for Match Background Class

Add a method to map the `PredictionOutcome` enum to the appropriate CSS class.

```csharp
@code {
    [Parameter, EditorRequired]
    public UpcomingRoundDto Round { get; set; } = null!;

    private bool IsInProgress => Round.Status == RoundStatus.InProgress;

    private string GetMatchBackgroundClass(UpcomingMatchDto match)
    {
        return match.Outcome switch
        {
            PredictionOutcome.ExactScore => "match-outcome-exact",
            PredictionOutcome.CorrectResult => "match-outcome-correct-result",
            PredictionOutcome.Incorrect => "match-outcome-incorrect",
            _ => "" // Pending or null - use default background
        };
    }
}
```

**Note:** This is much simpler than the original plan because we're using the pre-calculated `Outcome` from the database rather than calculating it ourselves.

### Step 2: Update Badge Display Logic

Replace the current deadline badge logic to handle in-progress state.

**Current code:**
```razor
@if ((Round.DeadlineUtc - DateTime.UtcNow).TotalHours < 24)
{
    <CountdownTimer DeadlineUtc="Round.DeadlineUtc" />
}
else
{
    <div class="badge-group badge-group--red">
        <span class="badge-icon" title="Deadline"><i class="bi bi-alarm"></i></span>
        <LocalTime UtcDate="@Round.DeadlineUtc"/>
    </div>
}
```

**New code:**
```razor
@if (IsInProgress)
{
    <div class="badge-group badge-group--blue">
        <span class="badge-icon" title="In Progress"><i class="bi bi-play-fill"></i></span>
        <span>In Progress</span>
    </div>
}
else if ((Round.DeadlineUtc - DateTime.UtcNow).TotalHours < 24)
{
    <CountdownTimer DeadlineUtc="Round.DeadlineUtc" />
}
else
{
    <div class="badge-group badge-group--red">
        <span class="badge-icon" title="Deadline"><i class="bi bi-alarm"></i></span>
        <LocalTime UtcDate="@Round.DeadlineUtc"/>
    </div>
}
```

### Step 3: Update Match Preview Row

Apply the outcome class to each match preview row.

**Current code:**
```razor
<div class="match-preview-row">
    <img src="@(match.HomeTeamLogoUrl ?? "images/team-placeholder.svg")"
         ...
```

**New code:**
```razor
<div class="match-preview-row @GetMatchBackgroundClass(match)">
    <img src="@(match.HomeTeamLogoUrl ?? "images/team-placeholder.svg")"
         ...
```

### Step 4: Update Footer Button Logic

Hide the button for in-progress rounds.

**Current code:**
```razor
<div class="footer">
    @if (Round.DeadlineUtc > DateTime.UtcNow)
    {
        <NavLink href="@($"/predictions/{Round.Id}")" class="btn green-button w-100">
            ...
        </NavLink>
    }
</div>
```

**New code:**
```razor
<div class="footer">
    @if (!IsInProgress && Round.DeadlineUtc > DateTime.UtcNow)
    {
        <NavLink href="@($"/predictions/{Round.Id}")" class="btn green-button w-100">
            ...
        </NavLink>
    }
</div>
```

## Full Updated Component

```razor
@using PredictionLeague.Contracts.Dashboard
@using PredictionLeague.Domain.Common.Enumerations

<div class="card slide">
    <div class="header">
        <h5 class="fw-bold text-white">@Round.SeasonName</h5>
        <small class="text-white-50 fw-bold">Round @Round.RoundNumber</small>
    </div>

    <div class="body">
        <dl class="mb-0 detail-list">
            <div class="detail-row centered">
                @if (IsInProgress)
                {
                    <div class="badge-group badge-group--blue">
                        <span class="badge-icon" title="In Progress"><i class="bi bi-play-fill"></i></span>
                        <span>In Progress</span>
                    </div>
                }
                else if ((Round.DeadlineUtc - DateTime.UtcNow).TotalHours < 24)
                {
                    <CountdownTimer DeadlineUtc="Round.DeadlineUtc" />
                }
                else
                {
                    <div class="badge-group badge-group--red">
                        <span class="badge-icon" title="Deadline"><i class="bi bi-alarm"></i></span>
                        <LocalTime UtcDate="@Round.DeadlineUtc"/>
                    </div>
                }
            </div>
        </dl>

        @if (Round.Matches.Any())
        {
            <div class="match-preview-grid">
                @foreach (var match in Round.Matches)
                {
                    <div class="match-preview-row @GetMatchBackgroundClass(match)">
                        <img src="@(match.HomeTeamLogoUrl ?? "images/team-placeholder.svg")"
                             class="match-preview-logo"
                             alt="Home"
                             loading="lazy"
                             onerror="this.onerror=null; this.src='images/team-placeholder.svg';" />
                        <span class="match-preview-score">@(match.PredictedHomeScore?.ToString() ?? "-")</span>
                        <span class="match-preview-vs">v</span>
                        <span class="match-preview-score">@(match.PredictedAwayScore?.ToString() ?? "-")</span>
                        <img src="@(match.AwayTeamLogoUrl ?? "images/team-placeholder.svg")"
                             class="match-preview-logo"
                             alt="Away"
                             loading="lazy"
                             onerror="this.onerror=null; this.src='images/team-placeholder.svg';" />
                    </div>
                }
            </div>
        }
    </div>
    <div class="footer">
        @if (!IsInProgress && Round.DeadlineUtc > DateTime.UtcNow)
        {
            <NavLink href="@($"/predictions/{Round.Id}")" class="btn green-button w-100">
                @if (Round.HasUserPredicted)
                {
                    <span class="bi bi-pencil-fill me-2"></span>
                    <span>Edit Predictions</span>
                }
                else
                {
                    <span class="bi bi-pencil me-2"></span>
                    <span>Predict Now</span>
                }
            </NavLink>
        }
    </div>
</div>

@code {
    [Parameter, EditorRequired]
    public UpcomingRoundDto Round { get; set; } = null!;

    private bool IsInProgress => Round.Status == RoundStatus.InProgress;

    private string GetMatchBackgroundClass(UpcomingMatchDto match)
    {
        return match.Outcome switch
        {
            PredictionOutcome.ExactScore => "match-outcome-exact",
            PredictionOutcome.CorrectResult => "match-outcome-correct-result",
            PredictionOutcome.Incorrect => "match-outcome-incorrect",
            _ => "" // Pending or null - use default background
        };
    }
}
```

## Code Patterns to Follow

From `PredictionStatusBadge.razor` - existing pattern for outcome-based styling:

```csharp
private string GetColorClass() => Outcome switch
{
    PredictionOutcome.Incorrect => "badge-group--red",
    PredictionOutcome.CorrectResult => "badge-group--yellow",
    PredictionOutcome.ExactScore => "badge-group--green",
    _ => "badge-group--blue"
};
```

The colour scheme should match:
- Incorrect: Red
- Correct Result: Yellow
- Exact Score: Green
- Pending/Not Started: Default (dark)

## Verification

- [ ] In-progress rounds show blue "In Progress" badge with play icon
- [ ] Upcoming rounds continue to show deadline badge/countdown as before
- [ ] Edit/Predict button is hidden for in-progress rounds
- [ ] Edit/Predict button still appears for upcoming rounds
- [ ] Matches with `ExactScore` outcome show green background
- [ ] Matches with `CorrectResult` outcome show yellow background
- [ ] Matches with `Incorrect` outcome show red background
- [ ] Matches with `Pending` or `null` outcome show default dark background
- [ ] Component builds without errors

## Edge Cases to Consider

- User didn't make any predictions - all matches have `null` outcome, all show default background
- All matches not yet started - all have `Pending` outcome, all show default background
- Mixed state - some matches have results, others pending - different backgrounds per match
- Round transitions from Published to InProgress while user is viewing - card updates on next data refresh

## Notes

- Using the `RoundStatus` enum for comparison (`Round.Status == RoundStatus.InProgress`) provides type safety
- The `GetMatchBackgroundClass` method uses a switch expression for clean, readable code
- No calculation logic needed - we simply map the pre-calculated `Outcome` to a CSS class
- Added `@using PredictionLeague.Domain.Common.Enumerations` for the enum references
