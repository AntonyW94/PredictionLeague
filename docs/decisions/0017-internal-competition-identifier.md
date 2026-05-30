# 0017. Stable internal Competition identifier (decouple from API league id)

- **Status:** Accepted
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** technical, domain

## Context

`Season` carries an `ApiLeagueId` from the external football API (api-sports.io) used to sync fixtures. Two business rules need to match seasons to a **competition**:
- the free-SMS early-bird reward, which applies to the **next season of the same competition** (0010), and
- the recommended-price calculator's **comparable-season** denominator (0012).

Keying these on `ApiLeagueId` couples business logic to the provider. If we **switch API provider** (or the provider renumbers a league), the identifier changes — which would **invalidate earned free-SMS entitlements** and break price comparables.

## Decision

Introduce a domain **`Competition` enum** (initial values: `WorldCup`, `EnglishPremierLeague`, `EnglishChampionship`, `ChampionsLeague`, `EuropaLeague`; extend as needed) and add it to `Season`. **All business logic keys on `Competition`**, never on `ApiLeagueId`. `ApiLeagueId` is retained **only** as a provider mapping for sync at the infrastructure boundary; switching provider updates that mapping, not the `Competition`.

## Consequences

**For / positive**
- Free-SMS entitlements and price comparables survive an **API-provider switch** — the core motivation.
- Type-safe, explicit, and consistent with existing enums (`CompetitionType`).
- Cleanly separates domain identity from third-party identifiers.

**Against / cost**
- Adding a new competition needs an **enum value + deploy** (acceptable for a small, owner-controlled set).
- Existing seasons must be **backfilled** with a `Competition` value in the migration.

**Neutral / notes**
- `Competition` (specific competition) is distinct from `CompetitionType` (League vs Tournament) — they complement each other.
- The reward's "same competition" check and the calculator's comparable-season lookup both use `Competition`.

## Alternatives considered

- **Key on `ApiLeagueId`** — rejected; provider coupling is the very problem being solved.
- **Key on `CompetitionType`** — rejected; too coarse (all leagues collapse into one).
- **`Competitions` reference table now** — deferred; the enum is simpler and sufficient. Migrate to a table later if admin-managed competitions or per-competition metadata (logos, default scoring) are needed.

## Related

- 0010, 0012; `Season`, `season-passes/06-domain-season-pass.md`, `07-database-migration.md`, `13-sms-earned-upgrade.md`, `15-configurable-prices-and-calculator.md`
