# 0018. Competitions reference table (supersedes 0017)

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** technical, domain, product
- **Supersedes:** [0017](./0017-internal-competition-identifier.md)

## Context

[0017](./0017-internal-competition-identifier.md) introduced a `Competition` **enum** to give a stable, provider-independent competition identity (so the free-SMS reward and price comparables survive an API-provider switch). New requirements have emerged that an enum cannot meet:

- per-competition **logos** (and room for other metadata), and
- an **admin page to change the provider's league id** (`ApiLeagueId`) without a deploy.

## Decision

Replace the planned enum with a **`Competitions` reference table** (domain entity + table):

| Column | Role |
|--------|------|
| `Id` | stable internal PK — **the business key** for reward/pricing matching |
| `Code` | stable unique slug (so code can reference a specific competition without a magic id) |
| `Name` | display name |
| `LogoUrl` | competition logo |
| `ApiLeagueId` | provider's league id — **nullable and admin-editable** |

`Season` **drops `ApiLeagueId`** and gains **`CompetitionId`** (FK to `Competitions`). All business rules key on `CompetitionId`; the provider id is **looked up from the competition** at sync time and edited via an admin page.

## Consequences

**For / positive**
- Keeps the stable, provider-independent identity: `CompetitionId` never changes when an admin repoints `ApiLeagueId`, so earned free-SMS entitlements and price comparables are safe across a provider switch.
- Adds **logos + metadata** and **no-deploy** API-id changes.
- Single source of truth for the provider id (on the competition, not duplicated on every season).

**Against / cost**
- More moving parts than an enum (table, repository, admin CRUD).
- **Refactors existing code:** migrate existing `Season.ApiLeagueId` values into `Competitions`, backfill `Season.CompetitionId`, drop the old column, and update the **existing season-sync** handler/readers — with care around existing tests.

**Neutral / notes**
- `Code` lets code reference a specific competition without a magic DB id.
- `CompetitionType` (League/Tournament) could later move from `Season` onto `Competitions` — left on `Season` for now (open question).

## Alternatives considered

- **`Competition` enum (0017)** — superseded; can't hold logos/metadata or be edited without a deploy.
- **Keep `ApiLeagueId` on `Season` as well** — rejected; duplicates the source of truth and re-couples `Season` to the provider.

## Related

- Supersedes 0017; relates to 0010, 0012; `season-passes/06-domain-season-pass.md`, `07-database-migration.md`, `13-sms-earned-upgrade.md`, `15-configurable-prices-and-calculator.md`, `16-competitions-management.md`
