# Task 8: Per‑Round Legend

**Parent Feature:** [Displaying Knockout Results (Extra Time & Penalties)](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Explain the 90‑minute scoring rule **once per round** (not per cell) with a
small, muted note, shown only when the selected round actually contains a match
that went to extra time / penalties.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Web.Client/Components/Pages/Leagues/Dashboard/RoundResultsTile.razor` | Modify | Add the legend |
| `src/ThePredictions.Web.Client/wwwroot/css/components/badges.css` | Modify | `.knockout-scoring-note` (+ dark) |

## Implementation Steps

### Step 1: Add the legend in `RoundResultsTile.razor`

Inside the `selectedRound != null` block, just below the round‑pill selector
(after ~line 32) and above the desktop grid / mobile carousel. The trigger is
derived from the data already carried (no extra plumbing) — show it once a
knockout in this round has gone past 90 minutes:

```razor
@if (State.CurrentRoundMatches.Any(m => m.AfterExtraTimeHomeScore.HasValue))
{
    <p class="knockout-scoring-note">
        <i class="bi bi-info-circle me-1"></i>
        Knockout matches are scored on the result after 90 minutes. Extra time and
        penalties are shown for reference only.
    </p>
}
```

### Step 2: CSS — `components/badges.css`

The tile sits on the purple dashboard background (light text), so use a light,
muted token; verify contrast in both themes.

```css
.knockout-scoring-note {
    text-align: center;
    font-size: 0.75rem;
    color: var(--white-alpha-50);
    margin-bottom: 0.75rem;
}
```

## Verification

- [ ] Legend appears only when the selected round has a match with `AfterExtraTimeHomeScore` set; hidden otherwise.
- [ ] Appears once per round (not per match/cell).
- [ ] Readable in light and dark mode.

## Notes

- Because the trigger keys off `AfterExtraTimeHomeScore`, the legend also shows
  while a knockout is live in extra time — which is the right moment to explain
  the rule.
</content>
