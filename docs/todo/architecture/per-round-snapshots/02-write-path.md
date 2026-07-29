# Task 2: Write Path

**Parent Feature:** [Per-Round Historical Snapshots](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

When a round closes, write one `LeagueMemberRoundStats` row per participating member per league, and one `LeagueRoundSnapshot` row per league. Idempotent. Tournament-aware. No regression to existing `LeagueMemberStats` overwriting behaviour - that stays as-is for current-state queries.

## Where it hooks in

> **Stale as written.** [ADR-0015](../../../decisions/0015-cache-my-leagues-ranks.md) removed
> `LeagueStatsService` and the round-scoped `UpdateStableStatsAsync` / `SnapshotRanksForRoundStartAsync`
> methods this section described. `ILeagueStatsRepository` now exposes only `RefreshLeagueAsync` and
> `RefreshSeasonAsync`, and `LeagueMemberStats` holds no point-in-time snapshots at all - every value is
> recomputed from current results. The hook point below needs rewriting before this plan is picked up.

Round completion is detected in [`UpdateMatchResultsCommandHandler`](../../../src/ThePredictions.Application/Features/Admin/Rounds/Commands/UpdateMatchResultsCommandHandler.cs), which flips the round to `Completed` and then refreshes the season's cached ranks. A `WriteRoundSnapshotsAsync(roundId)` on `LeagueStatsRepository` would be called from that same block, responsible for both INSERTs (member-level + league-level) inside one transaction.

Note that the historical snapshots this plan proposes are a genuinely different thing from the cached ranks: they are an immutable record of how a specific round finished, whereas `LeagueMemberStats` is a derived view of the present that is rebuilt on every refresh. Keep them separate.

## SQL shape

Member-level INSERT can be set-based:

```sql
INSERT INTO [LeagueMemberRoundStats]
(
    [LeagueId], [UserId], [RoundId],
    [OverallRankAfterRound], [OverallPointsAfterRound],
    [MonthRankAfterRound], [MonthPointsAfterRound],
    [RoundRank], [RoundPointsRaw], [RoundPointsBoosted],
    [PredictionsSubmittedCount], [ExactScoresCount], [CorrectResultsCount], [IncorrectCount],
    [BoostTypeUsed], [BoostPointsGained],
    [TournamentStage], [CreatedAtUtc]
)
SELECT
    lms.[LeagueId],
    lms.[UserId],
    @RoundId,
    lms.[OverallRank],
    -- compute cumulative points by summing LeagueRoundResults up to and including this round
    (SELECT ISNULL(SUM(lrr2.[BoostedPoints]), 0)
     FROM [LeagueRoundResults] lrr2
     WHERE lrr2.[LeagueId] = lms.[LeagueId]
       AND lrr2.[UserId] = lms.[UserId]
       AND lrr2.[RoundId] <= @RoundId),
    -- monthly fields NULL for tournament seasons
    CASE WHEN s.[CompetitionType] = 0 THEN lms.[MonthRank] ELSE NULL END,
    CASE WHEN s.[CompetitionType] = 0 THEN /* monthly cumulative */ ELSE NULL END,
    lms.[StableRoundRank],
    lrr.[RawPoints],     -- or compute from BoostedPoints / multiplier - confirm during task
    lrr.[BoostedPoints],
    -- prediction outcome counts via JOIN to Predictions + match results
    /* PredictionsSubmittedCount    */ ...,
    /* ExactScoresCount             */ ...,
    /* CorrectResultsCount          */ ...,
    /* IncorrectCount               */ ...,
    bu.[BoostType],
    lrr.[BoostedPoints] - lrr.[RawPoints],
    /* TournamentStage from TournamentRoundMappings join, NULL for league competitions */ ...,
    SYSUTCDATETIME()
FROM [LeagueMemberStats] lms
JOIN [Leagues] l   ON lms.[LeagueId] = l.[Id]
JOIN [Seasons] s   ON l.[SeasonId] = s.[Id]
JOIN [Rounds] r    ON r.[Id] = @RoundId
LEFT JOIN [LeagueRoundResults] lrr
    ON lrr.[LeagueId] = lms.[LeagueId]
   AND lrr.[UserId]   = lms.[UserId]
   AND lrr.[RoundId]  = @RoundId
LEFT JOIN [BoostUsage] bu
    ON bu.[LeagueId] = lms.[LeagueId]
   AND bu.[UserId]   = lms.[UserId]
   AND bu.[RoundId]  = @RoundId
LEFT JOIN [TournamentRoundMappings] trm
    ON trm.[SeasonId]    = s.[Id]
   AND trm.[RoundNumber] = r.[RoundNumber]
WHERE l.[SeasonId] = r.[SeasonId]
  -- idempotency
  AND NOT EXISTS (
      SELECT 1 FROM [LeagueMemberRoundStats] lmrs
      WHERE lmrs.[LeagueId] = lms.[LeagueId]
        AND lmrs.[UserId]   = lms.[UserId]
        AND lmrs.[RoundId]  = @RoundId
  );
```

The exact column-derivation expressions need confirming against the codebase during implementation - especially the prediction-outcome counts (probably best computed via a CTE that joins `Predictions` to match results). The shape above is illustrative.

The league-level INSERT is similar but aggregates across members (top scorer, average, etc.). Wrap both in a transaction.

## Idempotency

Two layers:

1. **`NOT EXISTS` guard in the INSERT** - skips rows already present.
2. **Composite PK** - hard prevents duplicates even if the guard is bypassed.

This means re-running round close (e.g. after a deploy mid-flow) is safe.

## Tournament awareness

The CASE expressions on monthly columns drive off `Seasons.CompetitionType`. League seasons (CompetitionType = 0) populate monthly fields normally; tournament seasons (= 1) get NULL.

`TournamentStage` is populated from the `TournamentRoundMappings` join, which is LEFT JOIN so league rounds get NULL.

## Tests

Unit-test surface is limited because most of the logic is SQL. Integration tests are the right level.

- `WriteRoundSnapshots_ShouldCreateOneRowPerMember_WhenRoundCloses`
- `WriteRoundSnapshots_ShouldSkipDuplicates_WhenCalledTwice`
- `WriteRoundSnapshots_ShouldNullMonthlyFields_WhenTournamentSeason`
- `WriteRoundSnapshots_ShouldPopulateTournamentStage_WhenTournamentRound`
- `WriteRoundSnapshots_ShouldLeaveTournamentStageNull_WhenLeagueRound`
- `WriteRoundSnapshots_ShouldComputeCumulativePointsCorrectly_WhenMidSeason`

These can run against the existing test database or an in-memory SQLite if appropriate; check what pattern other repository tests use.

Service-level wiring: `UpdateStableStatsAsync_ShouldCallWriteRoundSnapshotsAsync_AfterStableUpdate`.

## Verification

- [ ] Closing a round in dev results in N new `LeagueMemberRoundStats` rows (one per member) and 1 `LeagueRoundSnapshot` row per league.
- [ ] Re-running the close produces zero new rows and zero exceptions.
- [ ] Tournament rounds have NULL `MonthRank*` fields and a non-null `TournamentStage`.
- [ ] League rounds have populated `MonthRank*` fields and NULL `TournamentStage`.
- [ ] Domain coverage stays at 100%.
- [ ] All existing tests still pass.
