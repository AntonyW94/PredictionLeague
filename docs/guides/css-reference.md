# CSS Reference

This file contains detailed CSS reference material for the PredictionLeague application. For rules and conventions, see the main [CLAUDE.md](../CLAUDE.md) file.

## Design Tokens (variables.css)

### Breakpoints (mobile-first)

| Variable | Value | Usage |
|----------|-------|-------|
| `--breakpoint-phone-small` | 480px | Extra small phones |
| `--breakpoint-phone` | 576px | Standard phones |
| `--breakpoint-tablet` | 768px | Tablets |
| `--breakpoint-desktop` | 992px | Desktop |

### Colour Palette

#### Purples (Primary Brand)

| Variable | Hex | Usage |
|----------|-----|-------|
| `--purple-1000` | #2C0A3D | Darkest - deep backgrounds |
| `--purple-900` | #31144A | Dark backgrounds |
| `--purple-800` | #3D195B | Common section backgrounds |
| `--purple-700` | #432468 | Card backgrounds |
| `--purple-600` | #4A2E6C | Lighter sections |
| `--purple-300` | #75559D | Muted accents |
| `--purple-200` | #963CFF | Bright accent/highlight |

#### Blues

| Variable | Hex | Usage |
|----------|-----|-------|
| `--blue-500` | #04F5FF | Bright cyan - links, highlights |
| `--blue-700` | #03c2b4 | Darker teal - secondary |

#### Greens

| Variable | Hex | Usage |
|----------|-----|-------|
| `--green-300` | #84fab0 | Light/pastel green |
| `--green-600` | #00B960 | Success, positive values |

#### Greys

| Variable | Range | Usage |
|----------|-------|-------|
| `--grey-100` to `--grey-500` | Light to dark | Backgrounds, muted text |

#### Single-Value Colours

| Variable | Hex | Usage |
|----------|-----|-------|
| `--red` | #E90052 | Errors, destructive actions |
| `--yellow` | #EBFF01 | Warnings, highlights |
| `--orange` | #CC8200 | Caution states |
| `--gold` | #FFD700 | 1st place, premium |
| `--silver` | #C0C0C0 | 2nd place |
| `--bronze` | #CD7F32 | 3rd place |
| `--white` | #FFFFFF | Text on dark backgrounds |

#### Transparent Colours

**Black alphas** (for shadows, overlays):
- `--black-alpha-15`, `--black-alpha-20`, `--black-alpha-35`, `--black-alpha-50`, `--black-alpha-60`

**White alphas** (for subtle effects):
- `--white-alpha-02`, `--white-alpha-05`, `--white-alpha-08`, `--white-alpha-10`, `--white-alpha-15`, `--white-alpha-30`

**Purple alphas**:
- `--purple-800-alpha-25`, `--purple-900-alpha-80`

**Yellow alphas** (for animations):
- `--yellow-alpha-70`, `--yellow-alpha-00`

---

## Utility Classes

### Text Colours

| Class | Colour | Usage |
|-------|--------|-------|
| `.text-white` | White | Default text on dark backgrounds |
| `.text-green-600` | Green | Success, positive values |
| `.text-red` | Red | Errors, negative values |
| `.text-blue-500` | Cyan | Highlights, links |
| `.text-grey-300` | Light grey | Muted secondary text |
| `.text-grey-500` | Dark grey | Muted text on light backgrounds |
| `.text-purple-1000` | Dark purple | Text on light backgrounds |
| `.text-gold` | Gold | Prize/premium indicators |

### Background Colours

| Class | Usage |
|-------|-------|
| `.bg-purple-600` through `.bg-purple-1000` | Section backgrounds (lighter to darker) |
| `.bg-green-600`, `.bg-green-300` | Success states |
| `.bg-blue-500`, `.bg-blue-700` | Accent backgrounds |
| `.bg-red` | Error/destructive backgrounds |
| `.bg-gold`, `.bg-silver`, `.bg-bronze` | Medal/rank backgrounds |

### Effects

| Class | Effect |
|-------|--------|
| `.glass-panel` | Radial gradient with subtle border and inner glow |
| `.table-striped-purple` | Purple row striping for tables |
| `.shadow` | Standard drop shadow |

### Width Utilities

Complete set from `.w-10` through `.w-100` (increments of 5), plus `.w-auto`.

### Typography

| Class | Effect |
|-------|--------|
| `.fw-bold`, `.fw-normal` | Font weight |
| `.text-center`, `.text-start`, `.text-end` | Text alignment |
| `.text-uppercase`, `.text-lowercase` | Text transform |
| `.word-break-all` | Break long words |
| `.whitespace-nowrap` | Prevent wrapping |

---

## Component Patterns

### Glass Panel Effect

```html
<div class="glass-panel hero-rank-container">
    <!-- Content with frosted glass appearance -->
</div>
```

Creates a premium-looking panel with:
- Radial gradient background
- Subtle white border
- Inner glow effect

### Table Striping

```html
<table class="leaderboard-table table-striped-purple">
    <thead>
        <tr>
            <th>#</th>
            <th>Player</th>
            <th>Pts</th>
        </tr>
    </thead>
    <tbody>
        <!-- Rows automatically striped with purple tones -->
    </tbody>
</table>
```

### Button Variants

| Class | Usage | Colour |
|-------|-------|--------|
| `.green-button` | Primary actions (submit, confirm) | Green |
| `.red-button` | Destructive actions (delete, remove) | Red |
| `.purple-accent-button` | Secondary actions | Purple accent |
| `.blue-light-button` | Tertiary/informational actions | Light blue |

### Card Structure

```html
<div class="card">
    <div class="card-header">
        <!-- Header content -->
    </div>
    <div class="card-body">
        <!-- Main content -->
    </div>
    <div class="card-footer">
        <!-- Footer/actions -->
    </div>
</div>
```

### Section Structure

```html
<div class="section">
    <h5 class="text-white fw-bold mb-3">Section Title</h5>
    <div class="body">
        <!-- Section content -->
    </div>
</div>
```

### Collapsible Section

```html
<div class="section">
    <div class="section-header-collapsible" @onclick="ToggleExpanded">
        <h5 class="text-white fw-bold">Title</h5>
        <i class="bi bi-chevron-down collapse-icon @(_isExpanded ? "rotated" : "")"></i>
    </div>
    <div class="section-body-collapsible @(_isExpanded ? "" : "collapsed")">
        <!-- Collapsible content -->
    </div>
</div>
```

---

## Media Query Examples

### Mobile-First Pattern

```css
/* Base: Extra-small mobile (< 480px) */
.element {
    padding: 0.5rem;
    font-size: 0.875rem;
}

/* Small phone and up (480px) */
@media (min-width: 480px) {
    .element {
        padding: 0.75rem;
    }
}

/* Phone and up (576px) */
@media (min-width: 576px) {
    .element {
        padding: 1rem;
    }
}

/* Tablet and up (768px) */
@media (min-width: 768px) {
    .element {
        padding: 1.5rem;
        font-size: 1rem;
    }
}

/* Desktop and up (992px) */
@media (min-width: 992px) {
    .element {
        padding: 2rem;
    }
}
```

---

## File Locations

| Type | Location | Example |
|------|----------|---------|
| Design tokens | `wwwroot/css/variables.css` | Colours, breakpoints, radii |
| Utilities | `wwwroot/css/utilities/` | colours.css, sizing.css, etc. |
| Components | `wwwroot/css/components/` | buttons.css, badges.css, etc. |
| Cards | `wwwroot/css/components/cards/` | card-base.css, league-cards.css |
| Layout | `wwwroot/css/layout/` | navigation.css, section.css |
| Pages | `wwwroot/css/pages/` | home.css, leaderboard.css |
