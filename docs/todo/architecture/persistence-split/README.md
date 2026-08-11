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

### Rules found duplicated so far

Each of these is one rule with two implementations and nothing linking them. The split is what
collapses each to one.

| Rule | Copies | Status |
|------|--------|--------|
| Predictable fixture | `GetRoundCompletionQueryHandler.PredictableMatchPredicate` + `ReminderService.GetUsersMissingPredictionsAsync` | **Fully collapsed 2026-08-10.** The rule was never absent from C#: `Match.AreTeamsConfirmed` and `Match.IsPredictionLocked` both existed and were tested, and both SQL copies said in their comments that they mirrored the latter. Only the three-way composition was missing, so each call site rewrote the whole thing in T-SQL. `Match.IsOpenForPrediction` is now that composition, and both call sites read through one `IRoundCompletionQuery`. |
| Round display name (`CASE WHEN LEN(LTRIM(RTRIM(DisplayName))) > 0 ...`) | The same two files | **Fully collapsed 2026-08-10** to `Round.GetDisplayNameOrDefault`. The second rule those two files duplicated. |
| Round outcome counts | `RoundRepository.UpdateRoundResultsAsync` MERGE + `GetActiveRoundsQueryHandler.cs:195` | Found 2026-08-10, **untested in either copy**. `RoundResults.ExactScoreCount` is stored and read by badges, digests, leaderboards, records and season recap, so the SQL is canonical and the C# is a live shadow of it. Collapses in phase "Rounds". |
| Boost secrecy | SQL only, but reads `GETUTCDATE()` instead of `IDateTimeProvider` | Integration tested 2026-08-10; becomes a clock-injected C# filter in phase "Boosts" |
| Player display name (`FirstName + ' ' + LEFT(LastName, 1)`) | **17 files originally, 14 left** | `Domain.Services.PlayerDisplayName` added 2026-08-10 with the Boosts batch, which adopted it. **16 SQL copies remain** - each area adopts it as the split reaches it. The C# version also drops the trailing space the SQL produced for an empty surname. |

## Open question, not yet decided

**The write side.** The agreed mandate ("every rule to C#, no exceptions") was decided for reads.
Set-based writes mostly turn out to be mechanism under the decision procedure above, but the
`RoundResults` MERGE is a genuine rule and `UpsertBatchAsync` / `UpdateLeagueRoundBoostsAsync` need a
call each. Decide per case, with measured row counts, when the relevant feature area comes up.
