# Task 6: Add Responsive Styles

## Objective

Add CSS styles for the match preview grid with responsive 2-column (desktop) and 1-column (mobile) layouts.

## File to Modify

**Path:** `PredictionLeague.Web/PredictionLeague.Web.Client/wwwroot/css/components/cards.css`

## Why cards.css?

The project uses global CSS files organized by type (`components/`, `pages/`, `layout/`). The `RoundCard` component already uses classes defined in `cards.css` (`.action-card`, `.action-card-body`, etc.), so the new styles should be added there for consistency.

## Styles to Add

Add the following CSS at the end of `cards.css`:

```css
/* Match Preview Grid - Dashboard Upcoming Rounds Tile */
.match-preview-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 8px;
    padding: 12px 0;
    width: 100%;
}

.match-preview-row {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 6px;
}

.match-preview-logo {
    width: 20px;
    height: 20px;
    object-fit: contain;
    flex-shrink: 0;
}

.match-preview-score {
    min-width: 14px;
    text-align: center;
    color: var(--pl-white);
    font-size: 14px;
    font-weight: 500;
}

.match-preview-vs {
    color: var(--pl-grey-300);
    font-size: 12px;
    opacity: 0.7;
}

@media (max-width: 768px) {
    .match-preview-grid {
        grid-template-columns: 1fr;
    }
}
```

## Style Breakdown

### `.match-preview-grid`

```css
.match-preview-grid {
    display: grid;
    grid-template-columns: 1fr 1fr;  /* 2 equal columns */
    gap: 8px;                         /* Space between rows/columns */
    padding: 12px 0;                  /* Vertical padding only */
    width: 100%;                      /* Full width of container */
}
```

- Uses CSS Grid for 2-column layout
- `1fr 1fr` creates two equal-width columns
- `gap: 8px` adds consistent spacing
- `padding: 12px 0` separates from deadline badge above and button below

### `.match-preview-row`

```css
.match-preview-row {
    display: flex;
    align-items: center;      /* Vertically center all items */
    justify-content: center;  /* Horizontally center the row */
    gap: 6px;                 /* Space between logo-score-v-score-logo */
}
```

- Flexbox for horizontal alignment
- All items centered both ways
- Small gap between elements

### `.match-preview-logo`

```css
.match-preview-logo {
    width: 20px;
    height: 20px;
    object-fit: contain;  /* Preserve aspect ratio */
    flex-shrink: 0;       /* Don't shrink below 20px */
}
```

- Fixed 20×20px size as specified
- `object-fit: contain` ensures logos aren't distorted
- `flex-shrink: 0` prevents logos from shrinking in flexbox

### `.match-preview-score`

```css
.match-preview-score {
    min-width: 14px;          /* Enough for 2-digit scores */
    text-align: center;        /* Center single digits */
    color: var(--pl-white);    /* White text */
    font-size: 14px;           /* Readable but compact */
    font-weight: 500;          /* Medium weight for visibility */
}
```

- `min-width: 14px` ensures alignment even with single-digit vs double-digit scores
- Uses project's white colour variable

### `.match-preview-vs`

```css
.match-preview-vs {
    color: var(--pl-grey-300);  /* Subtle grey */
    font-size: 12px;            /* Smaller than scores */
    opacity: 0.7;               /* Further subdued */
}
```

- Subdued styling so "v" doesn't compete with scores
- Uses project's grey colour variable

### Mobile Responsive

```css
@media (max-width: 768px) {
    .match-preview-grid {
        grid-template-columns: 1fr;  /* Single column */
    }
}
```

- Below 768px (mobile), switch to single column
- 768px breakpoint matches other responsive styles in cards.css

## Colour Variables Reference

The styles use these variables from `colours.css`:

| Variable | Value | Usage |
|----------|-------|-------|
| `--pl-white` | `#FFFFFF` | Score text |
| `--pl-grey-300` | `#CECECE` | "v" separator |

## Visual Result

### Desktop (2 columns, ≥768px):
```
┌─────────────────────────────────────────┐
│  [logo] 2 v 1 [logo]  [logo] 0 v 0 [logo]  │
│  [logo] - v - [logo]  [logo] 3 v 2 [logo]  │
│  [logo] 1 v 1 [logo]  [logo] - v 1 [logo]  │
│  [logo] 2 v 0 [logo]  [logo] 1 v 1 [logo]  │
│  [logo] - v - [logo]  [logo] 2 v 2 [logo]  │
└─────────────────────────────────────────┘
```

### Mobile (1 column, <768px):
```
┌─────────────────────┐
│  [logo] 2 v 1 [logo]  │
│  [logo] - v - [logo]  │
│  [logo] 0 v 0 [logo]  │
│  [logo] 3 v 2 [logo]  │
│  [logo] 1 v 1 [logo]  │
│  [logo] - v 1 [logo]  │
│  [logo] 2 v 0 [logo]  │
│  [logo] 1 v 1 [logo]  │
│  [logo] - v - [logo]  │
│  [logo] 2 v 2 [logo]  │
└─────────────────────┘
```

## Location in File

Add the new styles at the **end** of `cards.css`, after the existing styles. The file currently ends around line 385.

Look for a good place after the last existing rule, add a blank line, then add the comment and styles:

```css
/* ... existing styles ... */

/* Match Preview Grid - Dashboard Upcoming Rounds Tile */
.match-preview-grid {
    /* ... */
}
```

## Verification

After modifying the file:

1. Verify no CSS syntax errors by opening the site in a browser and checking the console

2. Use browser DevTools to test responsive behaviour:
   - Toggle device toolbar (F12 → mobile icon)
   - Verify 2 columns at ≥768px
   - Verify 1 column at <768px

3. Verify alignment:
   - Logos should be vertically centered
   - Scores should be horizontally centered
   - All rows should align consistently

## Integration with Existing Styles

The new styles are self-contained and don't conflict with existing card styles. They:

- Use unique class names prefixed with `match-preview-`
- Don't override any existing classes
- Follow the same naming conventions (kebab-case)
- Use the same colour variables

## Next Task

Proceed to [Task 7: Testing Checklist](./07-testing-checklist.md)
