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
| 0c | **Identity stores and SQL-bearing services** | `DapperUserStore`, `DapperRoleStore`, `LeagueMembershipService`, `CachedEmailSettingsProvider`, and `Dapper` finally leaves Infrastructure. |
| 0d | **Migration set** | Move the DbUp scripts under the adapter. Touches the root `CLAUDE.md` rule, the migrations README, `DatabaseTools` and the integration project's glob, so it is kept separate. |
| 1 | **Conformance split** | Extract the abstract bases, `ITestDataSeeder`, and re-home the existing 29 integration tests. No new tests. |
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

Feature areas for phases 2..N, roughly worst-first by rule density: Boosts, Rounds, Leaderboards,
Leagues, Dashboard, Badges, Admin/Rounds, Admin/Seasons, Predictions, Authentication, Account,
Payouts, External/Tasks.

### Rules found duplicated so far

Each of these is one rule with two implementations and nothing linking them. The split is what
collapses each to one.

| Rule | Copies | Status |
|------|--------|--------|
| Predictable fixture | `GetRoundCompletionQueryHandler.PredictableMatchPredicate` + `ReminderService.GetUsersMissingPredictionsAsync` | Integration tested 2026-08-10; collapses to one C# rule in phase "Rounds" |
| Round outcome counts | `RoundRepository.UpdateRoundResultsAsync` MERGE + `GetActiveRoundsQueryHandler.cs:195` | Found 2026-08-10, **untested in either copy**. `RoundResults.ExactScoreCount` is stored and read by badges, digests, leaderboards, records and season recap, so the SQL is canonical and the C# is a live shadow of it. Collapses in phase "Rounds". |
| Boost secrecy | SQL only, but reads `GETUTCDATE()` instead of `IDateTimeProvider` | Integration tested 2026-08-10; becomes a clock-injected C# filter in phase "Boosts" |

## Open question, not yet decided

**The write side.** The agreed mandate ("every rule to C#, no exceptions") was decided for reads.
Set-based writes mostly turn out to be mechanism under the decision procedure above, but the
`RoundResults` MERGE is a genuine rule and `UpsertBatchAsync` / `UpdateLeagueRoundBoostsAsync` need a
call each. Decide per case, with measured row counts, when the relevant feature area comes up.
