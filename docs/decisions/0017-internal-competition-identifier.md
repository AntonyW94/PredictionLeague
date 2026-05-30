# 0017. Competitions reference table (stable competition identity)

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** technical, domain, product

## Context

`Season` carries `ApiLeagueId` (the external fixture provider's id) and `CompetitionType` (League/Tournament). Two business rules need to match seasons to a **competition**:
- the free-SMS early-bird reward, which applies to the **next season of the same competition** (0010), and
- the recommended-price calculator's **comparable-season** denominator (0012).

Keying these on `ApiLeagueId` couples business logic to the provider — a provider switch or renumber would **invalidate earned free-SMS entitlements** and break price comparables. We also want **per-competition logos** and the ability to **change a provider's league id without a deploy**.

## Decision

Introduce a **`Competitions` reference table** (domain entity + table) as the stable, provider-independent competition identity:

| Column | Role |
|--------|------|
| `Id` | stable internal PK — **the business key** for reward/pricing matching |
| `Code` | stable unique slug (reference a competition in code without a magic id) |
| `Name` | display name |
| `LogoAsset` | **hosted** logo asset (uploaded via admin, stored and served by us) |
| `Type` | League / Tournament — **moved from `Season.CompetitionType`** |
| `ApiLeagueId` | provider's league id — **nullable and admin-editable** |

`Season` **drops `ApiLeagueId` and `CompetitionType`** and gains **`CompetitionId`** (FK). All business rules key on `CompetitionId`; the provider id and competition type are read from the competition. The provider id is editable via an admin page.

## Consequences

**For / positive**
- Stable identity survives a provider switch: `CompetitionId` never changes when an admin edits `ApiLeagueId`, so free-SMS entitlements and price comparables are safe.
- **Per-competition logos** (hosted) + metadata; **no-deploy** API-id changes.
- Single source of truth for both the provider id and the competition type.

**Against / cost**
- More moving parts than a plain field (table, repository, admin CRUD, logo hosting).
- **Refactors existing code:** migrate `Season.ApiLeagueId` into `Competitions`, **move `CompetitionType` onto `Competitions.Type`** and update every `Season.CompetitionType`/`IsTournament` reader (e.g. round-allocation/tournament logic), backfill `Season.CompetitionId`, then drop the old columns — with care around existing sync **and tournament** tests.

**Neutral / notes**
- `Code` lets code reference a specific competition without a magic DB id.
- Logos are **hosted assets** (admin upload), not external URLs; the exact store (DB blob vs persistent folder vs object storage) is an implementation detail to confirm given Fasthosts hosting.

## Alternatives considered

- **`Competition` enum** — considered earlier in planning; rejected because it can't hold logos/metadata or be edited without a deploy. (Recorded here as an alternative rather than a separate superseded ADR, since the enum was never an established decision — see the supersede policy in the README.)
- **Keep `ApiLeagueId`/`CompetitionType` on `Season`** — rejected; provider coupling and a split source of truth.
- **External logo URLs** — rejected; we want hosted assets we control.

## Related

- 0010, 0012; `season-passes/06-domain-season-pass.md`, `07-database-migration.md`, `13-sms-earned-upgrade.md`, `15-configurable-prices-and-calculator.md`, `16-competitions-management.md`
