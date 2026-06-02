# Task: Create/Edit UI — "Prizes & Boosts"

**Parent Feature:** [Link to README.md](./README.md)

## Status

**Not Started**

## Goal

Add a "Prizes & Boosts" section to the league Create/Edit pages: category toggles
(gated by competition type), per-entry **pound sliders with a live derived-prize
preview**, boost checkboxes, and the write-once read-only state.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/.../Web.Client/Components/Pages/Leagues/Create.razor` | Modify | Add the section (always editable here) |
| `src/.../Web.Client/Components/Pages/Leagues/Edit.razor` | Modify | Section editable only while scheme unset; else read-only |
| `src/.../Web.Client/Components/Leagues/PrizeSchemeEditor.razor` | Create | Toggles + sliders + live preview |
| `src/.../Web.Client/Components/Leagues/BoostSelector.razor` | Create | Boost checkboxes from the catalogue |
| `src/.../Web.Client/Components/Leagues/PrizeBreakdownPreview.razor` | Create | Renders the derived prize amounts at current N |
| `*.razor.css` | Create | Per the new-css-file checklist |

## Implementation Steps

### Step 1: toggles + gating

- Show Overall / Round / Most Exact Scores always; Section only for tournaments;
  Monthly only for seasons (from the create-data payload / league competition).

### Step 2: pound sliders + live preview

- One slider per enabled category; constrained to total the stake (whole pounds).
- On change, call the evaluator (via an API endpoint from task 7) and render the
  derived prizes ("£4/round, £48 exact, £220/£120/£90 overall, £25/month") at the
  current entrant count so the admin tunes to a shape they like.
- Default the sliders from the registry's recommended allocation.

### Step 3: write-once state

- In Edit, if the scheme is set, render the whole section read-only with a note
  ("Prizes & boosts are locked once set"). Brand-new leagues set it at creation.

## Code Patterns to Follow

- Blazor + `BaseFormComponent` conventions (`src/.../Web.Client/CLAUDE.md`,
  `docs/guides/checklists/new-blazor-component.md`).
- Reuse the disabled-field/hint pattern already in `Edit.razor` (price/scoring
  locks).
- UK English throughout.

## Verification

- [ ] Toggles gate correctly by competition type.
- [ ] Sliders always total the stake; preview updates live and matches the engine.
- [ ] Edit shows the section locked once set; Create always editable.
- [ ] Renders correctly on mobile; dark-mode styles consistent with recent UI work.

## Edge Cases to Consider

- Free league → section shows "informational only, no prizes".
- Changing the stake re-defaults the allocation and preview.
- Many categories on a small stake → preview shows some places not lighting up.

## Notes

The same section hosts both the prize scheme (task 4) and boost selection
(task 5), saved together under the shared write-once lifecycle.
