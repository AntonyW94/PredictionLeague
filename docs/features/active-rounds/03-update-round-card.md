# Task 3: Update RoundCard Component

**Parent Feature:** [Active Rounds](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Modify the `RoundCard.razor` component to handle in-progress rounds by showing a blue "In Progress" badge, hiding the edit button, and applying colour-coded backgrounds to matches based on prediction outcomes.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Web/PredictionLeague.Web.Client/Components/Pages/Dashboard/RoundCard.razor` | Modify | Add conditional UI for in-progress state |

## Implementation Steps

### Step 1: Add Helper Method for Prediction Outcome

Add a code block to calculate the prediction outcome for a match.

```csharp
@code {
    [Parameter, EditorRequired]
    public UpcomingRoundDto Round { get; set; } = null!;

    private bool IsInProgress => Round.Status == "InProgress";

    private string GetMatchBackgroundClass(UpcomingMatchDto match)
    {
        // If match hasn't started (no actual scores), use default background
        if (match.ActualHomeScore == null || match.ActualAwayScore == null)
            return "";

        // If user didn't predict, it's incorrect
        if (match.PredictedHomeScore == null || match.PredictedAwayScore == null)
            return "match-outcome-incorrect";

        // Exact score match
        if (match.PredictedHomeScore == match.ActualHomeScore &&
            match.PredictedAwayScore == match.ActualAwayScore)
            return "match-outcome-exact";

        // Check if result is correct (home win, away win, or draw)
        var predictedDiff = match.PredictedHomeScore.Value - match.PredictedAwayScore.Value;
        var actualDiff = match.ActualHomeScore.Value - match.ActualAwayScore.Value;

        var predictedOutcome = predictedDiff > 0 ? 1 : (predictedDiff < 0 ? -1 : 0);
        var actualOutcome = actualDiff > 0 ? 1 : (actualDiff < 0 ? -1 : 0);

        if (predictedOutcome == actualOutcome)
            return "match-outcome-correct-result";

        return "match-outcome-incorrect";
    }
}
```

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

    private bool IsInProgress => Round.Status == "InProgress";

    private string GetMatchBackgroundClass(UpcomingMatchDto match)
    {
        // If match hasn't started (no actual scores), use default background
        if (match.ActualHomeScore == null || match.ActualAwayScore == null)
            return "";

        // If user didn't predict, it's incorrect
        if (match.PredictedHomeScore == null || match.PredictedAwayScore == null)
            return "match-outcome-incorrect";

        // Exact score match
        if (match.PredictedHomeScore == match.ActualHomeScore &&
            match.PredictedAwayScore == match.ActualAwayScore)
            return "match-outcome-exact";

        // Check if result is correct (home win, away win, or draw)
        var predictedDiff = match.PredictedHomeScore.Value - match.PredictedAwayScore.Value;
        var actualDiff = match.ActualHomeScore.Value - match.ActualAwayScore.Value;

        var predictedOutcome = predictedDiff > 0 ? 1 : (predictedDiff < 0 ? -1 : 0);
        var actualOutcome = actualDiff > 0 ? 1 : (actualDiff < 0 ? -1 : 0);

        if (predictedOutcome == actualOutcome)
            return "match-outcome-correct-result";

        return "match-outcome-incorrect";
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
- [ ] Matches with results show coloured backgrounds
- [ ] Matches without results (not started) show default dark background
- [ ] Component builds without errors

## Edge Cases to Consider

- User didn't make any predictions - all matches show red background once results come in
- All matches not yet started - all show default background (behaves like current upcoming rounds)
- Mixed state - some matches started, others not yet - different backgrounds per match
- Round transitions from Published to InProgress while user is viewing - card updates on next data refresh

## Notes

- The outcome calculation is intentionally in the component (not the DTO) to keep the DTO simple
- An alternative would be to calculate outcome in the query handler, but this would couple the handler to display logic
- The `IsInProgress` property is a simple string comparison since we use string for status in the DTO
