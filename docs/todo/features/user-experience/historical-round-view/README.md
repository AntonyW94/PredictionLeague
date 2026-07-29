# Feature: Historical Round View on the League Dashboard

## Status

**Not Started** | In Progress | Complete

## Summary

Selecting a previous round on the league dashboard currently changes only the round-results grid. This
makes the *whole* dashboard show the league as it stood at the end of that round: overall, monthly, stage
and exact-scores standings, boosts used, and prizes awarded up to that point.

Almost none of this needs new tables. `LeagueRoundResults` already stores per-round points per member per
league, and those rows stop changing once a round is done, so "the standings at the end of round N" is
`SUM(BoostedPoints) WHERE RoundNumber <= N`. **This is a query change, not a schema change.**

## User Story

As a league member, I want to select a past round and see the whole dashboard as it looked then, so that I
can follow how the league developed and revisit where I stood at any point in the season.

## What each tile needs

`LeagueDashboardDto` already exposes `ViewableRounds`, and the client already tracks a selected round
(`State.IsSelectedRoundLive`), so the picker exists. What is missing is threading the selected round into
the tiles that currently only ever show "now".

| Tile | As at end of round N | Schema change |
|------|----------------------|---------------|
| Round results grid / `PredictionGrid` | Already takes a `RoundId`. `GetLeagueDashboardRoundResultsQueryHandler` reconstructs predictions, outcomes, boosts and round rank from raw data | **None, works today** |
| `OverallLeaderboardTile` | `SUM(BoostedPoints)` over rounds with `RoundNumber <= N` | None |
| `MonthlyLeaderboardTile` | Same, restricted to that month, rounds `<= N` | None |
| `StageLeaderboardTile` | Same, restricted to that stage, rounds `<= N` | None |
| `ExactScoresLeaderboardTile` | `SUM(RoundResults.ExactScoreCount)` over rounds `<= N` | None |
| `BoostUsageTile` | `UserBoostUsages` already carries `RoundId` | None |
| `WinningsLeaderboardTile`, prize tiles | `Winnings` already carries `RoundNumber` and `Month` | None |
| `LeagueRecordsTile` | Existing record queries plus a round ceiling | None |
| Member count, prize pot | Current-only and not derivable - show current | None |

## The one thing that is not derivable: membership history

> **Rule: add membership history before adding any way to leave or be removed from a league.**

`LeagueMembers` records `JoinedAtUtc` and `ApprovedAtUtc`, but there is **no leave/removal record and no
status-change history**. So "who was an approved member of this league at the end of round 12" is only
answerable for as long as nobody has ever left.

Nothing is being lost today, because nothing in the app can take an *approved* member out of a league:

- `LeagueMember.Approve` and `LeagueMember.Reject` both require the member to be `Pending`.
- `CancelLeagueRequestCommandHandler` only deletes `Pending` rows.
- `RemoveRejectedLeagueCommandHandler` only removes `Rejected` ones.
- Deleting the league removes everything anyway.

The moment a leave-league or remove-member feature ships *without* membership history, every historical
leaderboard silently drops those people from that point **backwards**, and it cannot be reconstructed.
This is the only part of this feature where waiting has a cost, and that cost is unrecoverable - so the
rule belongs to whoever builds the leave feature, not to whoever builds this one.

Either shape works:

- `LeftAtUtc` on `LeagueMembers`, plus soft-delete instead of row deletion, or
- a `LeagueMemberHistory` table logging every status transition with a timestamp.

The second is more work but also answers "when was this person approved / rejected / readmitted", which
the first cannot.

The prize pot is likewise not reconstructible (`Price × current member count` plus an editable
`PrizeFundOverride`), but showing the current pot on a historical view is the sensible behaviour anyway,
so it is not worth chasing.

## Technical Notes

### One handler per leaderboard, not two

An "as at round N" leaderboard **cannot** read `LeagueMemberStats`. That cache holds only the current
standings, by design ([ADR-0015](../../../decisions/0015-cache-my-leagues-ranks.md)), so a historical view
has to compute from `LeagueRoundResults`.

That gives each leaderboard two paths. Keep them in **one** handler taking a nullable round ceiling, where
`NULL` means "now, read the cache":

```csharp
public record GetOverallLeaderboardQuery(int LeagueId, string CurrentUserId, int? AsAtRoundNumber = null);
```

Two separate handlers will drift apart, and the failure mode is the worst kind: the historical view and the
live view disagreeing about the same league. Prefer branching inside one SQL statement with an
`AND (@AsAtRoundNumber IS NULL OR r.[RoundNumber] <= @AsAtRoundNumber)` predicate, so the ranking
expression itself is written once.

### The change arrows need their baseline shifted too

Every arrow's baseline is defined relative to the *active* round (ADR-0015). On a historical view the
selected round becomes the reference point, so the baselines move with it: the overall arrow compares
rounds `< N` against rounds `<= N`, the month arrow compares the month's rounds excluding N against
including N, and so on. Same expressions, different ceiling.

The round arrow is the exception. It compares finished-match standings against all-match standings, which
differ only while a round is in play. A completed historical round has nothing in progress, so there is no
arrow - suppress it rather than computing a zero.

### Round selection must not resurrect hidden predictions

`GetLeagueDashboardRoundResultsQueryHandler` hides other members' predictions until the deadline passes
(`COALESCE(m.[CustomLockTimeUtc], r.[DeadlineUtc]) > GETUTCDATE()`). That compares against *now*, not
against the selected round, which is correct and must stay that way. A past round is safe to reveal, but if
the guard is ever re-expressed in terms of the selected round, selecting a future round would leak live
predictions.

### Do not start this until the ranks cache has landed and been verified

This touches the same leaderboard query handlers that ADR-0015 changed. Landing both at once makes it much
harder to attribute a wrong rank or a missing arrow to the right change.

## Acceptance Criteria

- [ ] Selecting a past round updates every tile, not just the results grid.
- [ ] Selecting the current round is byte-identical to today's behaviour, and still reads the cache.
- [ ] Standings for a past round match what that round's digest email reported at the time.
- [ ] Change arrows on a historical round use that round as the reference point; the round arrow is
      suppressed.
- [ ] Other members' predictions for a round whose deadline has not passed stay hidden regardless of which
      round is selected.
- [ ] No migration required. If one turns out to be needed, that is a signal the approach has drifted.

## Tasks

| # | Task | Description | Status |
|---|------|-------------|--------|
| 1 | Thread the round ceiling through the leaderboard queries | Add the nullable `AsAtRoundNumber` to the overall, monthly, stage and exact-scores queries and their handlers, branching in SQL rather than in C#. | Not Started |
| 2 | Shift the arrow baselines | Make each pre-round baseline relative to the selected round; suppress the round arrow on a completed round. | Not Started |
| 3 | Round-scope the remaining tiles | Boost usage, winnings/prizes and league records. | Not Started |
| 4 | Client wiring | Pass the selected round from dashboard state into every tile; decide the loading/empty behaviour when switching rounds. | Not Started |
| 5 | Tests | Query handler tests per tile asserting a past round's standings, plus one asserting the current round still matches the cached path. | Not Started |

## Dependencies

- [ ] [ADR-0015](../../../decisions/0015-cache-my-leagues-ranks.md) cache work merged and verified.
- [ ] [Pagination](../../architecture/pagination/README.md) if leagues are large by then. A historical
      leaderboard returns exactly as many rows as a live one.
- [ ] Membership history, **only if** a leave-league feature exists or is planned by then (see above).

## Open Questions

- [ ] Should a historical view show the prize pot as it was (not derivable) or as it is now
      (recommended)?
- [ ] Should the season-recap and statistics tiles respect the selected round, or always cover the whole
      season?
- [ ] Is the selected round reflected in the URL, so a member can share "here is where we were after
      round 12"?
