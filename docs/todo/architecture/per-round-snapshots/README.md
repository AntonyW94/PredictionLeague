# Feature: Per-Round Historical Snapshots

## Status

**Not Started** | In Progress | Complete

## Priority

**Tier 2 - Foundation**. Capture-going-forward must ship before the next round closes; backfill can follow days later.

## Summary

Capture the irreversibly-derived state of each league at the close of each round so that future analytics features (round results email, season recap, statistics dashboard, prediction history, head-to-head, achievements) have a clean time series to query against. Without this, every subsequent round overwrites the only record of what users' ranks were at that point in time.

Two new tables - one per-user-per-league-per-round, one per-league-per-round. Backfilled in a single one-shot pass against the existing 34 rounds across 2 leagues using the existing scoring/boost logic, then maintained going forward by hooking into the round-completion flow.

## Why now

- **Cheapest backfill possible.** Right now: 34 rounds, 2 leagues. Every round and every new league widens the backfill scope. Within months this becomes a bigger and riskier piece of work.
- **Locks data in before rule changes.** The user has confirmed scoring/boost rules cannot change mid-season (rules are set at season start), but season-to-season changes are still possible. Snapshotting per round eliminates any future ambiguity.
- **Wanted by the round results email and the prize email (both now shipped):** both surface per-round rank deltas and league context that these snapshots would make cheaper and more reliable to compute.
- **Five other Tier 7 features get easier or possible:** stats dashboard, season recap, prediction history page, head-to-head, achievement badges.

## Design principle

**Capture base facts; derive everything else at query time.**

> **Correction (2026-07-29): most of this is re-derivable, so this plan is an optimisation, not a
> now-or-never data capture.** The claim below that rank "cannot be reliably re-derived" does not hold.
> `LeagueRoundResults` already stores per-round points per member per league, and those rows stop changing
> once a round is done, so the cumulative rank at the end of any past round is *exactly* re-derivable as
> `SUM(BoostedPoints) WHERE RoundNumber <= N` ranked. Same for monthly, stage and exact-scores standings.
> `UserBoostUsages.RoundId` and `Winnings.RoundNumber`/`.Month` already make boosts and prizes historical
> too.
>
> That does not make this plan worthless - the justifications that survive are **query cost** (rebuilding
> a season's worth of standings on demand gets expensive at scale) and **analytics convenience** (a clean
> single-table time series beats a five-way join for the stats dashboard, head-to-head and recap
> features). Both are real. But it does undercut the "Why now" section above: there is no data being lost
> while this is unbuilt, so the backfill-gets-more-expensive argument is about effort, not about
> recoverability.
>
> **The one genuinely irrecoverable gap is not in this plan at all: membership history.** `LeagueMembers`
> has `JoinedAtUtc` and `ApprovedAtUtc` but no leave/removal record and no status-change log, so "who was
> in this league at the end of round N" stops being answerable the day anyone can leave a league. Nothing
> can currently remove an *approved* member, so nothing is lost yet. See
> [historical-round-view](../../features/user-experience/historical-round-view/README.md) for the rule and
> the two possible shapes. If snapshot rows are written per member per round, they would incidentally
> record who was present - which is a further argument for this plan, but a fragile one, since it only
> captures members who were present at a *round close*.

A "base fact" is something true at that moment in time that is not cheaply re-derived from raw inputs because:
- Rank depends on cumulative state of all users (not just the user being measured).
- Cumulative points are computable but expensive at query time.
- Rule snapshots are insurance against future rule changes.

Derived stats (form, streaks, biggest-jump, average per round, accuracy %, "you topped the table for 3 weeks") are NOT captured - they're queries over the base facts. This keeps the schema narrow and means new analytics features need no schema change.

## What stays out of these tables

| Data | Already preserved by | Don't duplicate |
|---|---|---|
| Predictions | `Predictions` table | |
| Match results | match tables | |
| Round points (raw + boosted) | `LeagueRoundResults` | (We do duplicate this for query speed - see schema) |
| Boost type used | `BoostUsage` | (Same) |
| Prize won | `Winnings` + `PrizeNotifications` (shipped) | |

## Schema

Two new tables. Both keyed `(LeagueId, ..., RoundId)` so re-running the write path is idempotent (`MERGE` or check-then-insert).

### `[LeagueMemberRoundStats]` - per user, per league, per round

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `LeagueId` | int | NO | PK, FK |
| `UserId` | nvarchar(450) | NO | PK, FK |
| `RoundId` | int | NO | PK, FK |
| `OverallRankAfterRound` | int | NO | Cumulative-season rank within this league |
| `OverallPointsAfterRound` | decimal(10,2) | NO | Cumulative-season points |
| `MonthRankAfterRound` | int | **YES** | NULL for tournament rounds (no monthly concept) |
| `MonthPointsAfterRound` | decimal(10,2) | **YES** | NULL for tournament rounds |
| `RoundRank` | int | NO | Rank within this single round |
| `RoundPointsRaw` | decimal(10,2) | NO | Before boost |
| `RoundPointsBoosted` | decimal(10,2) | NO | After boost |
| `PredictionsSubmittedCount` | int | NO | How many of the round's matches were predicted (0 = round skipped) |
| `ExactScoresCount` | int | NO | |
| `CorrectResultsCount` | int | NO | Outcome correct, score wrong |
| `IncorrectCount` | int | NO | Predicted but wrong outcome |
| `BoostTypeUsed` | tinyint | YES | NULL = no boost used; matches `BoostType` enum |
| `BoostPointsGained` | decimal(10,2) | YES | Delta from raw to boosted |
| `TournamentStage` | tinyint | YES | NULL for league rounds; matches `TournamentStage` enum for tournament rounds. Denormalised from the round mapping so stats queries don't need the join. |
| `CreatedAtUtc` | datetime2 | NO | When this snapshot row was written |

### `[LeagueRoundSnapshot]` - per league, per round (league-wide context)

| Column | Type | Nullable | Notes |
|---|---|---|---|
| `LeagueId` | int | NO | PK, FK |
| `RoundId` | int | NO | PK, FK |
| `ParticipatingMemberCount` | int | NO | Members who submitted at least one prediction |
| `TopScorerUserId` | nvarchar(450) | YES | NULL only if no one predicted (theoretical edge case) |
| `TopScorerPoints` | decimal(10,2) | YES | |
| `RoundAveragePoints` | decimal(10,2) | NO | League-wide average for this round (only over participants) |
| `RoundMaxPoints` | decimal(10,2) | NO | |
| `LeaderUserId` | nvarchar(450) | NO | Overall leader at round close |
| `LeaderPoints` | decimal(10,2) | NO | |
| `ScoringRulesJson` | nvarchar(max) | YES | Serialised league scoring rules at round close. Cheap insurance against future season-to-season rule changes. |
| `BoostRulesJson` | nvarchar(max) | YES | Serialised boost rules at round close. |
| `TournamentStage` | tinyint | YES | NULL for league rounds. |
| `CreatedAtUtc` | datetime2 | NO | |

### Indexes

Both tables get the obvious composite PKs. Add covering indexes for likely query shapes:

- `LeagueMemberRoundStats`: `IX_LeagueMemberRoundStats_UserId_LeagueId` (so per-user time-series queries don't scan).
- `LeagueRoundSnapshot`: PK is enough.

## Acceptance Criteria

- [ ] Both tables exist with the schema above.
- [ ] When a round closes (in the existing `UpdateStableStatsAsync` flow), one row is INSERTed per participating member into `LeagueMemberRoundStats` and one row per league into `LeagueRoundSnapshot`.
- [ ] Tournament rounds have NULL in monthly fields; league rounds have NULL in `TournamentStage`.
- [ ] Re-running the round-close flow does not produce duplicate rows (idempotent).
- [ ] A backfill tool populates rows for all 34 existing rounds across both leagues. Counts of correct results, exact scores, etc. match what the live state currently shows for the latest round (sanity check).
- [ ] After backfill, every (league x round) combination has a `LeagueRoundSnapshot` row, and every (league x user x round) combination where the user was a member of the league at that time has a `LeagueMemberRoundStats` row.
- [ ] `database-schema.md` updated.
- [ ] `DatabaseRefresher.cs` includes both tables in `TableCopyOrder` (after `Rounds`, `Leagues`, `LeagueRoundResults`).
- [ ] `DataAnonymiser.cs` does not need changes (no PII in either table).
- [ ] Unit tests for the snapshot-writing logic (correct round filtering, idempotency, tournament-vs-league branching).
- [ ] Domain coverage stays at 100% line / 100% branch.

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | [Schema and domain models](./01-schema.md) | Create both tables, write migration SQL, add Domain entities, update `database-schema.md` and `DatabaseRefresher.cs`. | Not Started |
| 2 | [Write path](./02-write-path.md) | Hook into `LeagueStatsService.UpdateStableStatsAsync` so snapshots are persisted at round close. Idempotent. Tournament-aware (NULL monthly fields). | Not Started |
| 3 | [Backfill tool](./03-backfill-tool.md) | One-shot tool under `tools/ThePredictions.DatabaseTools/` that walks all completed rounds in chronological order and writes snapshots using current scoring/boost rules. Test against dev DB first. | Not Started |
| 4 | [Tests](./04-tests.md) | Unit tests for the write path (correct counts, idempotency, tournament branching) and at least one integration test for the backfill tool against a known fixture. | Not Started |

## Dependencies

- [ ] None blocking. This is a foundation for other work, not built on top of anything.
- [x] DbUp migrations have now shipped (ADR-0013), so this plan's schema changes should be written as a **new numbered migration** under `src/ThePredictions.Persistence.SqlServer/Migrations/` rather than a manual schema change.

## Technical Notes

### Why duplicate `RoundPointsBoosted` when `LeagueRoundResults` already has `BoostedPoints`

To make per-user time-series queries a single-table scan rather than a join. At 9,500 rows per season the duplication is microscopic; at any scale, the read pattern dominates and denormalisation pays for itself.

### Why duplicate `TournamentStage` from `TournamentRoundMapping`

Same reason. Stats queries that want "performance during knockout stages" should not have to four-way join through `Rounds`, `TournamentRoundMappings`, etc.

### Tournament-specific considerations

- **No monthly leaderboard.** Tournaments are short (~1 month) and the monthly view is meaningless. Both monthly columns are nullable and left NULL for tournament rounds.
- **TournamentStage column is denormalised** from the round's `TournamentRoundMapping` so future queries like "how do users do at knockout vs group stages?" don't need joins.
- **Group rounds vs knockout rounds.** Both are normal rounds in the snapshot. The stage value is what differentiates them.
- **No new fields specific to knockout match outcomes** (e.g. "how many advancements predicted correctly") - those are derivable per-prediction from the existing `Predictions` table joined to match results.

### Scoring/boost rules JSON

Set during `WriteRoundSnapshotsAsync` to a serialised view of the league's current rules. Since rules can't change mid-season, this is the same value for every round in a season - mildly redundant but cheap (a few KB across the lifetime of a season) and the safest hedge against future rule evolution. If we later confirm this redundancy isn't needed, dropping the columns is a one-line schema change.

### Backfill ordering

Walk rounds in `(SeasonStartDate, RoundNumber)` order. The backfill must process rounds in the same chronological order they actually occurred - cumulative ranks depend on prior rounds being processed first. Confirm league membership at each round (a user who joined mid-season shouldn't appear in earlier rounds).

### Performance

Round close already runs SQL across `LeagueMemberStats`. Adding two INSERTs (one per league member + one per league) is negligible. Backfill of ~9,500 rows × 2 tables is also fast - well under a minute.

## Open Questions

- [ ] Confirm with codebase exactly how `LeagueRoundResults.BoostedPoints` relates to raw points - is there a `Points` (raw) column too, or is raw computable as `BoostedPoints / boostMultiplier`? Schema task should resolve this and adjust `RoundPointsRaw` source accordingly.
- [ ] Should `BoostType` enum be denormalised by name as a string, or stored as the enum int? Int is smaller and faster; consumers can look up the name. Recommend int.
- [ ] Backfill: write to dev DB only, then snapshot the dev DB and copy that across to prod? Or run the tool live against prod with a transaction? Recommend dev-first, snapshot, restore - matches existing `DatabaseRefresher` patterns.
