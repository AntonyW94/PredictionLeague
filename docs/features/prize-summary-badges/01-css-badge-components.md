# Task 1: CSS Badge Components

**Parent Feature:** [Prize Summary Badges](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Create reusable CSS classes for the prize summary badge pills that can be used across all three prize sections.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `wwwroot/css/components/badges.css` | Modify | Add prize badge styles |
| `wwwroot/css/layout/section.css` | Modify | Add summary row container styles |

## Implementation Steps

### Step 1: Add Prize Badge Base Styles to badges.css

Add new styles for prize summary badges at the end of the existing `badges.css` file.

```css
/* ============================================
   Prize Summary Badges
   Used in collapsible prize section headers
   ============================================ */

.prize-badge {
    display: inline-flex;
    align-items: center;
    gap: 0.375rem;
    padding: 0.25rem 0.625rem;
    border-radius: 1rem;
    font-size: 0.8125rem;
    font-weight: 600;
    white-space: nowrap;
}

/* Badge colour variants */
.prize-badge-gold {
    background-color: var(--gold);
    color: var(--purple-1000);
}

.prize-badge-silver {
    background-color: var(--silver);
    color: var(--purple-1000);
}

.prize-badge-bronze {
    background-color: var(--bronze);
    color: var(--purple-1000);
}

.prize-badge-green {
    background-color: var(--green-600);
    color: var(--white);
}

.prize-badge-blue {
    background-color: var(--blue-500);
    color: var(--purple-1000);
}

.prize-badge-purple {
    background-color: var(--purple-300);
    color: var(--white);
}

/* Badge icon (emoji) */
.prize-badge-icon {
    font-size: 0.875rem;
    line-height: 1;
}

/* Badge text */
.prize-badge-text {
    line-height: 1.2;
}
```

### Step 2: Add Summary Row Container Styles to section.css

Add styles for the summary row that sits between the section title and chevron.

```css
/* Prize summary row in collapsible headers */
.section-header-summary {
    display: flex;
    justify-content: center;
    align-items: center;
    gap: 0.5rem;
    flex-wrap: wrap;
    padding: 0.5rem 0;
    transition: opacity 0.3s ease, max-height 0.3s ease;
    max-height: 3rem;
    opacity: 1;
    overflow: hidden;
}

/* Hide summary when section is expanded */
.section-header-collapsible.expanded .section-header-summary {
    max-height: 0;
    opacity: 0;
    padding: 0;
}
```

### Step 3: Update Collapsible Header for Expanded State

The current implementation uses a class on the body (`collapsed`). We need to also track expanded state on the header for the summary row. Update the existing header styles if needed:

```css
/* Ensure header can track expanded state */
.section-header-collapsible {
    display: flex;
    justify-content: space-between;
    align-items: center;
    cursor: pointer;
    user-select: none;
    flex-direction: column;
}
```

No changes needed to existing styles - the `.expanded` class will be added via Razor.

## Code Patterns to Follow

Existing badge styles in `badges.css`:

```css
/* Example of existing badge pattern */
.badge {
    display: inline-flex;
    align-items: center;
    justify-content: center;
    padding: 0.25rem 0.5rem;
    font-size: 0.75rem;
    font-weight: 700;
    border-radius: 0.25rem;
}
```

The new prize badges follow a similar pattern but with:
- Larger padding for the pill shape
- Full border-radius for pill effect
- Gap for icon + text layout

## Verification

- [ ] Badge classes render with correct colours when applied to test elements
- [ ] Badges display icon and text inline with proper spacing
- [ ] Summary row centres badges horizontally
- [ ] Summary row hides smoothly when `.expanded` class is applied to parent
- [ ] Badges wrap correctly on narrow mobile screens
- [ ] No CSS build/compilation errors

## Edge Cases to Consider

- Very long text in badges (e.g., "£1,000.00 earned") - should still fit on mobile
- Multiple badges wrapping to second line on very narrow screens
- Dark text on gold/blue backgrounds must remain readable

## Notes

- Using `0.8125rem` (13px) font size to keep badges compact but readable
- The `1rem` border-radius creates the pill shape
- Gold, silver, bronze variables already exist in `variables.css`
- The summary row transition matches existing collapsible body transition (0.3s ease)
