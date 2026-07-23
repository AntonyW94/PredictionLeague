# Achievements & Badges

## Status

Not Started | **In Progress** | Complete

## Summary

A gamification layer that awards **badges** to players for accomplishments in the game, to bring newcomers immediate wins and give everyone a reason to keep coming back. Badges are earned from round one, are purely cosmetic (respecting [ADR 0002](../../../../decisions/) - no pay-to-win), and are backfilled retrospectively so returning players open the new season to a full trophy cabinet.

Users call them "badges"; in code the feature is namespaced **`Badge`/`Badges`**. The CSS is prefixed `.tp-badge-*` because the existing `.badge` class is already used by the leaderboard status pills.

## Key decisions (locked)

- **20 badges, all global.** Every badge is earned once, in *any* league, and counts for that badge - never per-league. One trophy cabinet per person.
- **Retrospective backfill**, run once (no active season currently, so it is safe). Historical badges are stamped with the real achievement date (see Backdating). No `IsBackfilled` flag needed - digest emails only fire for *newly* awarded badges during a live round completion, and the idempotent award skips pre-existing rows.
- **Thresholds calibrated against two seasons of real data** (dev DB): Sharpshooter 3/4/5 (7.6% -> 3.2% -> 0.9% of user-rounds), On Fire 3/5/7 consecutive (record streak: 14).
- **Colour semantics:** green = your progress / earned; medal bronze->silver->gold = tier climbed & honours (placings); grey = locked.
- **Dashboard:** a full-width carousel tile (overall progress bar + recent-first, then closest-to-next; arrow navigation, not a scrollbar) tagged `data-tab="play"`, placed beneath Active Rounds. Links to a full badges page (Collections / Badges / Honours sections). Newly earned badges also appear in the round-results digest email.

## Badge catalogue

Tiers are separate badge keys; the UI groups a collection into one slot showing the highest tier + progress to next. Progress values are computed live on read, never stored.

### Collections (levelled)

| Key | Name | Earned by | Scope |
|-----|------|-----------|-------|
| `marksman-1/2/3` | Marksman I/II/III | 5 / 10 / 15 exact scores in a season | per-season |
| `sharpshooter-1/2/3` | Sharpshooter I/II/III | 3 / 4 / 5 exact scores in one round | per-round |
| `on-fire-1/2/3` | On Fire I/II/III | Exact score in 3 / 5 / 7 consecutive rounds | lifetime |
| `socialite-1/2/3` | Socialite I/II/III | Join 1 / 3 / 5 leagues (all-time) | lifetime |

### Badges (one-offs)

| Key | Name | Earned by | Scope |
|-----|------|-----------|-------|
| `off-the-mark` | Off the Mark | Submit your first-ever predictions | lifetime |
| `first-blood` | First Blood | Your first-ever exact score | lifetime |
| `on-the-board` | On the Board | First round you score points in | lifetime |
| `beat-the-crowd` | Beat the Crowd | Back the minority result in a match and get the result right (min. 5-strong crowd) | per-round |
| `ever-present` | Ever-Present | Predict every match of a full season | per-season |

### Honours (placings, medal-coloured)

| Key | Name | Earned by | Scope |
|-----|------|-----------|-------|
| `champion` | Champion | Win a league | lifetime |
| `podium` | Podium | Finish top 3 in a league | lifetime |
| `round-winner` | Round Winner | Finish 1st in a round (any league) | per-round |

## Data model

New table **`UserBadges`** (one row per earned badge):

| Column | Type | Notes |
|--------|------|-------|
| `Id` | int identity PK | |
| `UserId` | nvarchar(450) FK -> AspNetUsers (ON DELETE CASCADE) | |
| `BadgeKey` | nvarchar(50) | e.g. `marksman-2` |
| `AwardedUtc` | datetime2 | real achievement date (backdated for retro) |
| `LeagueId` | int NULL FK -> Leagues (NO ACTION) | provenance for caption |
| `RoundId` | int NULL FK -> Rounds (NO ACTION) | scope for per-round badges |
| `SeasonId` | int NULL FK -> Seasons (NO ACTION) | scope for per-season badges |
| `Detail` | nvarchar(100) NULL | caption extra (e.g. "5", "7 in a row", season name) |

**Idempotency:** one unique index `(UserId, BadgeKey, RoundId, SeasonId)`. SQL Server treats NULLs as equal in a unique index, so a single index covers all three repeat-scopes:

- **lifetime** one-time: RoundId NULL + SeasonId NULL -> one row ever.
- **per-round** (Sharpshooter, Round Winner, Beat the Crowd): RoundId set, SeasonId NULL -> one row per round.
- **per-season** (Marksman, Ever-Present): SeasonId set, RoundId NULL -> one row per season.

Only `UserId` cascades (GDPR delete). The provenance/scope FKs are `NO ACTION` to avoid SQL Server multiple-cascade-path errors.

## Backdating (retrospective dates)

Every badge maps to a dated event, so the backfill replays history chronologically and stamps the real date:

- **Round-triggered** (First Blood, On the Board, Sharpshooter, On Fire, Round Winner, Beat the Crowd, each Marksman threshold): `Rounds.CompletedDateUtc` of the round that earned it (for Marksman, the round where the cumulative season count first crossed the threshold).
- **Season-triggered** (Champion, Podium, Ever-Present): the season's final round completion date.
- **Socialite:** the join date of their 1st / 3rd / 5th league membership.
- **Off the Mark:** `MIN(UserPredictions.CreatedAtUtc)` (the field exists).

## Evaluation

`EvaluateBadgesForRoundCommand(RoundId)` dispatched from `UpdateMatchResultsCommandHandler` in the round-complete block, **after** the `ProcessPrizesCommand` loop and **before** `SendRoundDigestEmailsCommand` (so newly earned badges ride the digest). It runs every badge rule for the round across all leagues in the season, awards new badges idempotently (dedup global), and returns the list of newly awarded badges for the digest.

Data sources (mostly existing aggregates):
- Exact-score based (First Blood, On the Board, Sharpshooter, Marksman, On Fire): `RoundResults` (`ExactScoreCount`, `TotalPoints`) - already global per user per round.
- Round Winner: `LeagueRoundResults` RANK per league per round.
- Champion / Podium: final `LeagueRoundResults` cumulative RANK at season end.
- Beat the Crowd + On Fire + Ever-Present + Marksman-crossing: new SQL in the evaluation handler (streaks, minority-result, season-completeness, cumulative-crossing).

`BackfillBadgesCommand` runs the same rules over all completed rounds/seasons, silent (awards only), stamping backdated `AwardedUtc`. Triggered by an admin/task endpoint, run once.

## Read model

- `GetUserBadgesQuery(UserId)` -> `UserBadgesDto` (all earned + live progress for the full page).
- `GetBadgesTileQuery(UserId)` -> `BadgesTileDto` (earned count / total, plus the ordered carousel window: last-10-days first, then closest-to-next).
- `BadgeCatalogue` (code registry, mirrors `OnboardingStepRegistry`) defines every badge's key/name/description/glyph/tier/scope and composes the DTOs from earned rows + live progress. Adding a badge needs no migration.

## Build order (file-by-file)

1. **Migration** `0004_CreateUserBadges.sql`; update `docs/guides/database-schema.md`; add `UserBadges` to `DatabaseTools` (`TableCopyOrder`, no anonymisation needed - no personal data beyond `UserId`).
2. **Contracts:** `BadgeKeys`, `BadgeScope`, `BadgeDto`, `UserBadgesDto`, `BadgesTileDto`.
3. **Domain:** `AwardedBadge` entity (`Create` factory + guards + `IDateTimeProvider`) + 100% unit tests.
4. **Application:** `BadgeCatalogue` registry; `IUserBadgeRepository` + evaluation query building blocks.
5. **Infrastructure:** `UserBadgeRepository` (idempotent insert) + DI registration.
6. **Evaluation:** `EvaluateBadgesForRoundCommand` + handler (per-badge rules); hook into `UpdateMatchResultsCommandHandler`; `BackfillBadgesCommand` + handler + task endpoint.
7. **Queries:** `GetUserBadgesQuery`, `GetBadgesTileQuery` handlers.
8. **API:** `BadgesController` (`GET /api/badges`, `GET /api/badges/tile`).
9. **Digest:** include newly earned badges in `SendRoundDigestEmailsCommand`.
10. **Client:** `BadgeService` (typed HttpClient); `BadgesTile.razor` (dashboard, `data-tab="play"`); `Badges.razor` full page + nav entry; `tp-badges.css` (+ `app.css` import and `.csproj` bundle entry); dark-theme overrides.
11. **Verify:** `dotnet build /p:TreatWarningsAsErrors=true`; domain coverage 100%; calibrate Beat the Crowd crowd-size against data.

## Requirements

- [x] Achievement definitions (locked above)
- [x] Badge display (tile + full page)
- [x] Progress tracking (live, computed on read)
- [x] Retrospective backfill (`BackfillBadgesCommand` + `POST /api/external/tasks/backfill-badges`)
- [ ] Unlock notifications (digest email line) - see follow-ups

## Status of the build

Delivered on branch `achievements-badges` (builds clean under
`/p:TreatWarningsAsErrors=true`; Domain coverage 100% line + branch; all evaluation
SQL executed read-only against the dev DB and returns sensibly):

- Migration `0004_CreateUserBadges.sql`, schema docs, DatabaseTools copy order.
- Contracts, `AwardedBadge` domain entity (+ tests), `UserBadgeRepository`.
- `BadgeCatalogue`, read model (`BadgeStateQueries`) and API (`GET /api/badges`,
  `GET /api/badges/tile`).
- `BadgeEvaluationRepository`, `EvaluateBadgesForRoundCommand` (hooked into round
  completion after prizes, before the digest), `BackfillBadgesCommand` + task endpoint.
- Frontend: `BadgeService`, `BadgeIcon`, dashboard `BadgesTile` (progress bar + arrow
  carousel, full-width Play-tab row), `/badges` page, nav link, `tp-badges.css` (light + dark).

### Follow-ups (not yet done)

- **Digest email line.** Evaluation runs *before* the digest sends so the data is
  available, but the digest email body/template does not yet render newly earned badges -
  it needs the newly awarded list threaded through `SendRoundDigestEmailsCommand` and a
  Brevo template tweak.
- **Deploy steps (manual, when ready):** apply the migration (additive, safe ahead of the
  code deploy), then run the backfill task endpoint once.
- End-to-end run in the app against a seeded DB has not been done (build + read-only SQL
  checks only).
