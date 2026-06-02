# Task: Boost admin configuration

**Parent Feature:** [Link to README.md](./README.md)

## Status

**Not Started**

## Goal

Give admins a way to choose which boosts a league offers (and their per-season
limits / windows) from Create/Edit. Today `LeagueBoostRules` /
`LeagueBoostWindows` are **DB-only** — there is no write path or admin UI.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/.../Features/Boosts/Queries/GetBoostCatalogueQuery.cs` (+Handler) | Create | List all `BoostDefinitions` for the toggles UI (read via `IApplicationReadDbConnection`) |
| `src/.../Features/Boosts/Commands/SetLeagueBoostRulesCommand.cs` (+Handler) | Create | Write `LeagueBoostRules` (+`Windows`) for a league |
| `src/ThePredictions.Application/Repositories/ILeagueBoostRuleRepository.cs` | Create | Write path for rules/windows |
| `src/ThePredictions.Infrastructure/.../LeagueBoostRuleRepository.cs` | Create | Implementation |
| `src/ThePredictions.Contracts/Boosts/LeagueBoostSelectionDto.cs` | Create | `BoostCode`, `IsEnabled`, `TotalUsesPerSeason`, optional windows |
| `src/ThePredictions.API/Controllers/BoostsController.cs` | Modify | Admin endpoints: GET catalogue, PUT league boost selection |
| `src/ThePredictions.Validators/...` | Create | Validate uses ≥ 0, windows within season round range |

## Implementation Steps

### Step 1: catalogue query

- Read `BoostDefinitions` (Code, Name, Tooltip, images) for the create/edit UI.

### Step 2: set command

- Upsert `LeagueBoostRules` per `(LeagueId, BoostDefinitionId)` (table has a unique
  constraint); replace `LeagueBoostWindows` for the rule.
- Subject to the **same write-once rule as the prize scheme** (set at creation,
  locked thereafter, site-admin override) so boosts and prizes are fixed together.

### Step 3: authorisation

- League admin while unset; site admin to override (mirror task 4).

## Code Patterns to Follow

- Query handler uses `IApplicationReadDbConnection`; command uses the repository.
- Endpoint auth via `ILeagueMembershipService.EnsureLeagueAdministratorAsync`
  (+ site-admin role check for override) as in `LeaguesController`.

## Verification

- [ ] Admin can enable/disable boosts and set per-season uses at creation; locked
      after.
- [ ] Eligibility (`BoostEligibilityEvaluator`) reads the newly written rules
      correctly end-to-end.
- [ ] Unit tests for command/handler/validator; build clean.

## Edge Cases to Consider

- Disabling a boost mid-season is **not** allowed (write-once) — only via
  site-admin override.
- Windows referencing rounds outside the season → validation error.

## Notes

Boost selection and the prize scheme share the write-once lifecycle and the same
Create/Edit section (task 6).
