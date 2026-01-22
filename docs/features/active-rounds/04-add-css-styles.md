# Task 4: Add CSS Styles

**Parent Feature:** [Active Rounds](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add CSS classes for match outcome backgrounds (green, yellow, red) and update the match preview row border-radius to use the standard site radius.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `PredictionLeague.Web/PredictionLeague.Web.Client/wwwroot/css/pages/dashboard.css` | Modify | Add match outcome classes and update border-radius |

## Implementation Steps

### Step 1: Update Border Radius

Change the `border-radius` on `.match-preview-row` from `4px` to use the CSS variable.

**Current:**
```css
.match-preview-row {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 0.375rem;
    padding: 0.25rem 0.5rem;
    border-radius: 4px;
    background-color: var(--black-alpha-25);
}
```

**Updated:**
```css
.match-preview-row {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 0.375rem;
    padding: 0.25rem 0.5rem;
    border-radius: var(--bs-border-radius);
    background-color: var(--black-alpha-25);
}
```

### Step 2: Add Match Outcome Classes

Add new CSS classes for the three prediction outcomes. These classes override the default background colour.

```css
/* Match Outcome Backgrounds - Active Rounds Tile */
.match-preview-row.match-outcome-exact {
    background-color: var(--green-600);
}

.match-preview-row.match-outcome-correct-result {
    background-color: var(--yellow);
}

.match-preview-row.match-outcome-correct-result .match-preview-score,
.match-preview-row.match-outcome-correct-result .match-preview-vs {
    color: var(--purple-1000);
}

.match-preview-row.match-outcome-incorrect {
    background-color: var(--red);
}
```

### Step 3: Handle Text Colour on Yellow Background

The yellow background (`--yellow: #EBFF01`) is a bright colour, so we need dark text for readability. The green and red backgrounds work fine with white text.

The scores (`.match-preview-score`) currently use `color: var(--white)`. For yellow background, we override this to use dark purple.

## Full Updated dashboard.css Section

```css
/* Match Preview Grid - Active Rounds Tile */
.match-preview-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 0.5rem;
    padding: 0.75rem 0;
    width: 100%;
    margin-top: 0.5rem;
}

.match-preview-row {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 0.375rem;
    padding: 0.25rem 0.5rem;
    border-radius: var(--bs-border-radius);
    background-color: var(--black-alpha-25);
}

.match-preview-logo {
    width: 1.5rem;
    height: 1.5rem;
    object-fit: contain;
    flex-shrink: 0;
}

.match-preview-score {
    min-width: 0.875rem;
    text-align: center;
    color: var(--white);
    font-size: 0.875rem;
    font-weight: 600;
    font-variant-numeric: tabular-nums;
}

.match-preview-vs {
    color: var(--grey-300);
    font-size: 0.75rem;
    opacity: 0.7;
}

/* Match Outcome Backgrounds - Active Rounds Tile */
.match-preview-row.match-outcome-exact {
    background-color: var(--green-600);
}

.match-preview-row.match-outcome-correct-result {
    background-color: var(--yellow);
}

.match-preview-row.match-outcome-correct-result .match-preview-score,
.match-preview-row.match-outcome-correct-result .match-preview-vs {
    color: var(--purple-1000);
}

.match-preview-row.match-outcome-incorrect {
    background-color: var(--red);
}
```

## Code Patterns to Follow

Existing colour variable usage from `badges.css`:

```css
.badge-group--green {
    background-color: var(--green-600);
}

.badge-group--yellow {
    background-color: var(--yellow);
    color: var(--purple-800);  /* Dark text on yellow */
}

.badge-group--red {
    background-color: var(--red);
}
```

The match outcome classes follow the same pattern:
- Green (`--green-600`) for exact score
- Yellow (`--yellow`) with dark text for correct result
- Red (`--red`) for incorrect

## Verification

- [ ] Match preview rows have rounded corners matching site standard (20px)
- [ ] Exact score matches show green background (`#00B960`)
- [ ] Correct result matches show yellow background (`#EBFF01`) with dark purple text
- [ ] Incorrect predictions show red background (`#E90052`)
- [ ] Matches without results show the default dark background (`--black-alpha-25`)
- [ ] Text is readable on all background colours
- [ ] Colours match the existing `PredictionStatusBadge` colour scheme
- [ ] No CSS compilation errors

## Edge Cases to Consider

- Team logos on coloured backgrounds - ensure they remain visible
- Small screens - colours should still be distinct when rows are compact
- High contrast / accessibility - colours chosen are sufficiently different

## Notes

- The border-radius change from `4px` to `var(--bs-border-radius)` (20px) will be a noticeable visual change
- If 20px feels too rounded for these small elements, consider using a smaller value like `10px` or `12px`
- However, the requirement specified "standard border radius to match the rest of the site"
- The yellow background requires dark text for accessibility (bright yellow on white text is hard to read)
- The green (`--green-600`) and red (`--red`) work well with white text
