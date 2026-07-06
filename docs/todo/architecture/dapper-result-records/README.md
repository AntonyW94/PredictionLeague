# Dapper Query Handlers Materialise Into Private Result Records

## Status

**Not Started** | In Progress | Complete

## Priority

**Medium-High** - The root CLAUDE.md "Dapper Result Mapping" section documents that Dapper matches a positional record's constructor to the SELECT column list positionally (by name AND type per position), and that a mismatch compiles, passes unit tests, and throws `InvalidOperationException` only at runtime. Around 19 query handlers already contain the coupling with a co-located `private record XxxQueryResult(...)`, but roughly 30 more materialise Dapper rows DIRECTLY into `ThePredictions.Contracts` DTO constructors. That extends the fragile positional coupling into the shared Contracts assembly: reordering, retyping, or inserting a parameter in a Contracts record for a purely UI-facing reason breaks a query at runtime with no compile error and no failing test. Converting them confines the fragility to one file per query, next to its SQL.

## Summary

Convert every query that materialises Dapper rows directly into a Contracts type so that:

1. The generic argument to `QueryAsync<T>` / `QuerySingleOrDefaultAsync<T>` is a `private record XxxQueryResult(...)` co-located in the handler, matching the SELECT column order, carrying the standard ordering comment.
2. The handler then maps the result record to the Contracts DTO **by name** (explicit constructor call / object initialiser), so Contracts shape changes become compile errors in the handler instead of runtime materialisation failures.
3. Behaviour is identical; no SQL changes, no DTO changes.

Finish by appending the rule to CLAUDE.md (exact wording at the end).

## Background: the two Dapper mapping modes (matters for classification)

- **Positional constructor mapping** - used for types with no parameterless constructor (all positional `record` types). Constructor parameter N must match SELECT column N by name and type. Mismatch throws at runtime: `InvalidOperationException: A parameterless default constructor or one matching signature (...) is required for <Handler>+<Result> materialization`.
- **Name-based property mapping** - used for classes (and records) with a parameterless constructor and settable/init properties. Columns are matched to properties by name; order is irrelevant. The failure mode is different and WORSE in one way: an unmatched column or renamed property fails **silently**, leaving the property at its default value.

Both modes couple the query shape to the target type, so both directions get converted; only the risk ranking differs.

Also relevant: `src/ThePredictions.Application/Data/IApplicationReadDbConnection.cs` exposes exactly two methods, so the enumeration below is complete when those two are covered:

```csharp
Task<IEnumerable<T>> QueryAsync<T>(string sql, CancellationToken cancellationToken, object? param = null);
Task<T?> QuerySingleOrDefaultAsync<T>(string sql, CancellationToken cancellationToken, object? param = null);
```

## Authoritative enumeration method

Re-run this before starting (the inventory below is a snapshot from 2026-07-06 and the codebase moves):

```
rg -n "QueryAsync<|QuerySingleOrDefaultAsync<" src/ThePredictions.Application --type cs
```

Then classify each hit by its generic type argument:

1. **Scalar / tuple** (`int`, `int?`, `bool`, `string`, named tuples) - leave as is.
2. **Application-owned type** (declared anywhere under `src/ThePredictions.Application`) - already safe; the coupling is local. Find the declaration with `rg "record <TypeName>|class <TypeName>" src/ThePredictions.Application --type cs`.
3. **Contracts type** (declared under `src/ThePredictions.Contracts`) - convert. Sub-classify by declaration: positional `record Xxx(` (constructor mapping, priority) vs `class` with properties (name mapping).

Repositories in `src/ThePredictions.Infrastructure` also use Dapper, but commands/repositories are out of scope here (they largely hydrate Domain entities via constructors, a deliberate pattern per `docs/guides/domain-models.md`).

## Checked inventory (verified 2026-07-06)

### A. Already safe - no change (uses an Application-owned result type)

| Handler / service | Result type(s) |
|---|---|
| `Features/Account/Queries/GetMyPayoutDetailsQueryHandler.cs` | `PayoutDetailsRow` (private sealed record) + `string` scalar |
| `Common/Prizes/PrizeEvaluationInputsReader.cs` | `LeagueRow`, `SchemeRow`, `EntryRow` (private sealed classes) |
| `Services/SeasonPriceRecommendationService.cs` | `CostRow` (private sealed class) + Domain models `PricingSettings`, `ServiceFee` + `int` scalars |
| `Features/Predictions/Queries/GetPredictionPageDataQueryHandler.cs` | `PredictionPageQueryResult`, `PredictionLeagueQueryResult`, `UserBoostUsageResult` |
| `Features/Boosts/Queries/GetLeagueBoostUsageSummaryQueryHandler.cs` | `BoostRuleRow`, `WindowRow`, `MemberRow`, `SeasonInfoRow`, `UsageRow`, `RoundRangeRow` (private sealed classes) + `int?` scalars |
| `Features/External/Tasks/Queries/GetLeagueWelcomeBatchQueryHandler.cs` | `LeagueRecipientRow`, `PrizeRow`, `BoostRow`, `WindowRow` |
| `Features/Admin/Users/Queries/GetAllUsersQueryHandler.cs` | `UserQueryResult` (carries the canonical ordering comment, lines 119-122) |
| `Features/Onboarding/Queries/GetOnboardingChecklistQueryHandler.cs` | `OnboardingUserState` (Application-owned public record) + `string` |
| `Features/Dashboard/Queries/GetActiveRoundsQueryHandler.cs` | `ActiveRoundQueryResult`, `ActiveRoundMatchQueryResult` (lines 189-217) |
| `Features/Dashboard/Queries/GetLeaderboardsQueryHandler.cs` | `FlatLeaderboardEntry` (private record with properties) |
| `Features/Admin/Rounds/Queries/GetRoundByIdQueryHandler.cs` | `RoundQueryResult` |
| `Features/Admin/Rounds/Queries/FetchRoundsForSeasonQueryHandler.cs` | `RoundQueryResult` |
| `Features/Admin/Rounds/Queries/GetRoundDigestQueryHandler.cs` | `RoundDigestRow` (Application-owned public record) |
| `Features/Admin/Rounds/Queries/GetPrizeWinnersForRoundQueryHandler.cs` | `PrizeWinnerRow` (Application-owned public record) |
| `Features/Admin/EmailTests/Queries/GetEmailTestDefaultsQueryHandler.cs` | `EmailTestUserData` (Application-owned public record) |
| `Features/Leagues/Queries/FetchLeagueMembersQueryHandler.cs` | `MemberQueryResult` + `string` |
| `Features/Leagues/Queries/GetLeagueBankDetailsQueryHandler.cs` | `BankDetailsRow` |
| `Features/Leagues/Queries/GetLeagueDashboardRoundResultsQueryHandler.cs` | `PredictionQueryResult` |
| `Features/Leagues/Queries/GetLeaguePaymentInfoQueryHandler.cs` | `PaymentInfoRow` |
| `Features/Leagues/Queries/GetLeaguePayoutsQueryHandler.cs` | `LeagueRow`, `WinningRow`, `StoredPayoutRow`, `PayoutDetailRow` |
| `Features/Leagues/Queries/GetLeaguePrizesPageQueryHandler.cs` | `PrizesQueryResult` |
| `Features/Leagues/Queries/GetManageLeaguesQueryHandler.cs` | `LeagueWithCategory` |
| `Features/Leagues/Queries/GetMonthsForLeagueQueryHandler.cs` | `MonthRow` |
| `Features/Leagues/Queries/GetSeasonRecapQueryHandler.cs` | `SeasonRecapQueryResult`, `PositionResult` |
| `Features/Leagues/Queries/GetStagesForLeagueQueryHandler.cs` | `StageRow` |
| `Features/Leagues/Queries/GetWinningsQueryHandler.cs` | `LeagueData` (private class), `PrizeSettingQueryResult`, `WinningsQueryResult`, `LeagueMemberQueryResult` |
| `Features/Prizes/Queries/EvaluateSchemeQueryHandler.cs` | `SeasonRow` (private sealed class) |
| `Features/Leagues/Commands/NotifyLeagueAdminOfJoinRequestCommandHandler.cs` | `LeagueAdminDto` (Application-owned, declared in the same file - despite the Dto suffix it is NOT a Contracts type) |
| `Features/Leagues/Commands/NotifyMemberOfLeagueApprovalCommandHandler.cs` | `LeagueMemberContactDto` (same note) |

(All paths in this table are under `src/ThePredictions.Application/`.)

### B. Convert - direct into Contracts POSITIONAL RECORDS (priority: fragile positional mapping)

All under `src/ThePredictions.Application/`; line numbers are the Dapper call sites.

| # | Handler | Line(s) | Contracts type |
|---|---|---|---|
| 1 | `Features/Account/Queries/GetUserQueryHandler.cs` | 21 | `UserDetails` (worked example below) |
| 2 | `Features/Homepage/Queries/GetHomepageSeasonsQueryHandler.cs` | 66 | `HomepageSeasonDto` |
| 3 | `Features/SeasonPasses/Queries/GetSeasonTeamsQueryHandler.cs` | 29 | `SeasonTeamDto` |
| 4 | `Features/SeasonPasses/Queries/GetSeasonPassOptionsQueryHandler.cs` | 45 | `SeasonPassOptionsDto` |
| 5 | `Features/SeasonPasses/Queries/GetAvailableSeasonPassesQueryHandler.cs` | 56 | `AvailableSeasonPassDto` |
| 6 | `Features/SeasonPasses/Queries/GetMySeasonPassesQueryHandler.cs` | 34 | `MySeasonPassDto` |
| 7 | `Features/SeasonPasses/Queries/GetPastSeasonPassesQueryHandler.cs` | 51 | `PastSeasonPassDto` |
| 8 | `Features/Dashboard/Queries/GetPendingRequestsQueryHandler.cs` | 43 | `LeagueRequestDto` |
| 9 | `Features/Dashboard/Queries/GetPendingMembersForAdminQueryHandler.cs` | 33, 72 | `AdminLeagueSummaryDto`, `PendingLeagueMemberDto` |
| 10 | `Features/Dashboard/Queries/GetMyLeaguesQueryHandler.cs` | 318 | `MyLeagueDto` |
| 11 | `Features/Dashboard/Queries/GetMatchesForRoundQueryHandler.cs` | 45 | `MatchInRoundDto` |
| 12 | `Features/Dashboard/Queries/GetAvailableLeaguesQueryHandler.cs` | 44 | `AvailableLeagueDto` |
| 13 | `Features/Admin/Teams/Queries/GetTeamByIdQueryHandler.cs` | 23 | `TeamDto` |
| 14 | `Features/Admin/Teams/Queries/FetchAllTeamsQueryHandler.cs` | 33, 47 | `TeamDto` (two SQL strings, one shared result record) |
| 15 | `Features/Admin/EmailSettings/Queries/GetEmailSettingsQueryHandler.cs` | 21 | `EmailSettingsDto` |
| 16 | `Features/Admin/Competitions/Queries/GetCompetitionByIdQueryHandler.cs` | 27 | `CompetitionDto` |
| 17 | `Features/Admin/Competitions/Queries/FetchAllCompetitionsQueryHandler.cs` | 27 | `CompetitionDto` |
| 18 | `Features/Admin/ServiceFees/Queries/GetServiceFeesQueryHandler.cs` | 22 | `ServiceFeeDto` |
| 19 | `Features/Admin/PricingSettings/Queries/GetPricingSettingsQueryHandler.cs` | 22 | `PricingSettingsDto` |
| 20 | `Features/Admin/RunningCosts/Queries/GetRunningCostsQueryHandler.cs` | 26 | `RunningCostDto` |
| 21 | `Features/Admin/Seasons/Queries/FetchAllSeasonsQueryHandler.cs` | 56 | `SeasonDto` |
| 22 | `Features/Admin/Seasons/Queries/GetSeasonByIdQueryHandler.cs` | 56 | `SeasonDto` |
| 23 | `Features/Leagues/Queries/FetchAllLeaguesQueryHandler.cs` | 42 | `LeagueDto` |
| 24 | `Features/Leagues/Queries/GetLeagueByIdQueryHandler.cs` | 56 | `LeagueDto` |
| 25 | `Features/Leagues/Queries/GetCreateLeaguePageDataQueryHandler.cs` | 24 | `SeasonLookupDto` |
| 26 | `Features/Leagues/Queries/GetLeagueDashboardQueryHandler.cs` | 86, 104 | `RoundDto`, `LeagueDashboardMemberDto` (the `bool` at line 19 and the named tuple at line 53 stay as they are) |
| 27 | `Features/Leagues/Queries/GetLeagueRoundsForDashboardQueryHandler.cs` | 44 | `RoundDto` |

### C. Convert - direct into Contracts CLASSES (name-based mapping; silent-default failure mode, lower urgency)

| # | Handler | Line | Contracts type |
|---|---|---|---|
| 28 | `Features/Leagues/Queries/GetOverallLeaderboardQueryHandler.cs` | 60 | `LeaderboardEntryDto` |
| 29 | `Features/Leagues/Queries/GetMonthlyLeaderboardQueryHandler.cs` | 76 | `LeaderboardEntryDto` |
| 30 | `Features/Leagues/Queries/GetStageLeaderboardQueryHandler.cs` | 78 | `LeaderboardEntryDto` |
| 31 | `Features/Leagues/Queries/GetExactScoresLeaderboardQueryHandler.cs` | 46 | `ExactScoresLeaderboardEntryDto` |
| 32 | `Features/Boosts/Queries/GetBoostCatalogueQueryHandler.cs` | 26 | `BoostCatalogueItemDto` |
| 33 | `Features/Leagues/Queries/GetLeagueRecordsQueryHandler.cs` | 201 | `LeagueRecordsDto` |

Note on group C: these Contracts types are `class`es with init/settable properties, so Dapper maps them by column NAME, not position. They are not exposed to the reorder-throws-at-runtime failure, but a property rename or column-alias drift fails silently (default values). Convert them with the same recipe; the private record still maps positionally to the SELECT (so give it the ordering comment), and the mapping step constructs the class with an object initialiser, which makes any Contracts rename a compile error in the handler.

**Discrepancy found while verifying:** the original brief cited `GetOverallLeaderboardQueryHandler` into `LeaderboardEntryDto` as the example of positional-constructor fragility. `LeaderboardEntryDto` (`src/ThePredictions.Contracts/Leaderboards/LeaderboardEntryDto.cs`) is in fact a property-mapped class, so that specific handler is in group C, not group B. The positional risk is real for the 27 group B call sites, which all target positional Contracts records (verified via `rg "public (sealed )?record \w+\(" src/ThePredictions.Contracts`).

### D. Leave alone - scalars, tuples, Domain reads

- `Features/Dashboard/Queries/CheckForAvailablePrivateLeaguesQueryHandler.cs` line 26 (`bool`)
- `Features/Leagues/Queries/GetLeagueDashboardQueryHandler.cs` lines 19 (`bool`), 53 (named tuple - the tuple is positional too but private to the handler, which is exactly what this plan wants; optionally convert to a record for style, not required)
- `Features/Onboarding/Queries/GetOnboardingChecklistQueryHandler.cs` line 29 (`string`)
- `Features/Account/Queries/GetMyPayoutDetailsQueryHandler.cs` line 46 (`string`)
- `Features/Leagues/Queries/FetchLeagueMembersQueryHandler.cs` line 59 (`string`)
- `Services/SeasonPriceRecommendationService.cs` lines 56, 75 read Domain models (`PricingSettings`, `ServiceFee`) via Dapper, plus `int` scalars at 123, 160. Reading Domain models on the query side is a separate layering question; do NOT change it in this plan, just note it.

## Conversion recipe

For each handler in groups B and C, in this exact order:

1. **Read the SELECT.** Write down the output columns top to bottom, including every aliased computed column (`... AS X` counts as a column at its position).
2. **Compare with the current target type** (constructor parameters for records, properties for classes). If the SELECT order and a positional record's constructor DISAGREE in order or type, **stop: that is a latent runtime bug**. Do not silently fix it. Record the handler, the mismatch, and whether the code path can currently execute, and report all such findings in the PR description (and to the user). Then make the private record match the SELECT (the truth is the SQL) and map to the DTO by name, which fixes the materialisation without guessing intent.
3. **Add the private record** at the bottom of the handler class, named `<Thing>QueryResult` (matching the existing 19), parameters in the exact SELECT column order with exact column names and compatible types. Copy nullability from what the SQL can return, not from the DTO.
4. **Add the standard ordering comment** above the record. The canonical original is on `UserQueryResult` in `src/ThePredictions.Application/Features/Admin/Users/Queries/GetAllUsersQueryHandler.cs` lines 119-122 (note: the original uses an em dash after "POSITIONALLY"; write new copies with a plain hyphen per the repo writing rules):

```csharp
// NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
// parameter N must line up with SELECT column N (by name and type). Keep the order of
// these parameters identical to the SELECT column order above, or materialisation throws
// at runtime ("A parameterless default constructor or one matching signature ... is required").
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
private record XxxQueryResult(
    ...);
```

(The `[SuppressMessage]` needs `using System.Diagnostics.CodeAnalysis;`.)

5. **Switch the generic argument** of the Dapper call to the new record.
6. **Map by name** to the Contracts DTO: for records, an explicit constructor call with named arguments implied by order-free property reads (`new SomeDto(row.A, row.B, ...)` reading each record property by name); for classes, an object initialiser. Preserve `null` handling for `QuerySingleOrDefaultAsync` (return `null` when the row is `null`).
7. **Do not touch the SQL or the Contracts type.** Behaviour must be identical, including quirks (e.g. `HomepageSeasonDto.IsInProgress`/`IsUpcoming` are `int` flags; mirror `int` in the result record and pass through).

### Worked example, end to end: GetUserQueryHandler

Current code (`src/ThePredictions.Application/Features/Account/Queries/GetUserQueryHandler.cs`, verified; SELECT and `UserDetails` are currently ALIGNED, no latent bug here):

```csharp
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Account;

namespace ThePredictions.Application.Features.Account.Queries;

public class GetUserQueryHandler(IApplicationReadDbConnection dbConnection) : IRequestHandler<GetUserQuery, UserDetails?>
{
    public async Task<UserDetails?> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                [FirstName],
                [LastName],
                [Email],
                [PhoneNumber],
                [PreferredTheme]
            FROM [AspNetUsers]
            WHERE [Id] = @UserId;";

        return await dbConnection.QuerySingleOrDefaultAsync<UserDetails>(sql, cancellationToken, new { request.UserId });
    }
}
```

`UserDetails` (`src/ThePredictions.Contracts/Account/UserDetails.cs`) is the positional record `UserDetails(string FirstName, string LastName, string Email, string? PhoneNumber, string PreferredTheme)`.

Target code:

```csharp
using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Account;

namespace ThePredictions.Application.Features.Account.Queries;

public class GetUserQueryHandler(IApplicationReadDbConnection dbConnection) : IRequestHandler<GetUserQuery, UserDetails?>
{
    public async Task<UserDetails?> Handle(GetUserQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                [FirstName],
                [LastName],
                [Email],
                [PhoneNumber],
                [PreferredTheme]
            FROM [AspNetUsers]
            WHERE [Id] = @UserId;";

        var user = await dbConnection.QuerySingleOrDefaultAsync<UserQueryResult>(sql, cancellationToken, new { request.UserId });

        return user is null
            ? null
            : new UserDetails(
                user.FirstName,
                user.LastName,
                user.Email,
                user.PhoneNumber,
                user.PreferredTheme);
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record UserQueryResult(
        string FirstName,
        string LastName,
        string Email,
        string? PhoneNumber,
        string PreferredTheme);
}
```

The SQL is untouched; the DTO is untouched; the positional coupling now lives entirely inside the handler file, and any future `UserDetails` reshaping is a compile error at the `new UserDetails(...)` call.

## Work batches (each independently buildable and committable)

Do the batches in this order. Within each batch, apply the recipe per handler and note any step 2 mismatch findings. Spot-checks already done during planning: `GetUserQueryHandler` and `GetHomepageSeasonsQueryHandler` are aligned. The remaining handlers were NOT individually diffed; because a positional mismatch throws whenever the query runs, live paths are probably aligned, so pay closest attention to rarely-exercised handlers (admin screens, by-id lookups).

1. **Batch 1 - worked example plus Homepage:** B1, B2.
2. **Batch 2 - SeasonPasses:** B3-B7.
3. **Batch 3 - Dashboard:** B8-B12.
4. **Batch 4 - Admin:** B13-B22.
5. **Batch 5 - Leagues:** B23-B27.
6. **Batch 6 - Contracts classes (group C):** C28-C33. For `LeaderboardEntryDto` targets, one private record per handler (their SELECTs differ); map with an object initialiser, e.g. `new LeaderboardEntryDto { Rank = row.Rank, PlayerName = row.PlayerName, TotalPoints = row.TotalPoints, UserId = row.UserId, SnapshotRank = row.SnapshotRank, IsRoundInProgress = row.IsRoundInProgress }`. Watch types here: the class declares `long Rank` (SQL `RANK()` returns `bigint`) and `IsRoundInProgress` is produced by a `CASE ... THEN 1 ELSE 0` (an `int` column mapped onto a `bool` property today; in the private record, type the parameter to match the column, e.g. `int IsRoundInProgress`, and convert with `row.IsRoundInProgress == 1` in the initialiser - verify against the actual column output when converting, and if Dapper's current implicit conversion behaviour is ambiguous, prefer `CAST(... AS bit)` equivalence checks in a quick manual run rather than changing the SQL).
7. **Batch 7 - CLAUDE.md rule** (below).

## CLAUDE.md addition (final step)

Append the following to the **Dapper Result Mapping** section of the root `CLAUDE.md` (after the existing computed-columns paragraph), and add a matching bullet to `docs/guides/database.md` if that guide repeats the Dapper rules:

```markdown
**Query handlers materialise into private result records, never directly into Contracts DTOs.** The generic argument to `QueryAsync<T>` / `QuerySingleOrDefaultAsync<T>` must be a `private record XxxQueryResult(...)` co-located in the handler (or another Application-owned row type), kept in lockstep with the SELECT column order and carrying the standard ordering comment. Map from the result record to the outward Contracts DTO by name afterwards. Scalars (`int`, `bool`, `string`) are exempt. This keeps the fragile positional coupling inside a single file, next to its SQL, instead of extending it into the shared Contracts assembly where a UI-motivated constructor reorder would break queries at runtime.
```

## Out of scope

- Adding tests for these query handlers. Most have none today (verified for the group B handlers); that gap belongs to `docs/todo/architecture/test-suite/README.md`. This plan relies on the compiler plus the existing test suite staying green.
- Changing any SQL, any Contracts DTO shape, or any API/Blazor consumer.
- Silently fixing latent SELECT/constructor mismatches found during step 2 of the recipe - they must be reported; the conversion makes the record match the SQL, and any intended-behaviour question goes back to the user.
- Repositories and command handlers (Infrastructure Dapper usage hydrating Domain entities).
- `SeasonPriceRecommendationService` reading Domain models via Dapper (noted in group D; separate layering discussion).
- Converting the named tuple in `GetLeagueDashboardQueryHandler` line 53 (optional style improvement only).

## Verification checklist

Run after EACH batch:

- [ ] `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true` succeeds with zero warnings.
- [ ] `dotnet test tests/Unit/ThePredictions.Application.Tests.Unit` passes.
- [ ] `dotnet test tests/Unit/ThePredictions.Domain.Tests.Unit` and `dotnet test tests/Unit/ThePredictions.Composition.Tests.Unit` pass (nothing in them should change; this catches accidental edits).
- [ ] Re-run the enumeration grep; confirm every remaining `QueryAsync<`/`QuerySingleOrDefaultAsync<` generic argument in `src/ThePredictions.Application` is a scalar, tuple, or Application-owned type (no `ThePredictions.Contracts` types remain once all batches land).
- [ ] Every new private record carries the ordering comment and `[SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]`.
- [ ] Any SELECT/constructor mismatches found in step 2 are listed in the PR description.
- [ ] Final batch only: CLAUDE.md contains the new rule under "Dapper Result Mapping".
