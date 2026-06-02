# Task: Create/Edit commands & write-once

**Parent Feature:** [Link to README.md](./README.md)

## Status

**Not Started**

## Goal

Let an admin set the prize scheme through Create (new leagues) and once through
Edit (existing schemeless leagues), enforce write-once + the site-admin override,
and relax the entry-fee/stake rules the scheme needs.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/.../Features/Leagues/Commands/CreateLeagueCommand.cs` (+Handler) | Modify | Accept scheme + boost selections; call `League.SetPrizeScheme` |
| `src/.../Features/Leagues/Commands/UpdateLeagueCommand.cs` (+Handler) | Modify | Allow scheme set **only while unset**; site-admin may override |
| `src/.../Features/Leagues/Commands/SetPrizeSchemeCommand.cs` (+Handler) | Create (optional) | Dedicated write-once set/override path |
| `src/ThePredictions.Contracts/Leagues/*Request.cs` | Modify | Add `PrizeScheme` payload (categories + per-entry £) |
| `src/ThePredictions.Validators/.../PrizeSchemeRequestValidator.cs` | Create | Whole-pound stake, allocations sum to stake, gated categories valid |
| `src/.../Features/Leagues/Commands/DefinePrizeStructureCommandHandler.cs` | Modify | Demote to site-admin-only manual override (keep, no longer primary) |

## Implementation Steps

### Step 1: write-once enforcement

- `League.SetPrizeScheme` throws if already set (task 1). The Update handler:
  - if `league.PrizeScheme is null` → allow set (any league admin);
  - else if caller is **site admin** → `OverridePrizeScheme`;
  - else → `UnauthorizedAccessException` / validation error "already set".
- Mirror the existing site-admin check in `DefinePrizeStructureCommandHandler`
  (`IUserManager.IsInRoleAsync(user, RoleNames.Administrator)`).

### Step 2: stake rules

- When prizes are on, enforce **whole-pound stake** (validator). Keep the existing
  "price locked once members joined" rule for the fee itself; the scheme is gated
  by its own write-once rule, not member count (so the WC league can be set
  despite having members).

### Step 3: validators

- Allocations: each whole £ ≥ 0, sum == stake, ≥1 category > 0.
- Category availability matches competition type (season vs tournament).
- Optional rank-table override: sums to 100, descending, no zero-but-listed place.

## Code Patterns to Follow

- Command/handler/validator structure per `docs/guides/checklists/new-command.md`.
- `ITransactionalRequest` like `CreateLeagueCommand`.
- Logging format: `"League (ID: {LeagueId}) prize scheme set"`.

## Verification

- [ ] New league: scheme persists at creation and Edit shows it locked.
- [ ] Schemeless league: Edit sets it once, then locks.
- [ ] Non-site-admin cannot change a set scheme; site-admin can override.
- [ ] Validator rejects pence stakes / mis-summed allocations / wrong-competition
      categories.
- [ ] Unit tests for handlers + validators; build clean (warnings-as-errors,
      `CancellationToken.None` in tests).

## Edge Cases to Consider

- League admin changes the entry fee before the scheme is set → recompute
  allocation defaults.
- Site-admin override after members joined → allowed; log it.

## Notes

This task changes the **deadline lock**: the scheme is set up front, not after the
deadline. `DefinePrizeStructure` survives only as a manual site-admin escape hatch.
