# Task: Persistence & schema

**Parent Feature:** [Link to README.md](./README.md)

## Status

**Not Started**

## Goal

Persist the prize scheme: new tables, repository write path (commands only),
Dapper hydration into `League`, and the mandatory schema-doc + DatabaseTools
updates.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `docs/guides/database-schema.md` | Modify | Document `LeaguePrizeScheme` + `LeaguePrizeSchemeEntries` (single source of truth) |
| `tools/ThePredictions.DatabaseTools/DatabaseRefresher.cs` | Modify | Add new tables to `TableCopyOrder` in FK-safe position |
| `src/ThePredictions.Infrastructure/.../LeagueRepository.cs` | Modify | Persist/hydrate the scheme with the league |
| `src/ThePredictions.Infrastructure/.../*PrizeScheme*` | Create | Dapper mapping for scheme + entries |

## Implementation Steps

### Step 1: tables

- `LeaguePrizeScheme`: `Id`, `LeagueId` (FK, unique — one per league),
  `OverallFivePoundThreshold` (money), `SetAtUtc` (write-once marker),
  `SetByUserId`.
- `LeaguePrizeSchemeEntries`: `Id`, `LeaguePrizeSchemeId` (FK, cascade),
  `Category` (nvarchar/enum), `PerEntryPounds` (int), `RankTableJson` (nullable —
  per-league override of the places table).
- Brackets, PascalCase, FK constraints per `docs/guides/database.md`.

### Step 2: migration SQL (chat only)

Per CLAUDE.md, **do not commit a `.sql` file**. Present the **additive** migration
(new tables, nullable columns) in chat for the user to run. State that it is
additive and safe to apply ahead of the code deploy.

### Step 3: repository

- Commands use the repository (never `IApplicationReadDbConnection`).
- Insert/replace scheme + entries inside the existing league transaction.
- Hydrate `League.PrizeScheme` via constructor on load.

### Step 4: DatabaseTools

- Add both tables to `TableCopyOrder` after `Leagues`.
- No personal data → no `DataAnonymiser` / `PersonalDataVerifier` changes (confirm:
  `SetByUserId` is a non-sensitive FK already covered by user anonymisation).

## Code Patterns to Follow

- SQL conventions (`docs/guides/database.md`): bracketed names, aliases, one
  column per line, parameterised.
- CQRS: writes via repo only.

## Verification

- [ ] `database-schema.md` updated and matches the tables exactly.
- [ ] DatabaseTools refresh runs without FK errors.
- [ ] Round-trip test: save a scheme, reload the league, scheme matches.
- [ ] Build clean with warnings-as-errors.

## Edge Cases to Consider

- League with no scheme (legacy) → `PrizeScheme` is null everywhere downstream.
- Replacing a scheme via site-admin override → entries replaced atomically.

## Notes

`LeaguePrizeSettings` / `Winnings` / `LeaguePayouts` are **untouched** here — they
remain the frozen settlement artefacts produced at the deadline (task 8).
