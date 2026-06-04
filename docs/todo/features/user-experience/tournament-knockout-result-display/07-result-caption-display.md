# Task 7: Result Caption Display

**Parent Feature:** [Displaying Knockout Results (Extra Time & Penalties)](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Render the secondary caption under the actual‑result badge — a **live ticker**
(`Extra time 2-1` / `Penalties 3-2`) while `InProgress`, settling to
`2-1 (a.e.t.)` / `4-2 pens` when `Completed` — in both the desktop grid and the
mobile card, styled to be clearly subordinate to the 90′ score in light and dark
mode.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Web.Client/Components/Pages/Leagues/Dashboard/MatchStatusBadge.razor` | Modify | New params + caption markup + live/final wording |
| `src/ThePredictions.Web.Client/Components/Pages/Leagues/Dashboard/PredictionGrid.razor` | Modify | Pass ET/pens through to the badge |
| `src/ThePredictions.Web.Client/Components/Pages/Leagues/Dashboard/MobileMatchResultCard.razor` | Modify | Pass ET/pens through to the badge |
| `src/ThePredictions.Web.Client/wwwroot/css/components/badges.css` | Modify | `.match-result` / `.match-result-detail` (+ dark, + live) |

No new CSS *file* — reuse `components/badges.css`, so no `app.css` `@import` or
`<CssFilesToBundle>` change.

## Design principles

* **Primary = 90′ score, unchanged** (same bold blue/green/yellow/red badge; it
  pulses while `InProgress`).
* **Caption is muted and smaller** — never competes with the score.
* **Same wording on every surface** — make it fit, don't abbreviate per surface.
* The `InProgress` → `Completed` status transition is the only "phase" signal.

## Implementation Steps

### Step 1: `MatchStatusBadge.razor` — markup

Wrap the existing badge in a vertical stack and append the caption when present.
The badge markup itself is unchanged.

```razor
<div class="match-result @(ShowDetail ? "match-result--with-detail" : "")">
    <div class="@BadgeClass">
        <span class="badge-icon"><i class="@IconClass"></i></span>
        @if (Status == MatchStatus.Scheduled)
        {
            <LocalTime UtcDate="@MatchDateTimeUtc" Format="dd/MM/yyyy 'at' HH:mm" />
        }
        else
        {
            <span>@ScoreText</span>
        }
    </div>

    @if (ShowDetail)
    {
        <span class="match-result-detail @(IsLive ? "match-result-detail--live" : "")" title="@DetailTooltip">@DetailText</span>
    }
</div>
```

### Step 2: `MatchStatusBadge.razor` — parameters & computed members

```csharp
[Parameter] public int? AfterExtraTimeHomeScore { get; set; }
[Parameter] public int? AfterExtraTimeAwayScore { get; set; }
[Parameter] public int? PenaltyHomeScore { get; set; }
[Parameter] public int? PenaltyAwayScore { get; set; }

private bool WentToPenalties => PenaltyHomeScore.HasValue && PenaltyAwayScore.HasValue;

private bool IsLive => Status == MatchStatus.InProgress;

// Set on knockout matches whose tie went beyond 90 minutes — live (InProgress)
// or finished (Completed). Group/league matches never carry these.
private bool ShowDetail =>
    Status is MatchStatus.InProgress or MatchStatus.Completed
    && AfterExtraTimeHomeScore.HasValue
    && AfterExtraTimeAwayScore.HasValue;

private string DetailText
{
    get
    {
        if (WentToPenalties)
        {
            return IsLive
                ? $"Penalties {PenaltyHomeScore}-{PenaltyAwayScore}"
                : $"{PenaltyHomeScore}-{PenaltyAwayScore} pens";
        }

        return IsLive
            ? $"Extra time {AfterExtraTimeHomeScore}-{AfterExtraTimeAwayScore}"
            : $"{AfterExtraTimeHomeScore}-{AfterExtraTimeAwayScore} (a.e.t.)";
    }
}

private string DetailTooltip
{
    get
    {
        if (WentToPenalties)
        {
            return IsLive
                ? $"Penalty shootout in progress, currently {PenaltyHomeScore}-{PenaltyAwayScore}. Predictions are scored on the 90-minute result."
                : $"Final result {AfterExtraTimeHomeScore}-{AfterExtraTimeAwayScore}, {PenaltyHomeScore}-{PenaltyAwayScore} on penalties. Predictions are scored on the 90-minute result.";
        }

        return IsLive
            ? $"Extra time in progress, currently {AfterExtraTimeHomeScore}-{AfterExtraTimeAwayScore}. Predictions are scored on the 90-minute result."
            : $"Final result {AfterExtraTimeHomeScore}-{AfterExtraTimeAwayScore} after extra time. Predictions are scored on the 90-minute result.";
    }
}
```

### Step 3: Wire the two call sites

`PredictionGrid.razor` (~line 49) and `MobileMatchResultCard.razor` (~line 40)
already pass `Status` / `HomeScore` / `AwayScore`. Add four pass‑throughs to each
(`MobileMatchResultCard` uses `Match.` with a capital `M`):

```razor
<MatchStatusBadge Status="match.Status"
                  MatchDateTimeUtc="match.MatchDateTimeUtc"
                  HomeScore="match.ActualHomeTeamScore"
                  AwayScore="match.ActualAwayTeamScore"
                  AfterExtraTimeHomeScore="match.AfterExtraTimeHomeScore"
                  AfterExtraTimeAwayScore="match.AfterExtraTimeAwayScore"
                  PenaltyHomeScore="match.PenaltyHomeScore"
                  PenaltyAwayScore="match.PenaltyAwayScore" />
```

### Step 4: CSS — `components/badges.css`

The desktop grid column (`.results-grid .match-col`) is only `min-width: 6.5rem`
and its cells are `white-space: nowrap`. The trimmed captions fit one line, but
allow the caption (only) to wrap as a safety net. Co‑locate the `.theme-dark`
overrides here (as `results-grid.css` does).

```css
.match-result {
    display: inline-flex;
    flex-direction: column;
    align-items: center;
    gap: 0.15rem;
}

/* Secondary, deliberately quieter than the score badge:
   smaller, lighter, muted colour. Allowed to wrap inside the
   narrow desktop results-grid column (cells are nowrap). */
.match-result-detail {
    max-width: 6rem;
    white-space: normal;
    line-height: 1.1;
    text-align: center;
    font-size: 0.6rem;
    font-weight: 600;
    letter-spacing: 0.02em;
    color: var(--grey-500);
    font-variant-numeric: tabular-nums;
}

.theme-dark .match-result-detail {
    color: var(--white-alpha-50);
}

/* Live ET/penalties ticker: slightly stronger than the settled caption so it
   reads as "happening now" alongside the pulsing 90′ headline above.
   (Blue scale is 500/700/900 only — there is no --blue-300.) */
.match-result-detail--live {
    color: var(--blue-900);
}

.theme-dark .match-result-detail--live {
    color: var(--blue-500);
}

/* Mobile card header has room — keep it on one line and a touch larger. */
@media (min-width: 768px) {
    .match-result-detail {
        font-size: 0.65rem;
    }
}
```

Tokens verified against `variables.css`: `--grey-500` (#6C757D), `--white-alpha-50`,
`--blue-900` (#029187), `--blue-500` (#04F5FF). Confirm contrast in both themes.

## Verification

- [ ] **Light + dark mode** both read clearly; caption is visibly subordinate to the 90′ score.
- [ ] Desktop 24‑match grid: caption fits/wraps without breaking row heights.
- [ ] Mobile card: caption on one line.
- [ ] Live `ET` fixture shows `Extra time x-y`; live `P` shows `Penalties x-y`; both pulse via the headline.
- [ ] On `AET` → `x-y (a.e.t.)`; on `PEN` → `x-y pens`. Tooltip shows the full sentence in every state.
- [ ] A 90′‑decided knockout shows **no** caption.

## Notes

- `PredictionStatusBadge` (player predictions) is intentionally untouched — predictions are 90′ only.
</content>
