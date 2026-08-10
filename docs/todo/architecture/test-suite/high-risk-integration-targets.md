# High-Risk Integration Targets

Companion to [`README.md`](README.md), which sets out the overall testing strategy. This file names the
specific code the integration tier should cover **first**, identified during the August 2026 coverage
audit. Every item here is currently untested and cannot be unit tested, for reasons given per item.

## Why a separate list

The unit suite reached 100% line and branch on all eight projects, which makes it easy to assume the
risky code is covered. It is not. Three categories are structurally invisible to unit tests:

1. **Rules expressed in SQL.** Handler unit tests mock `IApplicationReadDbConnection`, so no SQL runs.
   `tools/ThePredictions.SchemaCheck` proves a result set *can materialise*; it says nothing about
   whether the query returns the right rows.
2. **Aggregate diffing in repositories.** Excluded as "thin Dapper wrappers", which some of them are
   not.
3. **Predicates duplicated across two call sites** with only a comment holding them together.

## 1. The delete guard in RoundRepository (highest risk)

`src/ThePredictions.Infrastructure/Repositories/RoundRepository.cs`

`UpdateAsync` computes which matches to insert, update and delete by diffing incoming matches against
`existingMatchIds`. The delete is protected by a `NOT EXISTS` against `UserPredictions`:

```sql
AND NOT EXISTS (SELECT 1 FROM [UserPredictions] up WHERE up.[MatchId] = m.[Id])
```

**This is a data-loss guard and it has no test.** If it regressed, editing a round would delete matches
that players had already predicted, taking their predictions with it. There is also an ordering
dependency: `MoveMatchesToRoundAsync` must run before `UpdateAsync`, or a moved match is treated as
removed and deleted.

Tests wanted:

- [x] Attempting to remove a match that has predictions leaves it in place
- [x] Removing a match with no predictions succeeds
- [x] A match moved to another round survives the update rather than being deleted
- [x] Insert, update and delete in one call all apply correctly together

**Done (August 2026)** in
`tests/Integration/ThePredictions.Persistence.SqlServer.Tests.Integration/Repositories/RoundRepositoryUpdateTests.cs`,
along with two the list did not name. `FK_UserPredictions_Matches` turned out to be `ON DELETE CASCADE`,
so an unguarded delete removes the predictions with no error at all - that premise now has its own test,
so the guard's importance is demonstrated rather than asserted in a comment. The wrong call order is
pinned too: running `UpdateAsync` before `MoveMatchesToRoundAsync` deletes the match that was about to
move, and a test now says so where a caller will see it.

## 2. The boost-usage secrecy predicate

`GetLeagueBoostUsageSummaryQueryHandler.GetUsagesAsync`

```sql
AND (
    ubu.[UserId] = @CurrentUserId
    OR r.[DeadlineUtc] <= GETUTCDATE()
)
```

Another player's boost is hidden until that round's deadline passes. Get this wrong and the page reveals
what opponents have played while they can still change it - the same class of fairness rule as the
dashboard's prediction split, which **is** unit tested because it happens in C#.

The shaping above this predicate was extracted to `BoostUsageSummaryBuilder` and is unit tested. The
predicate itself is only reachable through a real database.

Tests wanted:

- [x] Own boosts are visible in a round whose deadline has not passed
- [x] Another player's boosts are hidden in that same round
- [x] Another player's boosts appear once the deadline has passed

Note it calls `GETUTCDATE()` rather than taking a clock, so tests must arrange round deadlines relative
to now rather than pinning a fixed instant.

**Done (August 2026)** in `tests/Integration/.../Queries/BoostUsageSecrecyTests.cs`, through `Handle`
rather than the private read, so what is asserted is what a player would see. Two more went in alongside:
the same world viewed by the *other* player, which rules out a predicate that is right for one user only,
and the remaining-uses figure, which is computed from censored rows and so leaks the hidden usage if the
predicate is wrong. Verified by mutation - replacing the deadline comparison with `1 = 1` fails three of
the five and nothing else.

## 3. PredictableMatchPredicate, duplicated by comment

`GetRoundCompletionQueryHandler.PredictableMatchPredicate` carries this warning:

> Kept in lockstep with the identical predicate in `ReminderService.GetUsersMissingPredictionsAsync` -
> change both together.

**Nothing enforces that.** Two copies of a predicate held together by a comment will diverge, and the
symptom would be a player chased for predictions they cannot make, or not chased for ones they can.

The predicate encodes real rules: teams confirmed, status still `Scheduled`, and
`COALESCE(m.CustomLockTimeUtc, @RoundDeadlineUtc) > @NowUtc` so a per-match lock overrides the round
deadline.

Tests wanted:

- [x] Both call sites return the same match set for the same data - ideally one test feeding both
- [x] A match with a custom lock in the future counts as predictable after the round deadline has passed
- [x] A match with no custom lock stops counting once the round deadline passes
- [x] Matches without confirmed teams, or not `Scheduled`, are excluded

If the two can be made to share one SQL constant, that is better than testing them separately - but the
test should still exist, because sharing a constant is itself something that can be undone.

**Done (August 2026)** in `tests/Integration/.../Queries/PredictableMatchPredicateTests.cs`, as the single
theory this asked for: eight scenarios, each driving both call sites over one seeded fixture and asserting
they agree. Verified by mutation - diverging the handler's copy alone fails exactly the two custom-lock
cases, named in the failure output.

**The shared constant was not done, deliberately.** The two fragments are not textually identical: the
handler takes the deadline as `@RoundDeadlineUtc` and filters on `@RoundId`, while `ReminderService` reads
`r.[DeadlineUtc]` and joins on `r.[Id]`. Sharing means reconciling that first, which is a production
change to the reminder job's SQL and wants its own reviewed PR. The test does not depend on it either way,
which is the point this list already made.

## 4. The rest, in rough priority order

- [ ] The ~55 query handlers whose exclusion is legitimately "SQL plus a mapping" - the value is in the
      `WHERE` clauses, especially any encoding authorisation or visibility
- [ ] `GetLeaguePayoutsQueryHandler`'s administrator check, which sits in front of decrypted bank details
      (the C# guard is unit tested; the SQL that feeds it is not)
- [ ] The 30 repositories, `LeagueRepository` next after `RoundRepository` for the same diffing reason
- [ ] The two ASP.NET Identity stores over Dapper
- [ ] `TransactionBehaviour` rollback under a real transaction
- [x] DbUp migrations applying cleanly from empty, which the container strategy gives almost for free

## Reminder on the harness

Use **Testcontainers with real SQL Server, not SQLite** - already the decision in `README.md`. SQLite
cannot evaluate the `RANK() OVER`, `CROSS APPLY`, `MERGE` or `CAST(... AS bit)` this codebase relies on,
so a SQLite suite would pass while proving nothing about production behaviour.

**Built (August 2026):** `tests/Integration/ThePredictions.Persistence.SqlServer.Tests.Integration`. One
container per run, schema built by running the committed DbUp migrations, Respawn between tests. How to
run it and the conventions it expects are in
[`../../../guides/testing.md`](../../../guides/testing.md#integration-tests-against-real-sql-server). The
migrations applying from empty came free with the strategy, exactly as predicted, and is asserted rather
than assumed - the three real databases were baselined from an existing schema, so `0001_Baseline.sql`
had never run anywhere as anything but a no-op.
