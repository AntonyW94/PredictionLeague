# Card CSS Migration Guide

This document outlines the plan to restructure card CSS from business-domain naming to conceptual naming.

## Overview

### Current Problems
1. `.card` is used for both outer sections and inner cards
2. Multiple card types with duplicated styles (action-card, league-card, leaderboard-card)
3. Inconsistent body/footer classes
4. Business-domain naming (league-card, match-card) instead of conceptual naming
5. Inconsistent background colours across cards (mix of purple-600, 800, 900)
6. Card headers darker than card bodies

---

## Colour Scheme (CONFIRMED)

All colours are now standardised:

| Element | CSS Variable | Hex | Purpose |
|---------|--------------|-----|---------|
| **Section** | `--pl-purple-600` | #4A2E6C | Outer containers (lightest) |
| **Card** (all parts) | `--pl-purple-1000` | #2C0A3D | Cards - header, body, footer all same (darkest) |
| **Table stripe odd** | `--pl-purple-800` | #3D195B | Lighter than card |
| **Table stripe even** | `--pl-purple-900` | #31144A | Lighter than card |
| **Glass panels** | Keep gradient | - | Existing radial gradient effect preserved |

### Key Rules
1. **All cards use the same background colour** - No more bg-600, bg-800, bg-900 variants
2. **Card header = body = footer colour** - No darker headers
3. **Table rows never match card background** - Both stripe colours (800, 900) are lighter than card (1000)
4. **Glass panels keep gradient** - May change to flat colour later

### Visual Hierarchy
```
┌─────────────────────────────────┐  ← Section (purple-600) - Lightest
│  Section Title                  │
│  ┌───────────────────────────┐  │
│  │ Card Header (purple-1000) │  │  ← Card - Darkest (all same colour)
│  │ Card Body   (purple-1000) │  │
│  │ ┌─────────────────────┐   │  │
│  │ │ Table row (800)     │   │  │  ← Lighter than card
│  │ │ Table row (900)     │   │  │  ← Lighter than card
│  │ │ Table row (800)     │   │  │
│  │ └─────────────────────┘   │  │
│  │ Card Footer (purple-1000) │  │
│  └───────────────────────────┘  │
└─────────────────────────────────┘
```

---

### New Structure

| Concept | Class | Purpose |
|---------|-------|---------|
| **Section** | `.section` | Outer purple containers (was `.card` used as section) |
| **Card** | `.card` | Standard card with optional header/body/footer |
| **Slide** | `.card.slide` | Card used inside carousel |
| **Thumbnail** | `.card.thumbnail` | Interactive selection card (teams) |
| **Row** | `.card.row` | Horizontal inline card (member list) |

### Sub-elements (Inside Any Card)
```html
<div class="card">
    <div class="header">...</div>     <!-- Optional -->
    <div class="body">...</div>       <!-- Main content -->
    <div class="footer">...</div>     <!-- Optional -->
</div>
```

### Content Components (Inside Body - Unchanged)
- `.glass-panel` - Stat boxes with glass effect
- `.leaderboard-table` - Table styling
- `.detail-list` - Label-value pairs (new name for action-card-row pattern)
- `.match-grid` / `.match-preview-grid` - Prediction display

---

## Carousel Considerations (IMPORTANT)

The carousel CSS itself does NOT change. The carousel uses these classes:
- `.carousel-wrapper`
- `.carousel-container`
- `.carousel-track`
- `.carousel-item-wrapper`
- `.carousel-item-content`

**What changes:** The card INSIDE `carousel-item-content` changes from `action-card light` or `leaderboard-card` to `card slide`.

### Carousel-Specific CSS to Update

**File: `leaderboard.css` (line 70-73)**
```css
/* BEFORE */
.carousel-item-content .leaderboard-card {
    width: 100%;
    min-width: 0;
}

/* AFTER */
.carousel-item-content .card.slide {
    width: 100%;
    min-width: 0;
}
```

**File: `action-cards.css` (line 96-100)**
```css
/* BEFORE */
@media (max-width: 768px) {
    .action-card {
        max-width: 350px;
        width: 100%;
    }
}

/* AFTER - Move to card-base.css */
@media (max-width: 768px) {
    .card.slide {
        max-width: 350px;
        width: 100%;
    }
}
```

### Files Using Carousels (Verify After Changes)

| File | Current Card Class | New Card Class |
|------|-------------------|----------------|
| UpcomingRoundsTile.razor | Uses `RoundCard` component | No change needed (RoundCard will be updated) |
| LeaderboardsTile.razor | `leaderboard-card` | `card slide` |
| MonthlyLeaderboardTile.razor | `leaderboard-card` | `card slide` |
| RoundResultsTile.razor | `leaderboard-card` | `card slide` |

---

## CSS File Changes

### Files to Create

#### 1. `section.css` (NEW)
```css
/* Section - Outer container (purple boxes with titles) */
/* Background: purple-600 (#4A2E6C) - Lightest */
.section {
    background-color: var(--pl-purple-600);
    padding: 1.5rem;
    border-radius: var(--bs-border-radius);
    color: white;
    display: flex;
    flex-direction: column;
    box-shadow: 0 8px 20px rgba(0, 0, 0, 0.35), inset 0 0 0 1px rgba(255, 255, 255, 0.05);
}

.section.center-content {
    justify-content: center;
}
```

### Files to Modify

#### 2. `card-base.css` - Complete Rewrite

**Remove:**
- Current `.card` styles (moving to `.section`)
- `.card-header`, `.card-body`, `.card-footer` (will be simplified)
- `.gradient-flex-info` (move to effects.css or keep here)

**Add:**
```css
/* Base Card */
/* Background: purple-1000 (#2C0A3D) - Darkest (same as login form) */
/* ALL card parts (header, body, footer) use the SAME background colour */
.card {
    background-color: var(--pl-purple-1000);
    border-radius: var(--bs-border-radius);
    color: white;
    display: flex;
    flex-direction: column;
    overflow: hidden;
}

/* Card Variants (layout only, NOT colour) */
.card.slide {
    /* Carousel items - same as base, width handled by carousel */
    height: 100%;
}

.card.thumbnail {
    /* Interactive selection cards */
    transition: transform 0.2s ease-in-out, box-shadow 0.2s ease-in-out;
    cursor: pointer;
    text-align: center;
}

.card.row {
    /* Horizontal inline cards */
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    padding: 1rem 1.5rem;
    margin-bottom: 0.75rem;
}

/* NO background variants - all cards use purple-1000 */

/* Card Sub-elements - ALL use same background as card (purple-1000) */
.card > .header {
    padding: 1rem 1.5rem;
    /* NO background-color - inherits from card (purple-1000) */
    display: flex;
    flex-direction: column;
    align-items: center;
}

.card > .header.row-layout {
    flex-direction: row;
    justify-content: space-between;
}

.card > .body {
    padding: 1rem 1.5rem;
    flex-grow: 1;
    display: flex;
    flex-direction: column;
}

.card > .body.no-padding {
    padding: 0;
}

.card > .body.centered {
    align-items: center;
    justify-content: center;
}

.card > .footer {
    padding: 1rem 1.5rem;
    display: flex;
    flex-wrap: wrap;
    gap: 1rem;
}

.card > .footer .btn {
    flex-grow: 1;
}

/* Mobile adjustments */
@media (max-width: 768px) {
    .card.slide {
        max-width: 350px;
        width: 100%;
    }

    .card.row {
        flex-direction: column;
        align-items: center;
        gap: 0.5rem;
    }
}
```

#### 3. `action-cards.css` - DELETE or rename to `detail-list.css`

Keep only the detail-list pattern (label-value rows):

```css
/* Detail List - Label/Value rows inside card body */
.detail-list {
    width: 100%;
}

.detail-row {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding-bottom: 0.75rem;
    margin-bottom: 0.75rem;
}

.detail-row:last-child {
    border-bottom: none;
    margin-bottom: 0;
    padding-bottom: 0;
}

.detail-row dt {
    font-weight: normal;
    color: var(--pl-grey-500);
}

.detail-row dd {
    font-weight: bold;
    margin-bottom: 0;
}

.detail-row code {
    font-size: 1rem;
    font-weight: bold;
    color: var(--pl-white);
}

.detail-row.centered {
    justify-content: center;
}
```

#### 4. `league-cards.css` - DELETE

All styles absorbed into new `card-base.css`. Keep only:
- `.league-grid` (rename to a utility or keep)

#### 5. `leaderboard.css` - UPDATE

Change selector from `.leaderboard-card` to `.card.slide` or just `.card`:

```css
/* BEFORE line 63-68 */
.leaderboard-card .league-card-body,
.leaderboard-card .card-body { ... }

/* AFTER */
.card > .body.leaderboard-body {
    padding: 1.5em;
    flex-grow: 1;
    overflow-y: auto;
}

/* BEFORE line 70-73 */
.carousel-item-content .leaderboard-card { ... }

/* AFTER */
.carousel-item-content .card {
    width: 100%;
    min-width: 0;
}
```

#### 5b. `effects.css` - UPDATE table striping

Update `.table-striped-purple` to use colours that contrast with card background (purple-1000):

```css
/* BEFORE - may have used colours that blend with card */
.table-striped-purple tbody tr:nth-child(odd) { ... }
.table-striped-purple tbody tr:nth-child(even) { ... }

/* AFTER - neither row matches card background (purple-1000) */
.table-striped-purple tbody tr:nth-child(odd) {
    background-color: var(--pl-purple-800);  /* Lighter than card */
}

.table-striped-purple tbody tr:nth-child(even) {
    background-color: var(--pl-purple-900);  /* Lighter than card */
}
```

#### 6. `match-cards.css` - UPDATE

Keep `.match-editor-row`, `.match-logo`, `.logo-wrapper`.
Remove `.match-card`, `.match-card-header`, `.round-card` (absorbed into card variants).

#### 7. `member-cards.css` - UPDATE

Remove `.member-card` (becomes `.card.row`).
Keep `.member-status-column`, `.column-placeholder`.

#### 8. `team-cards.css` - UPDATE

Keep `.team-grid`.
Remove `.team-card` base styles (becomes `.card.thumbnail`).
Keep `.team-card-logo-bg`, `.team-card-logo`, `.team-card-name` but consider renaming:
- `.team-card-logo-bg` → `.thumbnail-image-area`
- `.team-card-body` → use `.card > .body`
- `.team-card-name` → `.thumbnail-title`

---

## Razor File Changes

### Dashboard Tiles (Outer Sections)

| File | Line | Current | New |
|------|------|---------|-----|
| MyLeaguesTile.razor | 71 | `<div class="card h-100">` | `<div class="section h-100">` |
| LeaderboardsTile.razor | 9 | `<div class="card h-100">` | `<div class="section h-100">` |
| UpcomingRoundsTile.razor | 7 | `<div class="card h-100">` | `<div class="section h-100">` |
| PendingRequestsTile.razor | 11 | `<div class="card h-100 d-flex flex-column">` | `<div class="section h-100">` |
| AvailableLeaguesTile.razor | 9 | `<div class="card h-100 d-flex flex-column">` | `<div class="section h-100">` |

### Dashboard Tiles (Inner Cards)

| File | Line | Current | New |
|------|------|---------|-----|
| MyLeaguesTile.razor | 89 | `<div class="action-card dark mb-3">` | `<div class="card mb-3">` |
| PendingRequestsTile.razor | 29 | `<div class="action-card dark mb-3">` | `<div class="card mb-3">` |
| AvailableLeaguesTile.razor | 23 | `<div class="action-card light">` | `<div class="card">` |
| RoundCard.razor | 4 | `<div class="action-card light">` | `<div class="card slide">` |

### League Dashboard Tiles

| File | Line | Current | New |
|------|------|---------|-----|
| RoundResultsTile.razor | 12 | `<div class="card">` | `<div class="section">` |
| RoundResultsTile.razor | 72 | `<div class="leaderboard-card mt-4 mh-600">` | `<div class="card slide mt-4 mh-600">` |
| OverallLeaderboardTile.razor | 8 | `<div class="card w-100 mh-600">` | `<div class="section w-100 mh-600">` |
| MonthlyLeaderboardTile.razor | 9 | `<div class="card w-100 mh-600">` | `<div class="section w-100 mh-600">` |
| MonthlyLeaderboardTile.razor | 30 | `<div class="leaderboard-card">` | `<div class="card slide">` |
| ExactScoresLeaderboardTile.razor | (check) | Similar pattern | Similar changes |
| WinningsSection.razor | 6,50 | `<div class="card">` | `<div class="section">` |
| WinningsLeaderboardTile.razor | 5 | `<div class="card h-100 mh-600">` | `<div class="section h-100 mh-600">` |
| RoundPrizesTile.razor | 5 | `<div class="card mh-600">` | `<div class="section mh-600">` |
| MonthlyPrizesTile.razor | (check) | Similar | Similar |
| EndOfSeasonPrizesTile.razor | (check) | Similar | Similar |
| PrizePotTile.razor | 3 | `<div class="card h-100">` | `<div class="section h-100">` |
| MobileMatchResultCard.razor | 7 | `<div class="leaderboard-card">` | `<div class="card slide">` |
| LeaderboardsTile.razor | 31 | `<div class="leaderboard-card">` | `<div class="card slide">` |

### Admin Pages

| File | Line | Current | New |
|------|------|---------|-----|
| Admin/Rounds/List.razor | 42 | `<div class="action-card light">` | `<div class="card">` |
| Admin/Seasons/List.razor | 37 | `<div class="action-card light">` | `<div class="card">` |
| Admin/Users/List.razor | 33 | `<div class="action-card light">` | `<div class="card">` |
| Admin/Team/List.razor | 31 | `<div class="team-card">` | `<div class="card thumbnail">` |
| Admin/Rounds/Create.razor | 118 | `<div class="d-md-none match-card">` | `<div class="d-md-none card">` |
| Admin/Rounds/Create.razor | 119 | `<div class="... match-card-header">` | `<div class="header">` |

### League Pages

| File | Line | Current | New |
|------|------|---------|-----|
| Members.razor | 35 | `<div class="action-card">` | `<div class="card">` |
| Members.razor | 61,84 | `<div class="member-card">` | `<div class="card row">` |
| LeagueCardList.razor | 13 | `<div class="action-card">` | `<div class="card">` |
| Prizes.razor | 36 | `<div class="action-card light">` | `<div class="card">` |
| Prizes.razor | 62,78 | `<div class="action-card light w-100">` | `<div class="card w-100">` |
| Prizes.razor | 119 | `<div class="action-card purple-600">` | `<div class="card">` |

### Sub-element Class Changes (All Files)

| Current | New |
|---------|-----|
| `card-header` | `header` (when inside .card) |
| `action-card-body` | `body` |
| `league-card-body` | `body` |
| `action-card-footer` | `footer` |
| `league-card-footer` | `footer` |
| `action-card-row` | `detail-row` (inside `detail-list`) |
| `action-card-row--centered` | `detail-row centered` |

---

## Migration Order

### Phase 1: Create New CSS Files
1. Create `section.css`
2. Create `detail-list.css` (content patterns)
3. Update `app.css` imports

### Phase 2: Update card-base.css
1. Move current `.card` styles to `.section`
2. Add new `.card` with variants
3. Add new `.header`, `.body`, `.footer` styles

### Phase 3: Update Other CSS Files
1. Update `leaderboard.css` carousel selectors
2. Update `match-cards.css`
3. Update `member-cards.css`
4. Update `team-cards.css`
5. Delete `action-cards.css` (or rename)
6. Delete `league-cards.css`

### Phase 4: Update Razor Files (By Area)
1. Dashboard tiles (outer sections first)
2. Dashboard tiles (inner cards)
3. League dashboard tiles
4. Admin pages
5. League pages

### Phase 5: Testing
1. Test all carousels (UpcomingRounds, Leaderboards, MonthlyLeaderboard, RoundResults mobile)
2. Test all admin pages
3. Test all league pages
4. Test responsive breakpoints

---

## Files Summary

### CSS Files to Create
- `section.css`
- `detail-list.css`

### CSS Files to Update
- `card-base.css` (major rewrite)
- `leaderboard.css` (selector updates)
- `effects.css` (table striping colours)
- `match-cards.css` (remove absorbed classes)
- `member-cards.css` (remove absorbed classes)
- `team-cards.css` (partial rename)
- `app.css` (import updates)

### CSS Files to Delete
- `action-cards.css`
- `league-cards.css`

### Razor Files to Update (25 files)
- Dashboard: MyLeaguesTile, LeaderboardsTile, UpcomingRoundsTile, PendingRequestsTile, AvailableLeaguesTile, RoundCard
- League Dashboard: RoundResultsTile, OverallLeaderboardTile, MonthlyLeaderboardTile, ExactScoresLeaderboardTile, WinningsSection, WinningsLeaderboardTile, RoundPrizesTile, MonthlyPrizesTile, EndOfSeasonPrizesTile, PrizePotTile, MobileMatchResultCard
- Admin: Rounds/List, Rounds/Create, Seasons/List, Users/List, Team/List
- Leagues: Members, LeagueCardList, Prizes

---

## Verification Checklist

After migration, verify:

### Colours
- [ ] All sections use purple-600 background
- [ ] All cards use purple-1000 background (same as login form)
- [ ] Card headers, bodies, and footers are all the same colour (no darker headers)
- [ ] Table rows alternate between purple-800 and purple-900 (both lighter than card)
- [ ] No table row blends into the card background
- [ ] Glass panels still have gradient effect

### Functionality
- [ ] All carousels still swipe/navigate correctly
- [ ] Carousel dots work
- [ ] Cards display correctly inside carousel-item-content
- [ ] Mobile responsive breakpoints work
- [ ] Admin pages display correctly
- [ ] Team selection grid works with hover states
- [ ] Member cards display horizontally (desktop) and stack (mobile)
- [ ] Leaderboard tables render correctly with visible striping
- [ ] All buttons in footers display correctly
