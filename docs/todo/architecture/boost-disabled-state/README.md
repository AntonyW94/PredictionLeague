# Boost Disabled State: Wire It Up Or Remove It

## Status

**Not Started** | In Progress | Complete

## Summary

`DisabledImageUrl` travels from the `BoostDefinitions` table, through the domain model, through two
query handlers and two DTOs, to the client - which never reads it. Two artwork files exist only to
serve it. Either the disabled state should render, or the whole path should go.

## Priority

**Low.** Nothing is broken and the wasted bytes are trivial. It matters as a consistency problem: a
populated column and a full plumbing path that terminate in nothing is the kind of thing that misleads
whoever next works on boosts.

## What was found, August 2026

Nothing in the client reads `DisabledImageUrl`. The renderers use `ImageUrl`
(`BoostUsageTile.razor`, guarded for empty) and `SelectedImageUrl`. There is no third branch.

The property nonetheless exists in:

| Layer | Where |
|---|---|
| Database | `BoostDefinitions.DisabledImageUrl` |
| Domain | `BoostDefinition` record |
| Queries | `GetBoostCatalogueQueryHandler`, `GetAvailableBoostsQueryHandler` |
| Contracts | `BoostCatalogueItemDto`, `BoostOptionDto` |
| Infrastructure | `BoostReadRepository` |

Two files exist purely to be pointed at by it, and are never rendered:

- `images/boosts/correct-goals-disabled.webp` (20 KB)
- `images/boosts/double-up-disabled.webp` (24 KB)

The gap was noticed because `Predictions.razor` had set `DisabledImageUrl` to
`/images/boosts/none-disabled.png`, **a file that never existed in the repository**. It caused no visible
problem precisely because nothing reads the property. That reference was removed in August 2026 with a
comment explaining why, rather than artwork being invented for it.

## The decision

**Option A - wire it up.** A boost the player cannot use this round (allowance spent, outside its window)
renders in a distinct disabled state rather than looking available. `BoostEligibilityDto` already carries
everything needed: `CanUse`, `Reason`, `RemainingSeasonUses`, `RemainingWindowUses`,
`AlreadyUsedThisRound`, `IsRoundInActiveWindow`, `NextWindowStartRound`. So the data is there and only
the rendering is missing.

This is probably the better product answer - a greyed-out boost with a reason reads better than one that
silently does nothing - but it needs a `none-disabled` asset produced to match the other two, and a look
at whether the existing two still suit the current styling.

**Option B - remove it.** Drop the column, the domain property, the DTO members and the two files. Needs
a migration, and `docs/guides/database-schema.md` updating.

Option A is preferred. Option B is honest if the disabled state is not wanted, and better than leaving a
path that goes nowhere.

## Requirements, if Option A

- [ ] Render the disabled state in the boost picker when `Eligibility.CanUse` is false
- [ ] Surface `Eligibility.Reason` so the player learns *why*
- [ ] Produce `none-disabled.webp` matching the existing two (640x640, WebP, same treatment)
- [ ] Check the two existing disabled images still suit the current design
- [ ] Migration adding the `none` row's `DisabledImageUrl`, if `none` is stored rather than hardcoded

## Requirements, if Option B

- [ ] Migration dropping `BoostDefinitions.DisabledImageUrl`
- [ ] Remove the property from `BoostDefinition`, both DTOs, both query handlers and `BoostReadRepository`
- [ ] Delete the two artwork files
- [ ] Update `docs/guides/database-schema.md`
