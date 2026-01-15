# Task 5: Update RoundCard Component

## Objective

Modify the `RoundCard.razor` component to display the match grid with team logos and predicted scores.

## File to Modify

**Path:** `PredictionLeague.Web/PredictionLeague.Web.Client/Components/Pages/Dashboard/RoundCard.razor`

## Current State

```razor
@using PredictionLeague.Contracts.Dashboard


<div class="action-card light">
    <div class="card-header">
        <h5 class="fw-bold text-white">@Round.SeasonName</h5>
        <small class="text-white-50">Round @Round.RoundNumber</small>
    </div>

    <div class="action-card-body">
        <dl class="mb-0">
            <div class="action-card-row action-card-row--centered">
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
            </div>
        </dl>
    </div>
    <div class="action-card-footer">
        @if (Round.DeadlineUtc > DateTime.UtcNow)
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
}
```

## Target State

```razor
@using PredictionLeague.Contracts.Dashboard


<div class="action-card light">
    <div class="card-header">
        <h5 class="fw-bold text-white">@Round.SeasonName</h5>
        <small class="text-white-50">Round @Round.RoundNumber</small>
    </div>

    <div class="action-card-body">
        <dl class="mb-0">
            <div class="action-card-row action-card-row--centered">
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
            </div>
        </dl>

        @if (Round.Matches.Any())
        {
            <div class="match-preview-grid">
                @foreach (var match in Round.Matches)
                {
                    <div class="match-preview-row">
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
    <div class="action-card-footer">
        @if (Round.DeadlineUtc > DateTime.UtcNow)
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
}
```

## Changes Explained

### 1. Match Preview Grid Section

Added after the deadline row, inside `action-card-body`:

```razor
@if (Round.Matches.Any())
{
    <div class="match-preview-grid">
        @foreach (var match in Round.Matches)
        {
            <!-- Match row content -->
        }
    </div>
}
```

The `@if (Round.Matches.Any())` check ensures we don't render an empty container if there are no matches (edge case).

### 2. Individual Match Row Structure

```html
<div class="match-preview-row">
    <img ... />           <!-- Home team logo -->
    <span>...</span>      <!-- Home predicted score or "-" -->
    <span>v</span>        <!-- Separator -->
    <span>...</span>      <!-- Away predicted score or "-" -->
    <img ... />           <!-- Away team logo -->
</div>
```

### 3. Image Handling

```razor
<img src="@(match.HomeTeamLogoUrl ?? "images/team-placeholder.svg")"
     class="match-preview-logo"
     alt="Home"
     loading="lazy"
     onerror="this.onerror=null; this.src='images/team-placeholder.svg';" />
```

- **Null coalescing (`??`)**: If `HomeTeamLogoUrl` is null, use placeholder
- **`loading="lazy"`**: Defers loading of off-screen images
- **`onerror`**: If image fails to load (404, network error), replace with placeholder
- **`this.onerror=null`**: Prevents infinite loop if placeholder also fails
- **`alt` attribute**: Accessibility - just "Home" or "Away" (logos are decorative, scores convey meaning)

### 4. Score Display

```razor
<span class="match-preview-score">@(match.PredictedHomeScore?.ToString() ?? "-")</span>
```

- If `PredictedHomeScore` is not null, display the number
- If null (no prediction), display "-"

## CSS Classes Used

The following CSS classes are referenced (defined in Task 6):

| Class | Purpose |
|-------|---------|
| `.match-preview-grid` | Grid container (2 columns desktop, 1 column mobile) |
| `.match-preview-row` | Flexbox row for single match |
| `.match-preview-logo` | Team logo image (20px × 20px) |
| `.match-preview-score` | Score text styling |
| `.match-preview-vs` | "v" separator styling |

## No Changes to @code Block

The `@code` block remains unchanged. The `UpcomingRoundDto` parameter already includes the `Matches` property (added in Task 2), so no additional parameters are needed.

## Verification

After modifying the file:

1. Build the Web.Client project:
   ```bash
   dotnet build PredictionLeague.Web/PredictionLeague.Web.Client/PredictionLeague.Web.Client.csproj
   ```

2. Verify no Razor compilation errors

3. The styles won't work until Task 6 is completed

## Important Notes

### Image Path

The placeholder image path `images/team-placeholder.svg` is relative to `wwwroot/`. This is the standard way to reference static files in Blazor.

### No `@using` Changes Needed

The `UpcomingMatchDto` type is accessed through `Round.Matches`, so no additional `@using` directive is required. The `PredictionLeague.Contracts.Dashboard` namespace already contains both `UpcomingRoundDto` and `UpcomingMatchDto`.

### Event Handling

No click handlers are added to match rows per requirements (clicking a match should not navigate anywhere).

## Next Task

Proceed to [Task 6: Add Responsive Styles](./06-add-styles.md)
