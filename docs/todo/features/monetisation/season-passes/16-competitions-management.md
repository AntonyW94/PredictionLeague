# Task: Competitions Management (entity, admin page, sync refactor)

**Parent Feature:** [README.md](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Introduce a `Competitions` reference table (ADR 0017) as the stable, provider-independent competition identity — with a **hosted logo**, a **`Type`** (League/Tournament, moved off `Season`), and an **admin-editable API league id** — and refactor `Season` and the fixture sync to use it instead of `Season.ApiLeagueId` / `Season.CompetitionType`.

## Scope note

This **refactors existing code** — the season-sync handler and any readers of `Season.ApiLeagueId`, `Season.CompetitionType`, or `Season.IsTournament` (e.g. round-allocation/tournament logic). Treat existing **sync and tournament** tests as part of the blast radius.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Domain/Models/Competition.cs` | Create | Reference entity |
| `src/ThePredictions.Application/Repositories/ICompetitionRepository.cs` (+ impl) | Create | CRUD (commands) |
| `...Application/Features/Admin/Competitions/Commands\|Queries/*` | Create | Create/Update/Delete + list |
| `src/ThePredictions.Web.Client/Components/Pages/Admin/Competitions.razor` | Create | Admin CRUD page `/admin/competitions` |
| `...Features/Admin/Seasons/Commands/SyncSeasonWithApiCommandHandler.cs` | Modify | Resolve provider id via `season.Competition.ApiLeagueId` |
| Any `Season.ApiLeagueId` / `Season.CompetitionType` / `Season.IsTournament` readers | Modify | Use `Competition.ApiLeagueId` / `Competition.Type` instead (e.g. round-allocation/tournament logic) |
| Logo upload/hosting (admin) | Create | Store the uploaded competition logo as a hosted asset, served from our domain |
| `Competitions` table + `Seasons.CompetitionId` / drop `ApiLeagueId` + `CompetitionType` | Create | (DDL in Task 07) |

## Implementation Steps

### Step 1: `Competition` entity

```csharp
public class Competition
{
    public int Id { get; init; }
    public string Code { get; private set; }        // stable slug, e.g. "WORLD_CUP", "EPL"
    public string Name { get; private set; }
    public CompetitionType Type { get; private set; }// League / Tournament (moved off Season)
    public string? LogoAssetPath { get; private set; } // hosted asset (uploaded via admin)
    public int? ApiLeagueId { get; private set; }   // provider's league id — admin-editable
    public DateTime CreatedAtUtc { get; private set; }

    public bool IsTournament => Type == CompetitionType.Tournament;  // moved from Season

    public void UpdateApiLeagueId(int? apiLeagueId) => ApiLeagueId = apiLeagueId;  // no deploy needed
    public void SetLogo(string assetPath) => LogoAssetPath = assetPath;
    // + Create factory and UpdateDetails (Code unique, Name not blank)
}
```

The existing `CompetitionType` enum is reused here (just relocated from `Season`).

### Step 2: Admin Competitions page (`/admin/competitions`)

- List competitions; create/edit Name, Code, **logo**, and **API league id**.
- Editing the API id is the **no-deploy provider repoint** — `CompetitionId` (the business key) is untouched, so free-SMS entitlements (Task 13) and price comparables (Task 15) are unaffected.

### Step 3: `Season` uses `CompetitionId`; sync + type resolve from the competition

- `Season.CompetitionId` (FK) replaces both `Season.ApiLeagueId` and `Season.CompetitionType` (Tasks 06–07).
- `SyncSeasonWithApiCommandHandler` reads the provider id from `season.Competition.ApiLeagueId`.
- **Move `IsTournament`/type logic to the competition:** update every reader of `Season.CompetitionType` / `Season.IsTournament` (round-allocation, tournament handling) to use `season.Competition.Type` / `Competition.IsTournament`.

### Step 4: Logos (hosted assets)

- Logos are **hosted by us**: the admin **uploads** an image, we store it and serve it from our domain; `Competition.LogoAssetPath` points to it. Surface it wherever a competition is shown (season lists, dashboards).
- Storage mechanism (DB blob vs persistent folder vs object storage) is an implementation detail to confirm given Fasthosts hosting (see Open Questions).

## Verification

- [ ] Admin can create/edit competitions incl. logo and API id.
- [ ] Changing a competition's API id does **not** change its `Id` (entitlements/comparables intact).
- [ ] Season sync works using the resolved `Competition.ApiLeagueId`; existing sync tests pass (updated as needed).
- [ ] `Season.ApiLeagueId` is fully removed; no remaining readers.
- [ ] Domain coverage 100% for `Competition`.

## Edge Cases to Consider

- A competition with **no** `ApiLeagueId` set (manual-only season) — sync should skip/warn gracefully.
- Backfill must create exactly one competition per distinct legacy `ApiLeagueId` (Task 07).
- Duplicate `Code` rejected (unique).

## Open Questions

- [ ] **Logo hosting mechanism** — given Fasthosts shared hosting (FTP deploy), where do uploaded logos persist: DB blob served via an endpoint, a persistent uploads folder (survives deploys?), or object storage? Pick one before building Step 4.

## Related

- ADR 0017; Tasks 06, 07, 13, 15.
