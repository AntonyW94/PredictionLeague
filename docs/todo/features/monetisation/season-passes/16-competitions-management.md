# Task: Competitions Management (entity, admin page, sync refactor)

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Introduce a `Competitions` reference table (ADR 0018) as the stable, provider-independent competition identity — with logos and an **admin-editable API league id** — and refactor `Season` and the fixture sync to use it instead of `Season.ApiLeagueId`.

## Scope note

This **refactors existing code** (the season-sync handler and any `Season.ApiLeagueId` readers), not just new monetisation code. Treat existing sync tests as part of the blast radius.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Domain/Models/Competition.cs` | Create | Reference entity |
| `src/ThePredictions.Application/Repositories/ICompetitionRepository.cs` (+ impl) | Create | CRUD (commands) |
| `...Application/Features/Admin/Competitions/Commands\|Queries/*` | Create | Create/Update/Delete + list |
| `src/ThePredictions.Web.Client/Components/Pages/Admin/Competitions.razor` | Create | Admin CRUD page `/admin/competitions` |
| `...Features/Admin/Seasons/Commands/SyncSeasonWithApiCommandHandler.cs` | Modify | Resolve provider id via `season.Competition.ApiLeagueId` |
| Any `Season.ApiLeagueId` readers | Modify | Use `Competition.ApiLeagueId` instead |
| `Competitions` table + `Seasons.CompetitionId` / drop `ApiLeagueId` | Create | (DDL in Task 07) |

## Implementation Steps

### Step 1: `Competition` entity

```csharp
public class Competition
{
    public int Id { get; init; }
    public string Code { get; private set; }       // stable slug, e.g. "WORLD_CUP", "EPL"
    public string Name { get; private set; }
    public string? LogoUrl { get; private set; }
    public int? ApiLeagueId { get; private set; }  // provider's league id — admin-editable
    public DateTime CreatedAtUtc { get; private set; }

    public void UpdateApiLeagueId(int? apiLeagueId) => ApiLeagueId = apiLeagueId;  // no deploy needed
    // + Create factory and UpdateDetails (Code unique, Name not blank)
}
```

### Step 2: Admin Competitions page (`/admin/competitions`)

- List competitions; create/edit Name, Code, **logo**, and **API league id**.
- Editing the API id is the **no-deploy provider repoint** — `CompetitionId` (the business key) is untouched, so free-SMS entitlements (Task 13) and price comparables (Task 15) are unaffected.

### Step 3: `Season` uses `CompetitionId`; sync resolves the API id

- `Season.CompetitionId` (FK) replaces `Season.ApiLeagueId` (Tasks 06–07).
- `SyncSeasonWithApiCommandHandler` reads the provider id from the season's `Competition.ApiLeagueId` (load the competition, or join in the query feeding the handler).
- Update any other reader of `Season.ApiLeagueId` accordingly.

### Step 4: Logos

- Surface `Competition.LogoUrl` wherever a competition is shown (season lists, dashboards). Logo hosting (URL vs uploaded asset) is a UI detail — start with a URL field.

## Verification

- [ ] Admin can create/edit competitions incl. logo and API id.
- [ ] Changing a competition's API id does **not** change its `Id` (entitlements/comparables intact).
- [ ] Season sync works using the resolved `Competition.ApiLeagueId`; existing sync tests pass (updated as needed).
- [ ] `Season.ApiLeagueId` is fully removed; no remaining readers.
- [ ] Domain coverage 100% for `Competition`.

## Edge Cases to Consider

- A competition with **no** `ApiLeagueId` set (manual-only season) — sync should skip/неwarn gracefully.
- Backfill must create exactly one competition per distinct legacy `ApiLeagueId` (Task 07).
- Duplicate `Code` rejected (unique).

## Open Questions

- [ ] Should `CompetitionType` (League/Tournament) move from `Season` onto `Competition`? (Recommended later; kept on `Season` for now to contain scope — ADR 0018.)
- [ ] Logo storage: external URL vs uploaded/hosted asset.

## Related

- ADR 0018 (supersedes 0017); Tasks 06, 07, 13, 15.
