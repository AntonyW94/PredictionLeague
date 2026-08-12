# Persistence Split

## Status

Not Started | **In Progress** | Complete

Agreed 2026-08-10. Moves every line of SQL out of `ThePredictions.Application` and
`ThePredictions.Infrastructure` into a dedicated adapter, `ThePredictions.Persistence.SqlServer`,
behind one interface per query owned by Application. Undertaken during a month of site downtime, so
each phase can land without a deploy waiting on it.

## Why, in one paragraph

Not portability. The interface boundary is already correct and machine-enforced - Application owns
`IApplicationReadDbConnection` and the `IXxxRepository` set, and
`LayerDependencyConventionTests` fails the build on a reference the wrong way. What leaks is the
**SQL text**: 61 `ISNULL(`, 21 `SELECT TOP`, 19 `CAST(... AS bit)`, 17 `OUTER APPLY`, 16
`GETUTCDATE()` and 16 `RANK() OVER` live in the layer that is supposed to hold business rules. The
architecture looks persistence-ignorant and is in fact persistence-coupled. Drawing this boundary
forces every predicate to be classified as a rule or a fetch, and every rule moved to C# is one
fewer thing a future adapter can get wrong.

The measurable prize: ~55 query handlers currently carry
`[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping...")]`.
Once the SQL leaves, the handler holds authorisation, orchestration and shaping only. It stops being
excludable and becomes measured, which is what the August 2026 audit was really complaining about.

## The decision procedure

Every statement gets one question:

> **Could someone who knows nothing about the business rules port this to another dialect correctly?**

| Answer | Meaning | Where it goes |
|--------|---------|---------------|
| Yes | Mechanism | Stays in the adapter |
| No | It encodes a rule | The rule moves to C#; the adapter keeps only the fetch |

Worked examples:

| Statement | Verdict |
|-----------|---------|
| `DELETE FROM [PasswordResetTokens] WHERE [CreatedAtUtc] < @CutoffDate` | Mechanism. The 30-day retention is already a C# const. |
| `SELECT TOP 20 ... ORDER BY [Name]` (paging) | Mechanism. |
| `SUM(CASE WHEN up.[Outcome] = @ExactScore THEN 1 ELSE 0 END)` | Rule - you have to know what an exact score is. |
| `ubu.[UserId] = @CurrentUserId OR r.[DeadlineUtc] <= GETUTCDATE()` | Rule - boost secrecy. |
| `COALESCE(m.[CustomLockTimeUtc], @RoundDeadlineUtc) > @NowUtc` | Rule - when a fixture locks. |
| `RANK() OVER (ORDER BY [Points] DESC)` | Rule - tie handling is a product decision. |

**Row counts make "every rule in C#" affordable.** Measured on dev (an anonymised copy of prod,
2026-08-10): largest round 648 predictions over 24 matches, largest league 22 members, 9,581
predictions in total, 44 users. Nothing here is a big-data problem. The `RoundResults` recompute runs
once per round in a background task, not on a request path.

## Target layout

```
src/
├── ThePredictions.Domain                  → unchanged
├── ThePredictions.Application             → NO SQL. Handlers, rules, and one interface per query.
├── ThePredictions.Persistence.SqlServer   → NEW. Every line of SQL, plus the DbUp migration set.
├── ThePredictions.Infrastructure          → external world only: Brevo, Stripe, football API,
│                                            SkiaSharp, JWT, formatters
├── ThePredictions.API / Web               → composition root; references both adapters
└── ...
```

Cut rule: **if it speaks SQL it moves; if it speaks HTTP, SMTP or Skia it stays.**

| Moves to `Persistence.SqlServer` | Stays in `Infrastructure` |
|---|---|
| All 30 repositories | Brevo / Stripe / football API clients |
| `DapperReadDbConnection`, `SqlConnectionFactory`, `DbTransactionContext`, `DapperUtcDateTimeHandler`, `SqlRetryPolicy`, `SqlTransientFaultDetector` | `ShareCardRenderer`, `UkEmailDateFormatter` |
| `DapperUserStore`, `DapperRoleStore` | `AuthenticationTokenService`, `DateTimeProvider` |
| The SQL inside ~55 query handlers | `ReminderService.ShouldSendReminderAsync` (milestone logic) |
| `LeagueMembershipService`, `CachedEmailSettingsProvider`, `BadgeStateQueries` SQL | `CanonicalEmailLookupNormalizer` |
| The DbUp migration set (the schema belongs to the adapter) | |

New convention rules to add: `Infrastructure` must not reference `Persistence.SqlServer`;
`Application` must not reference either; **no `.cs` file under `src/ThePredictions.Application` may
contain SQL keywords** (the machine-enforced version of this whole plan, and the thing that stops it
rotting).

### Why `Persistence.SqlServer` and not `TSql`

T-SQL is the language; the coupling is to the engine. Collation
(`SQL_Latin1_General_CP1_CI_AS`, load-bearing per ADR-0013), `READ_COMMITTED_SNAPSHOT`, `money`
rounding, `datetime2(7)` precision and `MERGE` concurrency semantics do not transfer between engines
that share the language. Naming it after the dialect would encode the belief that the swap axis is
syntax, which is the misconception this plan exists to design away from.

## Interface convention

**One interface per query** (decided 2026-08-10; the alternative was ~15 per-feature-area
interfaces, rejected for granularity even though grouping would have made the compiler the
completeness gate).

```
src/ThePredictions.Application/Features/Boosts/Queries/ILeagueBoostUsageQuery.cs
src/ThePredictions.Persistence.SqlServer/Features/Boosts/LeagueBoostUsageQuery.cs
```

- Interface name: `I<Thing>Query`, one method, `ExecuteAsync`.
- Row types live in **Application**, beside the interface, because they are part of the port's
  contract. This puts the positional `SELECT`-to-record coupling across an assembly boundary, which
  the pre-integration-suite codebase could not have afforded. It can now: `SchemaCheck` and the
  conformance suite both catch a mismatch, and the conformance suite runs on every push.
- Completeness is enforced by a convention test (every `I*Query` in Application has exactly one
  implementation per adapter), since ~55 single-method interfaces give the compiler nothing to
  enforce.

## Conformance suite

The answer to "would the tests still pass against another database". Written once, run per adapter.

```
tests/Integration/
├── ThePredictions.Persistence.Conformance          → abstract test bases. Behavioural tests against
│                                                      the interfaces only. No SQL, no dialect.
├── ThePredictions.Persistence.SqlServer.Tests      → derives from it; SQL Server container +
│                                                      the SqlServer migration set
└── (later) ThePredictions.Persistence.MySql.Tests  → derives from the same bases; MySQL container +
                                                       its own migration set
```

The schema is part of the adapter, so **seeding cannot stay dialect-free.** Today's `TestDataSeeder`
writes raw `INSERT`s on purpose, so a test never arranges through the code it asserts on. That
principle is kept by putting the seeder behind `ITestDataSeeder` with one implementation per adapter,
rather than arranging through the adapter's own write path.

**Mongo is not a swap.** 39 tables with a dense foreign-key graph, `RANK()` leaderboards, `MERGE`
upserts and cross-entity transactions do not become documents by rewriting queries - they become a
different set of aggregate boundaries. The conformance suite would correctly report what broke, but
"everything still works" would not hold. MySQL or Postgres is the realistic second adapter; design
for that and Mongo stays possible without being paid for now.

## Phases

Each phase is one PR, master stays green and deployable throughout.

| # | Phase | Contents |
|---|-------|----------|
| 0a | **Scaffold + plumbing** ✅ | Create `Persistence.SqlServer` and move the connection, transaction, read, retry and type-handler seams. `AddSqlServerPersistence()`. New gated test project. Two new layer-convention rules. |
| 0b | **Repositories** ✅ | Move all 30 repositories and `RepositoryBase`, and their 29 registrations. `Microsoft.Data.SqlClient` leaves Infrastructure here rather than in 0c, because `BoostWriteRepository` was its only remaining user. |
| 0c | **Identity stores** ✅ | `DapperUserStore` and `DapperRoleStore` only, and `Dapper` leaves Infrastructure. `LeagueMembershipService` and `CachedEmailSettingsProvider` were dropped from this phase - see below. |
| 0d | **Migration set** ✅ | Move the DbUp scripts under the adapter, which now owns the embedded resources; `DatabaseTools` and the integration suite both read them from there. Required a journal rename on every database - see below. **Phase 0 complete.** |
| 1 | **Conformance split** ✅ | Renamed the integration project to `ThePredictions.Persistence.SqlServer.Tests.Integration` (Infrastructure holds no SQL, so the old name was a lie), then extracted `ThePredictions.Persistence.Conformance` with `ITestDataSeeder`, `ITestDataInspector` and the `RoundRepositoryConformanceTests` base. No new tests. |
| 2 | **Boosts/catalogue** ✅ | `IBoostCatalogueQuery` extracted, ordering moved to C#, handler measured for the first time. Pattern established. |
| 2 | **Boosts/usage-summary** ✅ | `ILeagueBoostUsageQuery` (one composite reply, 7 reads), three rules moved to C#, handler measured. |
| 2 | **Rounds/completion** ✅ | `IRoundCompletionQuery`; three SQL statements and a shared predicate replaced by `Match.IsOpenForPrediction`. SQL copy 1 of 2 gone. |
| 2 | **Rounds/reminders** ✅ | `ReminderService` onto the same port and the same domain rule. Predictable-fixture duplication **fully collapsed**; three rules, one definition each. |
| 2 | **Leaderboards/overall** ✅ | First adopter of `Ranking`. Five rules out of one query; `SnapshotOverallRank` stays a cached read per ADR-0015. |
| 2 | **Leaderboards/monthly** ✅ | Second adopter. `LeaderboardMemberRow` generalised and now shared; the month's pre-round rule kept separate from the overall table's on purpose. |
| 2 | **Leaderboards/exact-scores** ✅ | Third adopter, and the first with no rank-change arrow: four rules, no snapshot condition. Season-scoped counts pinned by conformance test. |
| 2 | **Leaderboards/stage** ✅ | Richest yet: seven rules from one statement, two ranks, and the stage classification. Pre-round position is computed here, not cached. |
| 2 | **Leaderboards/round grid** ✅ | The prediction secrecy rule, and the dense grid the old `CROSS JOIN` manufactured. First rule moved that is about fairness rather than arithmetic. |
| 2 | **Dashboard/leaderboards tile** ✅ | Seven rules from one windowed CTE. Found a rule already stated twice inside the handler - a SQL `ORDER BY` re-sorted by an identical LINQ chain. |
| 2 | **Leagues/records tile** ✅ | The largest statement in the application: ten `OUTER APPLY` blocks choosing ten record holders, four of them with no tie-break at all. |
| 2 | **Leagues/season recap** ✅ | Four ranks in two statements, one of them a running total recomputed round by round across the league. Collapsed the wins rule shared with the records tile. |
| 2 | **Dashboard/My Leagues** ✅ | The largest read on the site and the one with ADR-0015 behind it. Twelve rules out; every rank still a keyed cache lookup. Leaderboards are done. |
| 2 | **Leagues/dashboard + membership guard** ✅ | Completed the `SeasonCompletion` collapse, and moved the guard eighteen handlers depend on out of Infrastructure - the last SQL there bar one file. |
| 2 | **Leagues/pickers + email settings** ✅ | Two sibling statements collapsed into one read. **Infrastructure now contains no SQL at all.** |
| 2 | **Leagues/detail + rounds** ✅ | A third pair of statements collapsed into one read, and three sentinel values pulled out of SQL into named C#. |
| 2 | **Leagues/members + payment details** ✅ | The rule guarding a league's bank account had no tests, because the handler carried the boilerplate coverage exclusion. |
| 2 | **Leagues/prizes + create page** ✅ | A flattened left join split in two, which retired a row type where every prize column was nullable. |
| 2 | **Leagues/payouts** ✅ | Two definitions of "season finished" found side by side, and tests that no longer count the handler's SQL statements. |
| 2 | **Leagues/winnings** ✅ | **Leagues is done.** A month name formatted with the machine's locale and then parsed back to sort by it, and a latent crash on the round list. |
| 2 | **Dashboard/league discovery** ✅ | A rule enforced only by SQL's three-valued logic, and an entry code that no longer travels to non-members. |
| 2 | **Dashboard/pending membership** ✅ | Two views of one thing, and a third latent crash of the same shape as the first two. |
| 2 | **Dashboard/active rounds** ✅ | A domain rule that disagreed with its own SQL twin, and with its own sibling. Marked "Dashboard is done" here, wrongly - `GetMatchesForRoundQueryHandler` still held SQL, and moved with Admin/Rounds below. |
| 2 | **Badges** ✅ | **Badges is done.** Six statements to three reads, two gap-and-island streak queries to one `foreach`, and two screens that disagreed about the same player's position now reading one set of standings. |
| 2 | **Admin/Rounds + round fixtures** ✅ | A CTE written twice and selected never, and the near-twin fixture statement that was still outstanding from Dashboard. |
| 2 | **The small reads** ✅ | Ten files at once: homepage, onboarding, account, manage leagues, league bank details, prize scheme, and two notification handlers that had the same statement each. |
| 2 | **Predictions + share card** ✅ | Two screens onto the same two reads, and a round-naming rule that disagrees with the rest of the site on every league round. |
| 2 | **Season pass admin + pricing** ✅ | The one read where filtering and paging stay in the adapter, a rule that moved the *other* way, and a service whose three of five reads were already ports. |
| 2 | **SeasonPasses** ✅ | Four screens, four statements, one set of conditions - two of the screens turned out to be exact complements. Three `GETUTCDATE()` calls gone. |
| 2 | **Admin/Users** ✅ | Eleven correlated subqueries, three of them with an unstated definition of "spend", and a list flattened into a string by the database and split back apart in C#. |
| 2 | **Admin/Seasons** ✅ | The same twenty-column statement written twice, and a team count whose definition disagrees with the other read of the same idea. |
| 2 | **Admin reference data** ✅ | Nine handlers, seven ports, one statement deleted as a duplicate of a port that already existed - and a nullable column two of three reads denied. |
| 2 | **Admin/Rounds/results digest** ✅ | Six tables and two CTEs into four reads. A top-scorer tie-break that disagreed with every leaderboard, and the one read on the site that skipped the round-naming rule. |
| 2..N | **One feature area per PR** | Define the query interfaces, move the SQL, classify each predicate, move the rules to C# with unit tests, drop the handler's exclusion, add conformance tests. |
| Last | **Lock it** | The "no SQL in Application" convention test goes from advisory to enforced once the count reaches zero. |

**Phase 0 was split into four once the survey was done.** It was planned as one PR, but the pieces turn
out to be independently landable, because repositories depend only on Application's
`IDbConnectionFactory` / `IDbTransactionContext` and never on the concrete plumbing. Moving them in one
PR would also have meant a transitional state where Infrastructure referenced the adapter, which is the
very thing the new convention rule forbids. `BoostWriteRepository` uses `Microsoft.Data.SqlClient`
directly, so that package cannot leave Infrastructure until 0c.

`Data/DatabaseInitialiser.cs` deliberately stays in Infrastructure: it seeds Identity roles through
`RoleManager` and contains no SQL, so it would work unchanged against any adapter. It is the only file
left in `Infrastructure/Data/`.

### Why 0c dropped the two SQL-bearing services

`LeagueMembershipService` and `CachedEmailSettingsProvider` were listed for 0c on the strength of "if it
speaks SQL it moves". Reading them changed the answer, and neither blocks `Dapper` leaving Infrastructure
because both go through `IApplicationReadDbConnection` rather than using Dapper directly.

- **`CachedEmailSettingsProvider` is measured, not excluded** - it has real caching logic and its own test
  file. Moving it wholesale would drag tested non-persistence logic into the adapter, and phase 2 would
  move it straight back out. It is a caching decorator over a read: the cache is dialect-neutral, only the
  read is not.
- **`LeagueMembershipService` is authorisation.** "Is this user an approved member of this league" is a
  rule by the decision procedure above, so phase 2 splits it - query in the adapter, rule in C# - rather
  than relocating the pair.

Moving a file twice through two PRs is worse than moving it once, correctly. Both wait for their feature
area.

The same reasoning will apply to any other class that is logic-plus-SQL rather than pure persistence.
Phase 0 is for **relocation without behaviour change**; anything needing a split belongs to phase 2.

### 0d needed a production write, because DbUp journals by resource name

Moving the migration set was not a file move. DbUp keys `dbo.SchemaVersions` on each script's **embedded
resource name**, so all seven keys were `ThePredictions.DatabaseTools.Migrations.*` on production, dev and
backup. Moving the files renames the keys, and DbUp then sees seven unapplied scripts and re-runs them -
`0001_Baseline.sql` being 1,353 lines of DDL.

Three options were weighed. Moving the files while preserving the old resource names was rejected outright:
it would leave scripts under the adapter whose journal keys name the tool, which is *more* confusing than
leaving them alone. Leaving them was viable. Moving properly won, on the grounds that all seven scripts are
effectively idempotent (`0007` deliberately matches the exact old value rather than doing a blanket
`REPLACE`, and the other destructive one is guarded), so even a botched rename re-runs no-ops - and a month
of downtime is exactly the window for it.

The rename was run by hand on every database before the code landed, and verified. Two guards now stop this
being re-discovered the hard way:

- **`MigrationScripts`** in the adapter is the single definition of the script assembly and the journal
  table. `DatabaseTools` and the integration suite both use it, so they cannot disagree about either -
  previously each had its own copy of `"dbo"` / `"SchemaVersions"`, and a divergence would have silently
  started a second journal.
- **`MigrationScriptsTests`** asserts the seven names and their order. It is a change-detector test on
  purpose: what it detects is a schema-history break that is otherwise invisible until a migration runs
  against a real database, by which point the journal has already been written.

What is still written twice is the DbUp *wiring* (in `DatabaseTools.DatabaseMigrator` and the test
harness's `MigrationRunner`). Sharing that would mean a `dbup-sqlserver` reference in the adapter, which
would ship a migration engine inside the web application. Not worth it; the scripts and the journal
identity are shared, which is where divergence would actually hurt.

### The two read-only "commands"

`NotifyLeagueAdminOfJoinRequestCommandHandler` and `NotifyMemberOfLeagueApprovalCommandHandler` take
`IApplicationReadDbConnection`, which the root `CLAUDE.md` forbids for commands. Both only read - to
compose an email - and never write, so they are commands by MediatR shape and queries by behaviour. The
rule's table assumes commands mutate state.

**Phase 2 fixes them by default**, with no special case: each gets an injected `I<Thing>Query` like every
other read, and the read connection disappears from the constructor. Deliberately *not* by sending a
MediatR query from inside the command handler - that runs the pipeline twice and would subject a
system-initiated notification to authorisation written for an HTTP caller.

Two follow-ups belong with the Leagues feature area, not before it, because both would fail today:

- Reword the `CLAUDE.md` CQRS rule: a command that writes uses repositories; a command may read through an
  injected query interface, never through `IApplicationReadDbConnection` directly.
- Add the convention test that enforces it - no `*CommandHandler` in Application takes
  `IApplicationReadDbConnection`. Nothing enforces the CQRS split today at all.

### Phase 1 could only move one of the three test classes

The plan assumed the conformance harness was independent of the tests. It is not: a conformance suite's
shape is determined by the interfaces it tests, and for two of the three classes those interfaces do not
exist yet.

- **`RoundRepositoryUpdateTests` moved.** It goes through `IRoundRepository`, so it was already
  adapter-neutral in everything but its raw-SQL arrange and assert - which is exactly what
  `ITestDataSeeder` and `ITestDataInspector` now abstract.
- **`BoostUsageSecrecyTests` and `PredictableMatchPredicateTests` could not.** They construct handlers that
  still contain `GETUTCDATE()`, `SELECT TOP` and `ISNULL`. They become conformance tests when phase 2
  reaches Boosts and Rounds and moves that SQL behind query interfaces - not before.
- **`MigrationsFromEmptyTests` never will.** Each adapter has its own migration set, so it is
  adapter-specific by nature.

Doing the extraction now was still right, and for the reason originally given: every integration test phase
2 writes would otherwise need rewriting afterwards. The shape now exists, with one real suite proving it.

### A guard applied to one half of an answer but not the other - and kept that way

The season recap tells a player the highest position they ever held and how many rounds they held it for. Both come
from the same ranked trajectory, and the old statement guarded them differently:

```sql
FROM RanksPerRound WHERE [UserId] = @UserId AND [Total] > 0          -- finding the best position
(SELECT COUNT(*) FROM RanksPerRound WHERE [UserId] = @UserId AND Rnk = ub.BestRank)   -- counting the rounds
```

The `Total > 0` guard exists because before anyone has scored, everyone is joint first. Applied to the position but
not to the count, it means a completed round in which the entire league scored nothing can be counted as a round
spent in first place.

**Settled by the owner on 2026-08-11: keep it exactly as it is.** A completed round happened, and whoever was top
of the league during it was top of the league, so it counts towards a position the player also reached properly.
The asymmetry is intentional, not inherited.

The guard still applies to *finding* the position, and that half matters just as much: without it, a player who was
only ever joint first during a scoreless round would be told their highest position was first - and so would every
other member of the league. Generous counting of a position legitimately reached is a different thing from
inventing the position.

Both halves are now pinned by tests named for the behaviour rather than for the anomaly.

### Two similar-looking worst-round rules, deliberately kept apart

The records tile's lowest-round record excludes rounds the player never entered. The recap's worst round does not -
and both are faithful to their old SQL.

They are different questions. The tile's is a league record, a hall of shame, and letting an unplayed round win it
makes it a record about administration rather than about football. The recap's is a personal statistic in a page
that already tells the player what they missed, so a nil round they skipped genuinely was their worst. Same shape,
different question - the third instance of that pattern after the two visibility rules and the three snapshot
conditions.

### Non-determinism as a rule that was never written down

Four of the records tile's ten `SELECT TOP 1` blocks ordered by score alone. With two players tied, which one the
page named was the query plan's choice - stable in practice, not guaranteed, and free to change when statistics or
an index changed. Nothing in the codebase said which should win, so there was nothing to be wrong about.

`LeagueRecords.Highest` / `.Lowest` now take three keys: the score, a tie-break that means something (the earliest
round to reach a total, the earliest prize awarded), and the full name as a final deterministic fallback. Same
policy as `Ranking`, so a joint record holder and a joint leaderboard position are broken the same way.

Worth separating from the duplication findings: this is not a rule stated twice, it is a rule never stated. It only
becomes visible when the choice has to be written in a language that cannot shrug.

### Two more places the database was deciding presentation

Both found in the records tile:

- `DATENAME(MONTH, DATEFROMPARTS(2000, w.[Month], 1))` labelled a monthly prize, so the month's language came from
  the SQL Server login's configured language. Two identical databases could label the same prize "March" and
  "marzo". Now `PrizeDescription.For`, pinned to the invariant culture - and it returns no label rather than
  throwing on a month outside 1-12, which `DATEFROMPARTS` did, taking the whole tile down with it.
- The empty-wording check was `PrizeDescription <> ''`. SQL Server ignores trailing spaces when comparing strings,
  so `'   '` counted as empty there. `IsNullOrWhiteSpace` is the faithful translation; `IsNullOrEmpty` would have
  been a silent behaviour change. A test pins the three-space case.

### `LeaguePrizeSettings.PrizeType` holds numbers, not names

Declared `nvarchar(20)` and documented as holding `Overall`, `Monthly`, `Round`..., it actually holds `"0"` to
`"4"`: the write path passes the enum and Dapper sends its underlying int. The old comparison
`lps.[PrizeType] = @RoundPrizeType` therefore compared text to an integer and worked only because SQL Server
silently converted the column - had the column held the names it was documented as holding, the tile would have
failed outright with a conversion error.

The adapter now maps it to the `PrizeType` enum, so the next adapter has to produce a real enum member and cannot
inherit the accident. `docs/guides/database-schema.md` corrected. Three more comparisons of this shape remain in
`WinningsRepository` on the write side.

### Who can hold a record: an inconsistency found, then settled

The records tile's ten blocks disagreed about membership. Five read `LeagueRoundResults` with no membership check;
two joined `LeagueMembers` and required `Approved`. So a player outside the league could in principle hold its
highest-round record while being ineligible for its most-exact-scores record - and their points would appear on no
leaderboard that could corroborate the claim.

**Settled: all ten draw from the league's approved members**, decided once in the handler rather than ten times in
SQL. The owner chose this on 2026-08-11 after the finding was raised.

Three things made the direction clear:

1. **It changes nothing today, and cannot.** `Approved` is a terminal state - `LeagueMember.Approve()` and
   `Reject()` both require `Pending`, and only a pending request can be cancelled - and the `LeagueRoundResults`
   MERGE writes only for approved members. Dev confirms it: 1,616 result rows, none against a non-approved
   membership, and no rejected memberships at all. The filter is future-proofing, not a fix.
2. **The two filters were never the same rule.** The exact-score blocks filter because `RoundResults` is
   *league-agnostic*: without it a stranger's exact scores would show as the league's record. That filter is
   load-bearing. The `LeagueRoundResults` blocks have no filter because they never needed one. So "add it
   everywhere" costs nothing, while "remove it everywhere" would have broken the exact-score records outright.
   The first framing of this finding got that backwards.
3. **Consistency beyond the tile.** Every other read on the site shows approved members only, and the champion was
   already computed from them - so the tile could have named one player champion while crediting another with
   winning more rounds.

The adapter may still narrow at the source, and the exact-score read does, because reading every player's whole
season to discard most of it would be wasteful. Narrowing can only remove rows the handler's filter would remove
anyway, so it is an optimisation rather than a second copy of the rule - stated in
`LeagueRecordsData`'s remarks so the distinction survives the next reader.

If a leave-or-remove feature ever arrives and the product wants records to be historical, this is one line and one
test to reverse - deliberately, rather than by inheriting an accident.

### A domain rule that disagreed with its SQL twin - and with its own sibling

The dashboard worked out when a round's last match locks as a `COALESCE` over a correlated `MAX`:

```sql
COALESCE((SELECT MAX(lm.[CustomLockTimeUtc]) FROM [Matches] lm
          WHERE lm.[RoundId] = r.[Id]
            AND lm.[Status] <> @PostponedStatus
            AND lm.[CustomLockTimeUtc] > r.[DeadlineUtc]), r.[DeadlineUtc])
```

`Round.GetLatestPredictionDeadline()` already answered that question in the domain - **over every match, postponed ones
included**. So for a round holding a postponed match with a late custom lock, the two disagree: the SQL says the round has
closed, the entity says it is still open.

Worse, the entity disagrees with itself. Its sibling `GetNextPredictionDeadline` filters by `Match.IsOpenForPrediction`, which
requires the status to be `Scheduled` - so one method counts postponed matches and the neighbouring one does not.

The read path now uses `PredictionWindow.LatestDeadline` over the non-postponed matches, preserving what the dashboard did.
The entity method is untouched because it has other callers. **Open question for the owner:** should
`Round.GetLatestPredictionDeadline` exclude postponed matches, so that it agrees with both its sibling and the dashboard?

This is the first case of a duplicated rule where the SQL copy was the **more** correct one. Every previous instance had the
C# version right and the SQL version drifting.

### A flag that must not be computed from the rows beside it

`HasConfirmedMatch` decides whether a round is worth showing, and the old `EXISTS` looked at every match - including postponed
ones. The matches returned alongside it exclude postponed ones, so deriving the flag from those rows would silently drop a
round whose only confirmed fixture had been called off.

It is answered in the adapter for exactly that reason, with a conformance test for the case. Worth noting as a shape: when a
port returns both a filtered collection and a flag about the unfiltered one, the flag has to stay with the source.

### The same nullable-deadline crash, a third time

`GetPendingRequestsQueryHandler` read `l.[EntryDeadlineUtc]` into a non-nullable `DateTime`, and unlike the prize page it had
**no deadline filter** to accidentally exclude nulls - so a request to a league without a deadline would have failed to
materialise and taken the player's whole dashboard down.

Third instance of one shape: a nullable column read into a non-nullable field, safe or unsafe depending entirely on whether
some unrelated `WHERE` clause happened to filter nulls out. The three sites behaved differently for reasons nobody chose:

| Site | Deadline filter | If a league had no deadline |
|---|---|---|
| League settings | none | `ISNULL` sentinel - guarded |
| Prize page | none | would have crashed |
| Pending requests | none | would have crashed |
| Available leagues | `> GETUTCDATE()` | filtered out, so never reached |

**Worth doing as a sweep rather than one at a time:** every remaining `SELECT` of a nullable column into a non-nullable
result field. The pattern is mechanical to find and the failure is always a 500 on a page that renders fine today.

### An unreachable sentinel, removed rather than covered

The admin pending-members handler got the same 1900 sentinel as its siblings - and the coverage gate showed it was never
executed. It could not be: the rule that decides which leagues are still taking entries already rejects a league without a
deadline, so the null branch is unreachable. Deleted, with a comment saying why the value is safe to read directly, per the
"remove unreachable code rather than excluding it" rule in CLAUDE.md.

A useful side effect of the 100% gate: it distinguishes a defensive default that is needed from one that is decoration.

### Two views of one thing, and a one-tick difference

The applicant's view of a request and the administrator's view of the same request are separate reads and stay separate -
they answer different questions from different sides. But sharing the concept exposed a difference:

- League discovery uses `EntryDeadlineUtc > GETUTCDATE()` - at the deadline itself, a league is no longer joinable.
- The administrator's pending-members view used `>=` - at that same instant, they still see it.

One tick apart, almost certainly incidental. Preserved, both pinned by tests naming the boundary, and recorded here.
**Open question for the owner:** should the administrator's view use the same boundary as the player's?

### The dismissal rule

A rejected request keeps showing on the applicant's dashboard until they dismiss the notice, then stops - which was
`(Status = @Rejected AND IsAlertDismissed = 0)` inside an `OR`. The dismissal only ever applied to rejections; a pending
request cannot be dismissed because the player is still waiting for an answer. Both halves now stated and tested,
mutation-verified.

### A rule enforced only by SQL's three-valued logic

Both league-discovery queries filtered with `l.[EntryDeadlineUtc] > GETUTCDATE()`, which did two things. The comparison is
the obvious one. The other is that `NULL > anything` is **unknown** in SQL, so a league with no entry deadline was silently
never offered to anybody - a rule nobody wrote down, enforced by the absence of a value.

It is now `LeagueEntry.IsOpen`, which says it in a sentence and can be read by somebody who does not think in SQL nulls.
Mutation-verified in both the Domain rule and both handlers.

Worth generalising: a `WHERE` clause on a nullable column is always two rules - the comparison, and the exclusion of nulls.
The second one is invisible and is the one that gets lost in a rewrite.

### A secret that no longer travels

The available-leagues list selected `EntryCode` in order to compute `IsPrivate` from it, and these are by definition
leagues the reader is **not** a member of. The code is the one thing that lets somebody into a private league.

`JoinableLeagueRow` carries `HasEntryCode` and has no field for the code at all, so the mistake is now unavailable rather
than merely avoided. Not a live leak - the old handler mapped the flag and dropped the code - but the shape allowed it and
now does not.

### Two queries asking "could you join this?" differently

The available-leagues list and the "you have private leagues available" hint share one read and filter it differently:

| | Available list | Private-league hint |
|---|---|---|
| Private league not listed by its admin | hidden | **counted** |
| Player holds no pass for the season | hidden | **counted** |

The first difference is deliberate and right: an unlisted private league should not appear in a browsable list, but somebody
who has been handed a code should be prompted to type it.

The second looks like an oversight. The hint can appear for a league the player cannot enter without buying a pass first,
which is the "invitation to a dead end" the available list carefully avoids. Preserved, with a test naming it.
**Open question for the owner:** should the private-league prompt require a season pass, like the list does?

### A test-helper trap, twice

Two helpers in this work defaulted a nullable parameter with `?? someDefault`, which makes passing `null` indistinguishable
from passing nothing - so the "no value" case silently tested the default instead. Found in the winnings tests by a mutation
that should have failed and did not, then again here.

The fix that reads best is to leave the helper's default alone and say the special case at the call site:
`League(...) with { EntryDeadlineUtc = null }`. Worth knowing before writing the next helper: if a test's name mentions a
null, check the helper cannot swallow it.

### A value formatted, then parsed back to sort by it

The winnings page named each monthly prize with `CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(...)` and then
ordered the list with:

```csharp
DateTime.ParseExact(p.Name, "MMMM", CultureInfo.CurrentCulture).Month
```

A round trip out of a number, into a localised string, and back again - which fails outright if the culture that formatted
it is not the culture parsing it, and which no test could have caught on an English machine. The numbers are now carried to
the last step and only turned into names there, so there is nothing to parse back. Third instance of the
`CurrentCulture` family of findings, and the first where the round trip made it a correctness risk rather than only a
cosmetic one.

The same shape appeared in the round list: `Name = winner.RoundNumber.ToString()` and then
`OrderBy(p => int.Parse(p.Name))`. A round prize recorded **without** a round number produced an empty name and took the
whole page down on the sort. Both lists now carry their number through, and a win missing the period it belongs to is
skipped with a test naming it - a prize absent from a list is a far better failure than a page that will not load.

### A test whose name was a lie, caught by the mutation check

While mutating `AreWinningsWorkedOut` to treat a league with no entry deadline as still open, the test called
`Handle_ShouldTreatALeagueWithNoDeadlineAsClosed` **passed anyway**. Its helper defaulted a null deadline to the season
start, so the test had quietly been checking a *past* deadline all along.

Worth recording as a method note rather than a finding about the code: the mutation check is not only for verifying that a
moved rule is wired in - it also finds tests that pass for the wrong reason. A test that survives a mutation of the exact
behaviour it names is either wrong or misnamed.

### The one page whose prize pot ignores the top-up

The winnings page works its pot out as `EntryCount * EntryCost`. The other three sites go through `PrizeFund.Total`, which
adds the administrator's `PrizeFundOverride`. So a league with a funded top-up shows a smaller pot here than on its own
dashboard.

Preserved, with the top-up returned by the port and unused so the difference is visible in the handler rather than hidden
in a `SELECT` that omitted the column - the same treatment as the member counts. **Open question for the owner:** should
this page include the top-up like the others?

### Two definitions of "is the season over"

The dashboards ask it one way and the payouts screen asks it another:

```sql
COUNT(completed rounds) >= s.[NumberOfRounds]                     -- the dashboards
EXISTS (any round) AND NOT EXISTS (any round that is not complete) -- the payouts screen
```

**They can disagree.** A season declaring 38 rounds but holding 40, of which 38 are complete, is finished by the first
and unfinished by the second. Both now live in `SeasonCompletion` as `IsFinished` and `IsEveryRoundComplete`, side by
side, with a test named `TheTwoDefinitionsCanDisagree` that pins the divergence as a documented fact rather than a
surprise waiting to be found.

Not merged, because merging them changes what one of the two screens says and which is right is a product question.
**Open question for the owner:** should these be one definition, and if so which?

The payouts version also has a half worth keeping: *and at least one round exists*. Without it an empty season reports
itself finished and the screen offers to pay out a season that has not started. Mutation-verified.

### Money already sent is a historical fact

The payouts screen totals what is outstanding from **live** prize amounts and what has been paid from the **recorded**
ones. That asymmetry is deliberate: re-pricing a prize after somebody has been paid must not rewrite what the screen says
left the administrator's account - it must show up as a discrepancy instead. Making both use live totals compiles cleanly
and fails the test that pins it.

### Tests that counted SQL statements

`GetLeaguePayoutsQueryHandler` was the one query handler that already had tests, and they worked by mocking
`IApplicationReadDbConnection` and telling its four reads apart by their generic argument. That passes, but it couples the
tests to how many statements the handler runs and in what shape - so the tests had to change whenever the SQL did, which
is the opposite of what a test is for. Same coverage now, arranged through the port.

Worth noting alongside the coverage-exclusion finding: both are ways the old shape made tests either impossible or
misleading.

### A nullable column the result type said was not

`GetLeaguePrizesPageQueryHandler` selected `l.[EntryDeadlineUtc]` into a non-nullable `DateTime`. The column allows null,
so a league without an entry deadline would have failed to materialise and taken the prize page down with a 500.

**Latent rather than live**, and worth being precise about: the create and update commands both take a non-nullable
deadline and the validators require one, so nothing in the application writes null - dev confirms it, none of its six
leagues has one. The page is now honest about the column anyway and shows the same "never" sentinel the league settings
page already uses. Mutation-verified: trusting the value with a `!` compiles and fails four tests.

Note the asymmetry it exposes: the settings page defended against a null with `ISNULL` while this page did not. Two pages
reading the same column, one guarded, one not, both fine today - which is what makes it the kind of thing worth a test
rather than a comment.

### A third unfiltered member count

The prize page counts memberships with no status filter, so its prize-pot preview includes pending and rejected requests.
That is the same divergence as the league settings page, and now the same treatment: the port returns both counts, the
handler uses the total to preserve today's figure, and a test names it. **The open question covers both pages** - if the
count should be approved-only, it is two one-line changes.

### A flattened join that made every column nullable

The prize page's statement left-joined the prize settings onto the league, so a league with four prizes came back as four
copies of its own details and a league with none came back as one row of nulls. Every prize column was therefore declared
nullable, and every read of one needed a `!` to promise the compiler otherwise.

Two reads instead: a header and a list. `LeaguePrizeSettingRow` has nothing nullable except the stage, which only a stage
prize has. Worth noting as a shape rather than a bug - `!` on a column that is only nullable because of how it was
fetched is a smell that points at the fetch.

### A coverage exclusion that was simply untrue

`GetLeaguePaymentInfoQueryHandler` carried the same justification as every other query handler - "the body is a SQL
string plus a mapping" - and it was not. Underneath the statement sat the rule that decides **who may read a league's
bank account**: the administrator, anyone with a membership row, or a prospective joiner holding the entry code. It had
no tests at all.

It has twelve now, and two of them exist because of what the mutation check found:

- A blank entry code supplied by the caller must never match a league that has none. Remove the blank check and
  `string.Equals(null, null)` is true, so **a public league's bank details become readable by anybody**. That is one
  deleted line away, and it compiles.
- The details are decrypted *after* the check, not before. Moving the check three lines later still returns a 401 for a
  stranger, but the plaintext exists in memory first - and a later refactor that returned early would leak it. Pinned by
  a test asserting the decryption service is never called for a refused caller.

The lesson for the register: a boilerplate exclusion applied by pattern rather than by reading the code is worse than no
exclusion, because it looks considered. Worth a sweep of the remaining ones for the same mistake.

### Membership with no status filter, twice, meaning two different things

`HasMembership` on the payment query is `EXISTS` with no status check, so **any** membership row counts - approved,
pending or rejected. The pending case is load-bearing and clearly deliberate: you ask to join, then you need the bank
details in order to pay. Whether it should also cover someone who was **turned away** is the open part. Preserved, with
a conformance test naming all three statuses. **Open question for the owner:** should a rejected applicant still see the
bank details?

Contrast the member-management page, which lists memberships of every status on purpose, because rejecting is what that
page is for. Two unfiltered membership reads, two different reasons, neither shareable with the other.

### Sentinel values that only the SQL knew about

`GetLeagueByIdQueryHandler` answered three questions with values the database does not hold:

```sql
ISNULL(l.[EntryCode], 'Public')                              -- a public league has no code
ISNULL(l.[EntryDeadlineUtc], '1900-01-01')                   -- a league with no deadline
CAST(CASE WHEN c.[Type] = 1 THEN 1 ELSE 0 END AS bit)        -- is it a tournament
```

All three are presentation, and the second is the one worth pausing on: a date in 1900 standing for "never", chosen
because `LeagueDto.EntryDeadlineUtc` is not nullable. It is now a named constant with an explanation attached rather
than a literal buried in a `SELECT`, and a test asserts it. **Open question for the owner:** should the contract be
nullable, so "no deadline" stops being a date? That would ripple into the pages that format it, which is why it was not
done here.

### A member count that counts the wrong members

The same handler reported `COUNT(lm.[UserId])` over an unfiltered join - so a league with five members and two pending
requests reports seven. Every other member count on the site counts approved members only.

The port now returns **both** counts and the handler uses the total, preserving today's number. Returning both is the
point: the difference is visible in one place, a test names it, and switching is a one-line change. **Open question for
the owner:** should this count approved members only, like everywhere else?

### Two callers, one read, two answers to "viewable"

The league dashboard and the dashboard's round picker each listed a league's rounds with the same eight columns and the
same correlated fixture count. One read (`ILeagueRoundsQuery`) now serves both. What they do not share is the filter:

- the dashboard lists **every** round, drafts included, and calls the result `ViewableRounds`;
- the picker keeps `Status IN (@Published, @Completed)` - and so drops a round **in progress**, which is arguably the
  one most worth looking at.

Both preserved exactly, both now tested. **Open question for the owner:** should a round in play be pickable on the
league dashboard?

### Two statements that were the same statement

The month picker and the stage picker each read the league's season rounds and each computed the same three things -
rounds remaining, rounds completed, and whether the period is worth offering at all - as near-identical
`SUM(CASE WHEN ...)` columns. They differed only in what they grouped by: the calendar month, or the tournament stage.

One read (`ILeagueSeasonRoundsQuery`) now serves both, the counting is `Domain.Services.RoundProgress`, and each
handler keeps its own grouping and ordering. This is the first slice where two handlers' SQL turned out to be one
query rather than two.

Two rules came out of it worth naming:

- `SeasonMonthOrder` - a season starting in August runs August to December and then January to May, so a picker
  listing its months in calendar order would be useless. It was an `ORDER BY CASE WHEN Month >= StartMonth` over a
  cross-joined CTE whose only purpose was to find the season's first month. Note it works because the wrapped months
  are in the *next* calendar year, which is what makes `MIN(StartDateUtc)` land in August - a first draft of the test
  got this wrong and was corrected rather than the code.
- `RoundProgress` - and its quirk, preserved: a draft round counts as "still to come". So a month offered because it
  holds one published round reports its unpublished rounds as remaining. That was `SUM(CASE WHEN Status <> @Completed)`
  and is now stated with a test.

The stage picker also held the **fifth** copy of the stage display name and another copy of the collation-dependent
`LIKE '%Group%'`. Both now go through `TournamentStageName` and `TournamentStageClassifier`.

### `CultureInfo.CurrentCulture` - the same localisation bug, in C# this time

The month picker named its months with `CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(...)`, so the name
depended on the locale of whatever machine served the request. Exactly the fault found twice in SQL as
`DATENAME(MONTH, ...)`, and worth recording as its own finding: moving a rule out of SQL is not the same as making it
deterministic. Now `MonthName.Of`, invariant, consistent with the rest of the site.

### Infrastructure holds no SQL

`CachedEmailSettingsProvider` was the last file, and the caching it does is genuinely an Infrastructure concern - so
the provider stays there and its statement moved to `IEmailSettingsQuery`. Its conformance tests pin the distinction
that matters: an absent settings row must read as `null` rather than `false`, because the two look alike to a careless
adapter and mean opposite things. Reporting `false` would silently stop every email the site sends.

### The guard in front of eighteen queries was excluded from coverage

`LeagueMembershipService` lived in Infrastructure holding two `COUNT(*)` statements, marked
`[ExcludeFromCodeCoverage(Justification = "Repository composition over SQL: no branching logic of its own.")]`.
The justification was wrong on its own terms - the composition **was** the logic. Reading whether someone is a member
is a fact; deciding that a non-member gets an exception, and which one, is a rule, and eighteen handlers depend on it.

Split: the two reads are `ILeagueMembershipQuery` in Persistence, the two `Ensure*` decisions are C# in Application
with tests. Two consequences worth recording:

- Infrastructure is now down to **one** file containing SQL (`CachedEmailSettingsProvider`).
- `GetLeagueDashboardQueryHandler` had a **fourth** copy of the same membership `COUNT(*)`, written inline because it
  needed a different answer to a failure. It now shares the read while keeping its own rule - which is the shape to
  aim for whenever a caller "cannot use the shared thing because it throws the wrong exception".

### A security rule that only reads correctly with its comment

The league dashboard answers `EntityNotFoundException` for a non-member rather than "not allowed", so a stranger
cannot discover which leagues exist by reading status codes. It looks like a mistake - the league does exist - and
the original SQL-era handler had a paragraph of comment explaining that it was not.

It is now a named method with that paragraph attached and a test asserting the 404, mutation-verified: changing it to
an `UnauthorizedAccessException`, which is what a reader "tidying up" would do, compiles cleanly and fails.

### A rule that spans the read and the write side

Which round a league's tile is about - a round in play, else one finished within forty-eight hours, else the next
published one, lowest number first, never a draft - is now `Domain.Services.ActiveRound`. It was a
`ROW_NUMBER() OVER (PARTITION BY ... ORDER BY CASE ... END)` over `GETUTCDATE()`.

`LeagueStatsRepository` resolves the same active round on the write path, to decide which round its cached ranks
belong to. The schema documentation says so outright: the ranks are "relative to the league's active round ...
resolved by the same priority order the query uses". **If the two ever pick different rounds, the tile shows one
round's number above another round's positions** - a bug that reads as bad data rather than bad code.

That copy is on the write side and so out of scope by the owner's decision, which means this is the first moved rule
whose duplicate cannot be collapsed yet. The mitigation is that it is now one named, tested rule with a stated
obligation rather than two lists of `CASE` arms that happen to match: the repository adopts it when the write side
moves. Worth doing first when that work starts.

### The tile-ordering rule had three copies, in two languages

Recorded earlier as a single-file oddity; the count turned out to be worse. `GetLeaderboardsQueryHandler` stated it
as a SQL `ORDER BY` **and** as an identical LINQ chain over the same rows, and `GetMyLeaguesQueryHandler` had a third
copy in its own `ORDER BY`. Now `LeagueTileOrder.Apply`, over an `ILeagueTile` both row types implement. The
leaderboards tile's eighteen existing tests pass against it unchanged.

### Rules shared this round rather than restated

- `PrizeFund.Total` / `.Remaining` - the pot is entry fees plus the administrator's top-up, written out in SQL in
  two places. One copy remains in `GetAvailableLeaguesQueryHandler`, which moves with the rest of Dashboard.
- `MonthName.Of` - `DATENAME(MONTH, ...)` in two queries, so the month's language came from the database login.
  `PrizeDescription` adopted it here.
- `TournamentStageName.For` - the display spelling, paired with `TournamentStageClassifier` so classifying a round
  and naming its stage cannot drift. Fourth site adopting the classifier; the remaining copies are write-path.

### A nullable comparison that cost a branch, again

`completedDateUtc > utcNow - window` on a `DateTime?` compiles to a lifted comparison carrying its own null branch -
unreachable behind an earlier null guard, and so uncoverable. The coverage gate caught it, as it did for
`lastSent == null || lastSent < targetTime` earlier in this work. The fix is the same both times: unwrap with
`is not { } value` and compare the non-nullable. Third instance of this trap; worth knowing before writing the
guard, not after.

### A rule duplicated between SQL and C# in the same file

`GetLeaderboardsQueryHandler` ordered its leagues with a four-clause `ORDER BY` (round in progress first, then
season start, then stake descending, then name) and then re-sorted the same rows with an identical LINQ chain a
few lines later. Only the C# copy decided anything; the SQL half survived because it looked like it had to
agree with its twin.

Worth naming as its own failure mode. The registers below track rules duplicated across files, but a rule can be
duplicated across *languages* within one method, and that copy is the harder one to see - a reader who checks the
SQL finds the rule stated correctly and stops looking.

### Rules now shared rather than restated

`Wins.ByPeriod` - who won each round, and each calendar month - was **four** `RANK() OVER (PARTITION BY ...)`
windows across two handlers. The records tile asked who had won the most; the recap asked how many one player had
won. The same rule answering two questions, so it is stated once and each caller counts what it needs. Two parts of
it were easy to lose and are now pinned by tests: joint winners both win (`RANK`, not `ROW_NUMBER`), and a period
nobody scored in is won by nobody.


Two moved this round, both because the conditions turned out identical rather than merely similar:

- `SeasonCompletion.IsFinished` - `COUNT(completed rounds) >= NumberOfRounds`, written three times in SQL. The
  `>=` rather than `=` is the part worth stating once: a season carrying more completed rounds than it declares
  is finished, not broken. Two copies remain (`GetMyLeagues`, `GetLeagueDashboard`).
- `LeaderboardSnapshot.RankToShow` - whether a cached pre-round position is worth showing. The league's own
  overall table and the dashboard's tile asked this identically, so the overall handler adopted it here. The
  monthly and stage leaderboards deliberately do **not**: they need more than one of their rounds started, which
  is a different question wearing the same clothes.

### Two visibility rules that are not one rule

`BoostUsageVisibility` (moved with the boost work) and `PredictionVisibility` (moved with the round grid) read
almost identically:

```
AND (ubu.[UserId] = @CurrentUserId OR r.[DeadlineUtc] <= GETUTCDATE())          -- boosts
WHEN COALESCE(m.[CustomLockTimeUtc], r.[DeadlineUtc]) > GETUTCDATE() AND ...    -- predictions
```

They answer different questions. A boost is played for a **round**, so the round's deadline governs it. A
prediction belongs to a **fixture**, and a custom lock time can bring that fixture's deadline forward or push it
back - so a league's grid can be half revealed while the round is still open, which the round's deadline alone
cannot express. Sharing them would have been a one-line change that quietly leaked predictions for every
early-kick-off fixture, or hid them for every late one.

The lock comparison itself is **not** duplicated: both go through `Match.IsPredictionLocked`, so the inclusive
boundary (a fixture whose deadline is exactly now has locked) is decided in one place for predicting and for
revealing alike. That is the shape to aim for - share the mechanism, keep the questions apart.

### `Status IN (@Scheduled, @InProgress, @Completed)`, stated the right way round

Two queries listed every status except `Postponed` rather than naming the one they meant. `Match.IsPostponed`
now says it directly, and the round grid uses it. The behaviours differ if a status is ever added: a whitelist
silently drops the new value, the negation lets it through. Deliberately the second - a fixture appearing when
it should not is reported, a fixture quietly missing from a results grid is not. **Both copies are now gone**: the
second, in `GetMatchesForRoundQueryHandler`, moved with Admin/Rounds, and `Match.IsPostponedStatus` is the row-level
form so the entity and the reads share one definition.

### The stage classification, and a live collation dependency

`CASE WHEN trm.[Stages] LIKE '%Group%'` appears in **nine places across six files**, and produces **three
different spellings** of the same stage name between them: `'Group Stage'`, `'Group stage'` (lowercase s, in
`BadgeEvaluationRepository`) and `"GroupStage"` (no space, via `nameof`).

That is worse than untidy. `LeagueStatsRepository` compares its spelling against a stored `ActiveStageName`, so
the variants agree **only because the collation is case-insensitive** (`SQL_Latin1_General_CP1_CI_AS`). On a
case-sensitive collation, or a different engine, badge evaluation and the stats recompute would silently stop
matching rounds to stages. This is the concrete instance of the collation argument that motivated naming the
adapter after the engine rather than the dialect.

`Domain.Services.TournamentStageClassifier` is now the classification, adopted by the stage leaderboard.
**Eight copies remain**, and two of them are in repositories on the write path (`LeagueStatsRepository` is the
ADR-0015 recompute), so those wait for the write-side decision. The *display* spelling is a separate question
and is still written out at each site; worth settling when those move, because changing it alters an equality
comparison rather than only a label.

### A label that disagrees with the rest of the site

The My Leagues tile labels its round `'Round ' + CAST(ar.[RoundNumber] AS VARCHAR(10))`, ignoring the round's
`DisplayName`. Everywhere else uses `Round.GetDisplayNameOrDefault()`, which prefers the name an admin gave it - so a
round called "Semi Finals" appears as "Round 12" on the dashboard and as "Semi Finals" everywhere else.

Preserved exactly, because switching it changes what a live tile says. **Open question for the owner:** should the
tile use the round's name where it has one?

### An asymmetry found in the domain, not introduced by this work

`Round.GetNextPredictionDeadline` skips a fixture only when it is **postponed** or has unconfirmed teams.
`Match.IsOpenForPrediction` requires the status to be **Scheduled**. So the two disagree about a fixture
marked completed or in progress whose lock is still ahead: the first would count it towards the next
deadline, the second would not count it as predictable.

**Resolved 2026-08-10.** The state means something has gone wrong upstream and cannot legitimately occur - a
match does not start before its own prediction lock - so on the owner's instruction the rule now assumes it
away rather than defending against it. `GetNextPredictionDeadline` is defined as *the earliest effective
deadline among the fixtures still open for prediction*, which makes it and `IsOpenForPrediction` share one
definition of "still open".

That turned out to remove code rather than add it. The chase email had grown a local `Min` over its open
fixtures purely to sidestep the disagreement; with the domain method answering exactly that question, the
email calls it again and the local duplication is gone. Both the milestone schedule and the email now read
the same value, so they cannot disagree about the deadline they are counting down to.

Deliberately **not** guarded here: if the broken state is ever worth detecting, the place is the job that
sets match statuses, not a read path.

### Two more compiler-branch traps

Both found by the 100% gate, both the same shape as the property pattern in the Boosts batch: the branches
the compiler emits do not correspond to logical outcomes.

- `lastSent == null || lastSent < targetTime` compares a `DateTime?` against a `DateTime`, and the nullable
  lifting adds a fourth branch with no outcome behind it. Split into statements.
- `GetNextPredictionDeadline(nowUtc) ?? round.DeadlineUtc` had an unreachable fallback, which the code's own
  comment admitted ("non-null because at least one fixture is open"). Per `testing.md` an unreachable branch
  is removed rather than excluded, which is what the change above does.

### Moving a rule out of SQL creates a new failure mode

This is the thing to watch for the rest of phase 2. While a rule is a SQL predicate it cannot fail to be
applied - it is inside the read. Once it is a C# function the handler has to remember to call it, and a rule
that is present, correct and unit tested but never invoked is a silent leak.

The Boosts batch proved both halves of the guard:

- Deleting the call outright is caught by the **compiler**, because `dateTimeProvider` becomes unread and
  `TreatWarningsAsErrors` rejects CS9113. That is luck rather than design - it only works while the clock has
  no other use in the handler.
- The realistic bug - rule computed, result discarded, unfiltered list passed on - compiles cleanly and was
  caught by **2 unit tests and 3 end-to-end tests**.

So each area that moves a rule keeps at least one test that runs the whole handler, not just the rule. That
is not redundancy with the rule's own unit tests; it is the only thing that catches an unwired rule.

### A branch-coverage trap worth knowing

`usage is { HasBoost: true, BasePoints: not null, BoostedPoints: not null }` reads well and lowers to **ten
branches for three conditions**, so the 100% branch gate cannot be met without inventing tests for outcomes
the logic does not have. Written as statements the branches match the meaning. Expect this with property
patterns generally; the gate is right and the pattern is the problem.

### Undeclared dependencies the split keeps exposing

Removing `Microsoft.Data.SqlClient` from Infrastructure in 0b broke all eight
`AuthenticationTokenServiceTests` with a `TypeLoadException`. `AuthenticationTokenService` uses
`JwtSecurityTokenHandler` but **nothing declared `System.IdentityModel.Tokens.Jwt`** - it arrived
transitively through the SQL driver, which pulls `Microsoft.IdentityModel.*` at 8.x. With the driver gone,
the JWT handler fell back to 6.35.0 against `Microsoft.IdentityModel.Tokens` 8.14.0, and 8.x had deleted
the `Microsoft.IdentityModel.Json.JsonConvert` the older handler calls.

The hosts were unaffected because they still reach the driver through the adapter, so this was confined to
the test project - but the accident was real: token generation depended on a database driver's dependency
graph. Fixed by declaring `System.IdentityModel.Tokens.Jwt` in Infrastructure, pinned to 8.15.0 to match
`ThePredictions.Web.Client`, so both ends of the token round-trip agree.

Expect more of these. A package that only ever arrived transitively is invisible until the thing dragging
it in moves, and moving things is what this plan does. Each one found gets declared where it is used.

### The phase 2 pattern, as established by Boosts/catalogue

Each area follows the same six steps. `GetBoostCatalogueQueryHandler` was done first precisely because it
has **no rules at all** - `SELECT ... FROM [BoostDefinitions]` plus a map - so the mechanics could be proved
before the interesting cases.

1. **Port in Application**: `I<Thing>Query` with one `ExecuteAsync`, plus its `<Thing>Row` record beside it.
2. **Implementation in the adapter**, under `Queries/<FeatureArea>/`. Every query gets a feature folder, so
   unlike the repositories that grouping is uniform.
3. **Classify each predicate** by the decision procedure. Scoping filters (`WHERE LeagueId = @LeagueId`,
   `IsEnabled = 1`) are part of the fetch and stay. Semantic, temporal and visibility predicates are rules
   and move.
4. **Rules to C#, with unit tests.**
5. **Drop the handler's `[ExcludeFromCodeCoverage]`.** It is now measured, so it needs tests - which is the
   whole return on this work, not a side effect.
6. **Conformance test the port**, asserting only what the port promises.

**Ordering counts as a rule.** `ORDER BY [Name]` defers to the database's collation, so the same rows can
arrive in a different order from a different adapter, or from the same adapter on a differently-collated
database. The catalogue port therefore promises no order and the handler sorts with an explicit
`StringComparer`. The conformance test deliberately does **not** assert order: pinning it would assert a
guarantee the port does not make and would fail an adapter that is perfectly correct.

**What "every rule to C#" means operationally.** The mandate was agreed for reads with no exceptions, and it
needs a definition or it collapses into "fetch every table and filter in memory". The working line is above
in step 3: choosing *which rows* is fetching; computing *what they mean* is a rule. Both halves of that are
visible in the next batch, where the boost secrecy predicate and the points-gained `CASE` move while the
league and season scoping stays.

Feature areas for phases 2..N, roughly worst-first by rule density: Boosts, Rounds, Leaderboards,
Leagues, Dashboard, Badges, Admin/Rounds, Admin/Seasons, Predictions, Authentication, Account,
Payouts, External/Tasks.

### The badges area: four SQL statements that were all one question

Badges was the densest rule concentration in the codebase, because none of badge progress is stored - it is
recomputed on every read, so every metric had to be expressed as a statement. Six statements served one screen,
including two **gap-and-island** queries with four chained CTEs and a pair of subtracted
`ROW_NUMBER() OVER (PARTITION BY ...)` windows apiece. That technique is how a set-based language expresses "a
run of consecutive rows", and reading one of them tells you nothing whatever about what a streak is. Both are now
`Domain.Services.Badges.Streak`, twelve lines with a `foreach`, and the current-run rule turns out to be the same
loop without the running maximum.

The read that replaces all six returns rounds. One row per round of every season, with what the player did in it,
and no filtering at all - because the badges disagree about which rounds they care about, and a read that picked
one set would silently break the others. A streak counts rounds **anybody** was scored in, so a round the player
sat out breaks the run rather than being skipped; ever-present counts rounds that **finished**. Eighty-three rounds
exist, so this is a smaller read than any one of the statements it replaces.

### The same player, two positions, on two screens

The badges leaderboard and the dashboard tile both answered "where do I stand?", each with its own SQL, and they
disagreed. The page ordered by badge count and award date and then **numbered its rows one by one**, so players who
were genuinely level were shown different positions decided alphabetically - which
`Domain.Services.Ranking` explicitly forbids ("the tie-break affects order only, never the position awarded"). The
tile computed `COUNT(*) + 1 WHERE ahead of me`, which does let level players share. Measured on dev: nine players
hold no badges at all and five hold eighteen apiece with the same award date, so the two screens disagreed about
most of the table.

Both now read one `BadgeStandings`, ranked by `Ranking.ByDescending` over a comparable `BadgeTally` - the count,
then the date **reversed**, since reaching a tally sooner is worth more. Folding the tie-break into the score is
what lets it award positions rather than only sort rows.

The tile's statement had a second defect the rewrite removes. Its `Me` CTE was filtered by the same
"has a first name" rule as the table, so for an account that never finished signing up `Me` was empty, the
`CROSS JOIN` produced nothing, and `COUNT(*) + 1` returned **1** - the tile told a nameless account it was in first
place. It now shows no position at all, because such an account is not on the table.

### A column read for years and shown to nobody

`MAX([Detail])` per badge picked the alphabetically greatest caption of a group, which for round wins meant
"Gameweek 5" beat "Gameweek 12". Tracing it before porting it: `EarnedBadge.Detail` is set by the read, asserted by
one test, and read by no screen and no DTO. Rather than invent a rule for choosing between captions nobody sees,
the read stopped fetching them. The write side still records them, so nothing is lost if a screen ever wants one.

### Two ways of counting the same rows, in one feature

The badges page counts **awards** ("won 3 times"); the leaderboard counts **distinct badges** ("24 of 31 collected").
Both were `COUNT` in SQL, in different statements, and the difference was invisible. This is why the ports return
awards ungrouped: a read that grouped could serve one screen or the other, never both, and whichever it served the
other would quietly have been given the wrong number.

### A CTE written twice and selected never

Both administrator round statements opened with an `ActiveMemberCount` CTE: a `COUNT(DISTINCT lm.[UserId])` over the
season's approved league members, joined onto every round - and then selected in neither. One cross-joined its single
row onto the whole round list, the other left-joined it by season. Two copies of a join whose only effect was cost.

It is worth asking how this survives. A `SELECT` list is not checked against what a statement computes, so an unused
CTE is not a warning in any tool the build runs; and because the statements were inside handlers carrying the coverage
exclusion, no test ever named what they returned. The port makes the question unavoidable: an interface has to say what
comes back, and there was nowhere to put a number nobody wanted.

### "Dashboard is done" was wrong, and the plan said so

The Dashboard row above was ticked after the active-rounds PR, but `GetMatchesForRoundQueryHandler` was still holding
SQL - and this document had already recorded exactly that, in the note about `Status IN (@Scheduled, @InProgress,
@Completed)`: "One copy remains, in `GetMatchesForRoundQueryHandler`, and moves with the Dashboard area." The area
count is the check that catches this; a per-area tick is a claim, and this one was made from the wrong list.

It lands here rather than being deferred back, because it turned out to be the same read as the administrator's round
editor. Same twenty columns, same two left joins to `[Teams]`, same DTO - and three differences between the copies:

- One left out called-off fixtures, one did not. **Intended**, and the only real difference: a player cannot predict a
  postponed fixture, while the administrator's editor is the screen where one is put back.
- One ordered by kick-off, one did not order at all. So the editor's fixture list was in whatever order a join
  produced, and adding a fixture could reshuffle the page. Both now sort by kick-off and then home team, because a
  Saturday afternoon is mostly simultaneous kick-offs and without a tie-break the order is not stable.
- One declared the joined team columns as never-null; the other, correctly, as nullable. A tournament fixture with
  placeholder teams makes all ten of them null, and Dapper honours a non-nullable declaration by putting the null in
  anyway. The two copies of one read disagreed about the shape of the data, and one of them was simply wrong.

The administrator's copy also never selected `CustomLockTimeUtc`, so the per-fixture lock time could not be shown on
the screen that exists to set it. It is in the shared read now.

### A tie-break that disagreed with every leaderboard on the site

The round-results email names each league's top scorer, chosen by
`ROW_NUMBER() OVER (PARTITION BY LeagueId ORDER BY BoostedPoints DESC, u.[FirstName])`. Two problems in one clause.
The tie-break is on **first name only**, which cannot separate two players who share one - so which of them the email
named came down to whatever order the rows happened to arrive in. And it is a different tie-break from the one
`Domain.Services.Ranking` applies everywhere else, which is the full name, precisely because two players can share a
first name and a display name both. The league's own table and the email about it could therefore put different players
at the top of the same round.

It now goes through `Ranking.ByDescending` like everything else. Joint winners still yield one name, because the email
has one line for it - but it is the same name the league's table shows.

### The read that skipped a rule every other screen applied

`Round.GetDisplayNameOrDefault` has existed since the Rounds phase, and the digest read ignored it twice: once for the
round being reported on and once for the round it points forward to, both selected raw and dropped straight into an
email merge field. Checked on dev before claiming a bug: all 83 rounds carry a name, none blank, so this was a **gap
rather than a live fault** - the kind that surfaces as one blank email months after the read was written, on the first
round somebody creates without naming it.

Worth recording as a pattern: a rule collapsed into C# is not finished being collapsed. Each new read is another chance
to not use it, and nothing fails when it doesn't. `Round.DisplayNameOrDefault` is now the static form for the read paths
that hold columns rather than an entity, alongside `Match.IsPostponedStatus` from the previous PR - the same shape twice
in two days, which suggests the entity-only form is the thing that invites the copy.

### An email that could have gone to someone who never played

The digest's recipient list was an `EXISTS` on the player's predictions for the round, buried in the `WHERE` clause of a
six-table join. Everybody in a round is scored, including the players who forgot to predict - so without that clause the
email tells someone they got nothing right in a round they never entered. It is now `HasTakenPart`, one line with a name,
and a test that says what it is for.

Two more conditions were doing the same kind of work as inner joins: a recipient needs at least one league in the
season, and at least one league whose points for the round have actually been worked out. The second is the state a
league sits in between a round finishing and the points being computed, where a row of zeroes would read as a bad round
rather than an unfinished calculation.

### A test fixture that made a rule untestable

The conformance seeder wrote `DisplayName = "Round {n}"` into every round it created, so no test could arrange the
unnamed round the naming rule exists for. Fixing that meant giving `AddRoundAsync` a `displayName` parameter - and the
first attempt defaulted it to null, which failed 153 tests at once because the column is `NOT NULL`. The lesson is the
seeder's: it stands in for the write path, so its defaults have to be states the schema actually permits.

### The same column, three reads, two of them wrong

`Teams.LogoUrl` is nullable - a team added by hand before its badge has been found has none. Three reads select it:
the administrator's team list and the single-team screen declared it `string`, and the season-pass page declared it
`string?`. Dapper honours a non-nullable declaration by writing the null in regardless, so two screens held a null in a
field whose type said it could not. Fixing the row type made the compiler point straight at `TeamDto`, which had the
same lie, and then at the one line in the edit form that assumed a value. That is the whole argument for doing this
work: the type system can find these, but only once the shape is stated where it can see it.

### A port that already existed, and a handler that did not know

The administrator's email-settings screen carried its own copy of `SELECT TOP 1 es.[EmailsEnabled] ... ORDER BY es.[Id]`
- the identical statement to `IEmailSettingsQuery`, which was extracted earlier for the provider that caches this answer
for every outgoing email. No new port for this one: the handler now reads through the existing one and the statement is
simply gone. Nine handlers went into this batch and eight needed a port; the ninth needed a search first.

### "The earliest row wins" is a rule

`PricingSettings` and `EmailSettings` are single-row tables by convention rather than by constraint, and both reads
opened with `TOP 1 ... ORDER BY [Id]`. That is a decision about what to do if a second row ever appears, and it was
invisible inside a statement. The pricing row's id now comes back so the handler can make it out loud, with a test that
hands it two rows out of order.

### Three reads asking which teams are in a season

Nothing records which teams play in a season - it is worked out from the fixtures. The administrator's screen asked with
an `INNER JOIN ... SELECT DISTINCT` and the season-pass page with an `EXISTS`, which is the same question in two shapes.
The `EXISTS` form is the one kept, because it cannot produce a duplicate in the first place: the other's `DISTINCT` was
repairing its own join rather than stating a rule about teams.

### Two ways to count the teams in a season, and both are right

Nothing records which teams are in a season - it is inferred from the fixtures - and the codebase infers it twice, with
different answers. The administrator's season list counts the teams in the season's **first** round, via a nested
`UNION` inside a correlated `COUNT`. The season-pass page counts teams appearing **anywhere** in the season, via an
`EXISTS`. For a league those agree, because every team plays every round. For a knockout they do not: the first round
holds the entrants, later rounds hold survivors.

Neither is wrong - they answer different questions - but nothing said so, and the difference was buried in two
subqueries nobody would read side by side. Both are now named C# methods with the distinction written down.

### A status compared against a text literal, four times over

Each round-state count was its own correlated subquery ending `AND r.[Status] = 'Draft'`, with the state written in as a
string rather than parameterised from the enum like everywhere else in the codebase. A renamed status would not have
failed - it would have started returning zero, on an administrator's screen, quietly. The statuses now arrive typed and
the counting is four `Count` calls in one method.

### Definitions of "spend" that lived in a WHERE clause

The administrator's user list carries two money columns, and both had their definition buried inside a correlated
subquery. Season-pass spend was `WHERE sp.[Source] = @PurchasedSource` - a trial or a pass handed out by an
administrator counts as holding a pass but is not money anybody spent. League-entry spend was three conditions:
`lm.[Status] = @ApprovedStatus AND l.[IsFree] = 0 AND l.[Price] > 0` - a request never accepted was never paid for, and
the last two are two ways of saying the same thing that the data does not guarantee agree.

Nothing anywhere said any of this. The figures are the ones the site's own revenue gets judged by.

The mutation check earned its place on the third condition: dropping `Price > 0` survived, because a zero-priced league
adds nothing to a sum either way. It is only observable on a *negative* price - not a state the application creates, but
one the column allows, and a bad row must not reduce what somebody has spent. That is now a test rather than an
unexplained condition.

### A list flattened by the database and reassembled in C#

`STRING_AGG(ul.[LoginProvider], ',')` in the read, `SocialProviders?.Split(',')` in the handler. The database built a
comma-separated string and the handler took it back apart, which works right up until a provider name contains a comma.
It is a row per provider now, and the `GROUP BY` over seven columns that the aggregate forced went with it.

The same shape as the month name formatted by `DATENAME` and then parsed back to sort by it, found in the Leagues phase.
Two instances is a pattern: a set-based language makes it easy to collapse a collection into a scalar, and the collapse
is always undone somewhere.

### Two pages that were the same condition, negated

The available-passes page offered a season when it was active, not already held, and had a league whose entry deadline was
still ahead. The past-passes page listed a season when it was active, not already held, had leagues at all, and had **no**
league whose deadline was still ahead. Four conditions each, three shared, and the fourth the negation of the other's -
which is to say the two pages are complements by construction and nothing anywhere said so. A change to one would have
silently opened or closed a gap in the other.

Written out as `IsOnOffer` and `WasMissed` next to each other, the relationship is the first thing you see.

### The database's clock, three more times

`GETUTCDATE()` appeared in three of the four season-pass statements, so whether entry was still open depended on the
database's clock rather than the injected one - and two pages rendered in the same request could disagree with each other
about the same deadline. Each handler now reads `IDateTimeProvider.UtcNow` once and passes that instant to every decision.

This also picked up `Domain.Services.LeagueEntry.IsOpen` as its fourth caller, which is where "a league with no deadline is
not open" is stated. The old statements got that right only because SQL drops a null from a comparison - correct by
accident rather than by decision, and the kind of thing a rewrite in a different dialect would quietly change.

### A rule that moved into the adapter, not out of it

The pass-holders screen filters, sorts and pages in the database, and that stays: choosing which rows to return is fetching,
and a page cannot be taken without sorting first. But the handler was escaping the name filter's `%`, `_` and `[` before
handing it over - and those are `LIKE` wildcards. What needs escaping is a fact about how this adapter searches, not about
what an administrator meant, so it belongs on the other side of the boundary. The handler now passes the text as typed.

First case in this work of a rule moving **into** the adapter. Worth remembering that the boundary has two directions: the
question is not "is this SQL" but "does this knowledge belong to the database or to the business".

### A service whose reads were already ports

`SeasonPriceRecommendationService` made five reads. Three of them - the pricing settings, the payment provider's fee and the
running costs - were the same reads the administrator's own screens make, and those ports had been created two PRs earlier.
The pricing settings read was a third copy of `TOP 1 ORDER BY [Id]`, so "the earliest row is the live one" is now
`LivePricingSettings.From` with one definition and two callers.

Also here: the service reached the domain's annualisation rule by **constructing a `RunningCost` per row** with an id of
zero, a name of "cost" and two epoch dates, purely to read one property back off it. Fabricating an entity to borrow a line
of arithmetic invents state that was never in the database - the same mistake a draft of the active-rounds port made with
`Round`. `RunningCost.Annualise` is now the static form.

### The convention tests earned their keep twice in one PR

Removing the inline `DateTime.UtcNow` from the price recommendation made `ClockAccessConventionTests` fail - not because the
code was wrong, but because the file was still on its allowlist of permitted call sites, and the test asserts the allowlist
contains no file that has already been converted. An allowlist that fails when it becomes stale is worth copying.

The second failure was a false positive of a useful kind: the new adapter's own documentation mentioned
`DateTime.UtcNow` while explaining what had been removed, and the check is a text match. Reworded.

### Three ways to name a round, and they disagree on real data

Naming a round turns out to be done three ways:

1. `Round.GetDisplayNameOrDefault` - the stored name when there is one, otherwise "Round N". Used by the round-completion
   view, the reminder job and the digest email.
2. The share card and `Predictions.razor` - the stored name **only when the competition is a tournament**, otherwise
   "Round N".
3. `MyLeaguesTile.razor` - its own variant, already recorded as an open question.

Every round in the database is named, and league rounds are named "Gameweek 5". So form 1 renders "Gameweek 5" and form 2
renders "Round 5" **for the same round**: the share card and the prediction page header say "Round 5" while the digest email
and the round-completion view say "Gameweek 5". Not a bug either side of - both are defensible - but nobody chose it.

Left as it was and flagged, rather than unified: picking one changes wording on player-facing screens, which is the user's
call. What this PR did do is stop the share card and the razor being two implementations of form 2 - the handler now owns it
with the reasoning written down next to it.

### The prediction page and the share card were reading the same round twice

Both joined `[Rounds]`, `[Seasons]` and `[Competitions]` themselves and then reached the player's predictions through their
own join - the page through a left join to keep unpredicted fixtures on the form, the card through an inner join to drop
them. That difference is a rule about what each screen is for, and it was expressed as a join type.

They now share `IRoundHeaderQuery` and `IUserRoundPredictionsQuery`, and the fixtures come from `IRoundMatchesQuery`, which
already existed from the Admin/Rounds batch. Three ports, two screens, no SQL either side. The share card also gains the
postponed and placeholder exclusions as named rules rather than as the shape of its joins.

### A guard the compiler was already enforcing

One mutation could not be made to compile: removing the "no prediction means no row on the card" guard breaks nullable
analysis two statements later, where the outcome is read off the prediction. The invariant is held by the type system as
well as by the tests, which is the strongest form this can take. Recorded because a mutation that cannot compile proves
nothing on its own - it took a second edit, relaxing the later dereference too, before the mutation was real and the tests
caught it.

### A nullable column and a contract that denied it, twice more

`Leagues.EntryDeadlineUtc` allows null, and `LeagueDto.EntryDeadlineUtc` said it could not. Dapper throws rather than coerces
there, so the manage-leagues screen would have failed outright on the first league saved without a deadline - it has just never
happened. Making the contract honest cascaded into exactly one razor line, which now shows the editor a sensible placeholder.

Same shape as `Teams.LogoUrl` two batches earlier. Both were found by writing the row type honestly and letting the compiler
walk the consequences, which is the argument for doing the split at all.

### The prize fund, found for the third time

`SUM(Price * MemberCount + ISNULL(PrizeFundOverride, 0))` on the public homepage, which is `Domain.Services.PrizeFund.Total`
written out in SQL. The service was created when the same formula was found in the My Leagues tile and the available-leagues
list. Three copies, one of them on the least-watched page on the site.

### Two notification handlers, one statement each

"Your request to join was approved" and "somebody wants to join your league" both read a player and a season with an identical
`CROSS JOIN` between two unrelated tables - a way to fetch four columns in one trip. Both now use `ILeagueEmailRecipientQuery`,
and the comment explaining *why* the league itself is deliberately not read (its row may be locked by the in-flight join
transaction) now lives in one place instead of being paraphrased twice.

### Rules found duplicated so far

Each of these is one rule with two implementations and nothing linking them. The split is what
collapses each to one.

| Rule | Copies | Status |
|------|--------|--------|
| Predictable fixture | `GetRoundCompletionQueryHandler.PredictableMatchPredicate` + `ReminderService.GetUsersMissingPredictionsAsync` | **Fully collapsed 2026-08-10.** The rule was never absent from C#: `Match.AreTeamsConfirmed` and `Match.IsPredictionLocked` both existed and were tested, and both SQL copies said in their comments that they mirrored the latter. Only the three-way composition was missing, so each call site rewrote the whole thing in T-SQL. `Match.IsOpenForPrediction` is now that composition, and both call sites read through one `IRoundCompletionQuery`. |
| Round display name (`CASE WHEN LEN(LTRIM(RTRIM(DisplayName))) > 0 ...`) | The same two files | **Fully collapsed 2026-08-10** to `Round.GetDisplayNameOrDefault`. The second rule those two files duplicated. |
| Round outcome counts | `RoundRepository.UpdateRoundResultsAsync` MERGE + `GetActiveRoundsQueryHandler.cs:195` | Found 2026-08-10, **untested in either copy**. `RoundResults.ExactScoreCount` is stored and read by badges, digests, leaderboards, records and season recap, so the SQL is canonical and the C# is a live shadow of it. Collapses in phase "Rounds". |
| Boost secrecy | SQL only, but reads `GETUTCDATE()` instead of `IDateTimeProvider` | Integration tested 2026-08-10; becomes a clock-injected C# filter in phase "Boosts" |
| Player display name (`FirstName + ' ' + LEFT(LastName, 1)`) | **17 files originally, 2 left** | `Domain.Services.PlayerDisplayName` added 2026-08-10 with the Boosts batch, which adopted it. As of the Badges batch **two SQL copies remain**, in `PrizeEvaluationInputsReader` and `GetRoundDigestQueryHandler`, both in areas still to come. The C# version also drops the trailing space the SQL produced for an empty surname. |
| Where a player stands on the badges table | `GetBadgeLeaderboardQueryHandler` (C#, row-numbered) + `GetBadgesTileQueryHandler` (SQL, `COUNT(*) + 1`) | **Fully collapsed with the Badges batch.** The two disagreed on real data - joint players shared a position on the tile and did not on the page - and one of them also awarded first place to accounts that were not on the table. Both now read `BadgeStandings`. |
| Top scorer of a round in a league | `GetRoundDigestQueryHandler` SQL (`ORDER BY BoostedPoints DESC, FirstName`) vs `Ranking` everywhere else | **Collapsed with the digest batch.** The SQL tie-break could not separate two players sharing a first name, and disagreed with the full-name tie-break every other leaderboard uses, so a league's table and the email about it could name different winners. |
| Round display name (`Round.GetDisplayNameOrDefault`) | Two more reads found ignoring it | **Both fixed with the digest batch**, and `Round.DisplayNameOrDefault` added as the static form so a read holding columns has no excuse. Verified on dev that no round is currently unnamed, so this was a gap rather than a live fault. |
| Longest run of rounds with an exact score | Two gap-and-island SQL statements, one lifetime and one per season | **Fully collapsed** to `Domain.Services.Badges.Streak`. The two differed only in scope, which four CTEs of window functions made impossible to see. |

## Open question, not yet decided

**The write side.** The agreed mandate ("every rule to C#, no exceptions") was decided for reads.
Set-based writes mostly turn out to be mechanism under the decision procedure above, but the
`RoundResults` MERGE is a genuine rule and `UpsertBatchAsync` / `UpdateLeagueRoundBoostsAsync` need a
call each. Decide per case, with measured row counts, when the relevant feature area comes up.
