# 0009. Platform & data: Competitions table, email identity, deferred challenges

- **Status:** Accepted (Competitions, Email) / Deferred (Season Challenges)
- **Date:** 2026-05-30
- **Deciders:** Antony
- **Tags:** technical, domain, security, legal, product

## Context

Three platform/data decisions emerged that underpin the monetisation feature: a stable competition identity, trustworthy account identity, and where gamification sits.

## Decision

### a) Competitions reference table
Introduce a **`Competitions` table** as the stable, provider-independent competition identity: `Id` (the business key), `Code` (stable slug), `Name`, `LogoAsset` (**hosted** logo, admin-uploaded), `Type` (League/Tournament — **moved from `Season.CompetitionType`**), `ApiLeagueId` (provider id — **nullable, admin-editable**). `Season` **drops `ApiLeagueId` and `CompetitionType`** and gains **`CompetitionId`** (FK). All business rules (the SMS reward's "same competition", the price calculator's comparable season) key on `CompetitionId`; the provider id is resolved from the competition at sync time. So switching fixture provider is a no-deploy admin edit that never invalidates free-SMS entitlements or price comparables. Existing seasons are **backfilled** (one competition per distinct legacy `ApiLeagueId`).

### b) Verified, normalised email identity
**Complete email verification** (the column/store exist but there's no token, send-on-register, confirm endpoint, or login gate): add a confirmation token (mirroring `PasswordResetToken`), a confirm endpoint + Brevo template, and **require a confirmed email before purchasing/taking part**. **Normalise email for uniqueness** — lowercase and **strip the `+suffix`** — so plus-aliases can't farm repeated free trials (0005); store the entered address for delivery, the normalised key for dedup.

### c) Defer Season Challenges
**Defer** earnable badges/challenges tied to a season pass to a separate future feature (stub at `monetisation/season-challenges/`); out of scope for the Season Passes build. Any challenge stays **cosmetic/achievement only** (no pay-to-win, 0002); "Early Bird" dovetails with the SMS reward (0007).

## Consequences

**For / positive**
- Competition identity survives a provider switch; per-competition hosted logos + metadata; single source of truth for provider id and type.
- Verified + normalised email cuts free-trial abuse, improves deliverability/recovery.
- Challenges captured without bloating scope.

**Against / cost**
- Competitions **refactors existing code**: migrate `ApiLeagueId`, move `CompetitionType`, update sync + tournament/round-allocation readers, backfill, drop old columns — care around existing tests.
- Email verification adds a registration step + token/endpoint/template/login gate; universal `+`-stripping slightly over-merges on rare providers (accepted); existing unconfirmed accounts need handling.
- Players wait longer for gamification.

**Neutral / notes**
- `Code` lets code reference a specific competition without a magic id; logo store (DB blob vs folder vs object storage) is an impl detail given Fasthosts hosting.
- Gmail also ignores dots — optional extra dedup normalisation. Google-OAuth users are already confirmed.

## Alternatives considered

- **`Competition` enum** — considered earlier; rejected because it can't hold logos/metadata or be edited without a deploy.
- **Keep `ApiLeagueId`/`CompetitionType` on `Season`** / **external logo URLs** — rejected; provider coupling, split source of truth, want hosted assets.
- **Leave email verification off** / **block any `+` outright** — rejected; abuse + deliverability risk; `+` addresses are legitimate (normalise for dedup instead).
- **Build Season Challenges alongside passes** — rejected; scope creep.

## Related

- 0002, 0005, 0006, 0007; `season-passes/06-domain-season-pass.md`, `07-database-migration.md`, `16-competitions-management.md`, `18-email-verification-and-identity.md`; `monetisation/season-challenges/`.
