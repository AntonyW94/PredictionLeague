# Handler Domain Logic Extraction

## Status

**Not Started** | In Progress | Complete

## Priority

**Medium** - Four Application command handlers contain business rules (knockout stage sizes, the knockout 90-minute scoring rule, the round lifecycle state machine, and round-window scheduling policy) that belong in the Domain project, where they would be covered by the mandatory 100% line/branch coverage gate. None of these are bugs; this is a structural refactor that reduces the risk of future regressions in un-covered Application code and removes a known duplication (June 2026 audit item 4.7, last bullet).

## The template to copy

The pattern for this refactor already exists in the codebase and must be imitated:

- Handler: `src\ThePredictions.Application\Features\Predictions\Commands\SubmitPredictionsCommandHandler.cs` (26 lines). It loads the aggregate, guards existence, delegates every decision to the domain service, and persists the result:

  ```csharp
  var round = await roundRepository.GetByIdAsync(request.RoundId, cancellationToken);

  Guard.Against.EntityNotFound(request.RoundId, round, "Round");

  var predictedScores = request.Predictions.Select(p => (p.MatchId, p.HomeScore, p.AwayScore));

  var predictions = predictionDomainService.SubmitPredictions(
      round,
      request.UserId,
      predictedScores);

  await userPredictionRepository.UpsertBatchAsync(predictions, cancellationToken);
  ```

- Domain service: `src\ThePredictions.Domain\Services\PredictionDomainService.cs`. It takes domain types and primitives, uses `IDateTimeProvider` (never `DateTime.UtcNow`), and returns decisions for the handler to persist.
- Tests: `tests\Unit\ThePredictions.Domain.Tests.Unit\Services\PredictionDomainServiceTests.cs`. Entities are built via the full public constructor with explicit IDs; `TestDateTimeProvider` (from `tests\Shared\ThePredictions.Tests.Shared\Helpers\TestDateTimeProvider.cs`) supplies time.

DI note: `PredictionDomainService` is registered in `src\ThePredictions.Infrastructure\DependencyInjection.cs` (line 125, `services.AddScoped<PredictionDomainService>();`) because it depends on `IDateTimeProvider`. Every new type in this plan is pure and deterministic with no dependencies, so they are all **static classes or entity methods and need no DI registration** (same convention as the existing static `ThePredictions.Domain.Common.TournamentRoundNameParser`).

## Ground rules for every part

1. **Domain coverage gate.** The Domain project must keep 100% line AND branch coverage. Every part below lists the tests required to hold that. After each part, run `tools\Test Coverage\coverage-unit.bat` and confirm the report shows 100%/100% for `ThePredictions.Domain`.
2. **Test conventions.** xUnit v3 + FluentAssertions; names `MethodName_ShouldX_WhenY`; entities in tests built with the full public constructor and explicit IDs (never `Entity.Create(...)` unless testing the factory); `CancellationToken.None`, never a bare `default` (xUnit1051 fails CI under `TreatWarningsAsErrors`).
3. **No behaviour change.** Each part is a pure extraction. Where a micro-behaviour question arises (noted inline), the decision is recorded here so the implementer does not need to ask.
4. **One public type per file, UK English identifiers, plain hyphens in comments** (no em dashes in newly authored text).
5. **Data-only records** added to Domain get `[ExcludeFromCodeCoverage]` (they have no logic; compiler-generated members would otherwise drag coverage below 100%).
6. **Build gate.** After each part: `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true` must be clean.

Implement the parts in the order below (smallest first). Each part is independently shippable as its own commit/PR.

---

## Part 1 - Knockout stage sizes out of CreateSeasonCommandHandler

### Current code

`src\ThePredictions.Application\Features\Admin\Seasons\Commands\CreateSeasonCommandHandler.cs`, lines 163-187:

```csharp
private static TournamentStage GetStageForMatchIndex(List<TournamentStage> stages, int matchIndex, int totalMatches)
{
    // For combined knockout rounds, distribute matches across stages
    // using known knockout stage sizes (SF=2, ThirdPlace=1, Final=1)
    var cumulative = 0;
    foreach (var stage in stages)
    {
        var stageSize = stage switch
        {
            TournamentStage.SemiFinals => 2,
            TournamentStage.ThirdPlace => 1,
            TournamentStage.Final => 1,
            TournamentStage.QuarterFinals => 4,
            TournamentStage.RoundOf16 => 8,
            TournamentStage.RoundOf32 => 16,
            _ => totalMatches / stages.Count
        };

        cumulative += stageSize;
        if (matchIndex < cumulative)
            return stage;
    }

    return stages[^1];
}
```

Called from lines 146-148 inside `SaveTournamentMappingsAndCreatePlaceholderRoundsAsync`:

```csharp
var stage = stages.Count == 1
    ? stages[0]
    : GetStageForMatchIndex(stages, i, mapping.ExpectedMatchCount);
```

### Where it goes

The tournament/stage concept already lives in Domain: `TournamentStage` enum (`src\ThePredictions.Domain\Common\Enumerations\TournamentStage.cs`) and the static `TournamentRoundNameParser` (`src\ThePredictions.Domain\Common\TournamentRoundNameParser.cs`), which **already encodes the same knockout stage sizes** in `CalculateExpectedMatchCount` (R32=16, R16=8, QF=4, SF=2, ThirdPlace=1, Final=1). Add the new method to `TournamentRoundNameParser` rather than creating a new type.

Deliberate decision: do NOT merge the new method with `CalculateExpectedMatchCount`. The group-stage sizing semantics differ (`CalculateExpectedMatchCount` uses `totalTeams / 2`; the allocation fallback uses `totalMatches / stages.Count`). Port the switch verbatim.

### Steps

1. Add to `src\ThePredictions.Domain\Common\TournamentRoundNameParser.cs` (the file already has `using ThePredictions.Domain.Common.Enumerations;`; add `using Ardalis.GuardClauses;`):

   ```csharp
   /// <summary>
   /// For combined knockout rounds, distributes match indexes across stages in order,
   /// using known knockout stage sizes (R32=16, R16=8, QF=4, SF=2, ThirdPlace=1, Final=1).
   /// Group stages fall back to an even split of <paramref name="totalMatches"/>.
   /// </summary>
   public static TournamentStage GetStageForMatchIndex(IReadOnlyList<TournamentStage> stages, int matchIndex, int totalMatches)
   {
       Guard.Against.NullOrEmpty(stages, message: "At least one stage is required to allocate a match index.");

       var cumulative = 0;
       foreach (var stage in stages)
       {
           var stageSize = stage switch
           {
               TournamentStage.SemiFinals => 2,
               TournamentStage.ThirdPlace => 1,
               TournamentStage.Final => 1,
               TournamentStage.QuarterFinals => 4,
               TournamentStage.RoundOf16 => 8,
               TournamentStage.RoundOf32 => 16,
               _ => totalMatches / stages.Count
           };

           cumulative += stageSize;
           if (matchIndex < cumulative)
               return stage;
       }

       return stages[^1];
   }
   ```

   Note the guard is a deliberate (and safe) hardening: the old private method would have thrown `IndexOutOfRangeException` on an empty list; the call site can only pass an empty list if `TournamentRoundMapping.Stages` fails to parse, which the entity factory guards against in practice.

2. In `CreateSeasonCommandHandler.cs`: replace lines 146-148 with a direct call (the single-stage short-circuit is redundant; the domain method returns `stages[0]` for any single-stage list because either the stage's fixed size or the `totalMatches / 1` fallback always exceeds every valid index, and the trailing `stages[^1]` catches the rest):

   ```csharp
   var stage = TournamentRoundNameParser.GetStageForMatchIndex(stages, i, mapping.ExpectedMatchCount);
   ```

   Then delete the private `GetStageForMatchIndex` method (lines 163-187). `TournamentRoundNameParser` is already in scope via `using ThePredictions.Domain.Common;`.

### Tests (add to `tests\Unit\ThePredictions.Domain.Tests.Unit\Common\TournamentRoundNameParserTests.cs`, new region `GetStageForMatchIndex`)

Branch inventory: the empty-list guard, each of the 7 switch arms, the `matchIndex < cumulative` true/false, and the trailing fallback return.

- `GetStageForMatchIndex_ShouldThrow_WhenStagesEmpty` - `Array.Empty<TournamentStage>()`, expect `ArgumentException`
- `GetStageForMatchIndex_ShouldReturnSemiFinals_WhenIndexWithinFirstTwoOfSemiFinalPlusFinal` - stages `[SemiFinals, Final]`, totalMatches 3, indexes 0 and 1
- `GetStageForMatchIndex_ShouldReturnFinal_WhenIndexAfterSemiFinals` - stages `[SemiFinals, Final]`, totalMatches 3, index 2
- `GetStageForMatchIndex_ShouldReturnThirdPlace_WhenIndexInThirdPlaceSlot` - stages `[SemiFinals, ThirdPlace, Final]`, totalMatches 4, index 2 (and index 3 returns `Final`)
- `GetStageForMatchIndex_ShouldReturnQuarterFinals_WhenIndexWithinFirstFour` - stages `[QuarterFinals, SemiFinals]`, totalMatches 6, index 3 -> `QuarterFinals`, index 4 -> `SemiFinals`
- `GetStageForMatchIndex_ShouldReturnRoundOf16_WhenIndexWithinFirstEight` - stages `[RoundOf16, QuarterFinals]`, totalMatches 12, index 7 -> `RoundOf16`, index 8 -> `QuarterFinals`
- `GetStageForMatchIndex_ShouldReturnRoundOf32_WhenIndexWithinFirstSixteen` - stages `[RoundOf32, RoundOf16]`, totalMatches 24, index 15 -> `RoundOf32`, index 16 -> `RoundOf16`
- `GetStageForMatchIndex_ShouldSplitEvenly_WhenGroupStages` - stages `[Group1, Group2]`, totalMatches 8, index 3 -> `Group1`, index 4 -> `Group2` (covers the default arm)
- `GetStageForMatchIndex_ShouldReturnLastStage_WhenIndexBeyondAllStageSizes` - stages `[SemiFinals, Final]`, totalMatches 3, index 10 -> `Final` (covers the trailing return)

### Application test updates

None. There are no existing unit tests for `CreateSeasonCommandHandler` (verified: no matches for it under `tests\`).

---

## Part 2 - Knockout 90-minute scoring rule out of UpdateScoresForNextRoundCommandHandler

### Current code

`src\ThePredictions.Application\Features\Admin\Rounds\Commands\UpdateScoresForNextRoundCommandHandler.cs`, lines 83-118, three `internal static` methods added by commit `7b901397` ("Complete Knockout Matches On The 90-Minute Result Once Regulation Ends"):

```csharp
internal static (int HomeScore, int AwayScore) GetScoreForMatch(FixtureResponse fixture, bool isKnockout)
{
    if (isKnockout)
    {
        var fulltime = fixture.Score?.FullTime;
        if (fulltime?.Home != null && fulltime.Away != null)
            return (fulltime.Home.Value, fulltime.Away.Value);
    }

    return (fixture.Goals!.Home.GetValueOrDefault(), fixture.Goals.Away.GetValueOrDefault());
}

internal static bool IsKnockoutMatch(Match match)
{
    if (string.IsNullOrWhiteSpace(match.ApiRoundName))
        return false;

    if (!TournamentRoundNameParser.TryParseStage(match.ApiRoundName, out var stage))
        return false;

    return TournamentRoundNameParser.IsKnockoutStage(stage);
}

internal static MatchStatus GetMatchStatus(string apiStatus, bool isKnockout) => apiStatus switch
{
    "FT" or "AET" or "PEN" => MatchStatus.Completed,
    ...
    "BT" or "ET" or "P" when isKnockout => MatchStatus.Completed,
    "HT" or "1H" or "2H" or "ET" or "BT" or "P" or "LIVE" => MatchStatus.InProgress,
    "PST" => MatchStatus.Postponed,
    _ => MatchStatus.Scheduled
};
```

These have existing tests via `InternalsVisibleTo` (confirmed: `src\ThePredictions.Application\ThePredictions.Application.csproj` line 22 has `<InternalsVisibleTo Include="ThePredictions.Application.Tests.Unit" />`, and the tests live at `tests\Unit\ThePredictions.Application.Tests.Unit\Features\Admin\Rounds\Commands\UpdateScoresForNextRoundCommandHandlerTests.cs`).

### Scope boundary

`GetMatchStatus` mixes two concerns:

1. **Provider semantics** - what the raw api-sports.io short codes (`FT`, `AET`, `PEN`, `BT`, `ET`, `P`, `HT`, `1H`, `2H`, `LIVE`, `PST`) mean. That is an infrastructure concern; moving the raw-string mapping behind `IFootballDataService` is owned by the parallel plan `docs\todo\architecture\application-infrastructure-leaks\README.md` (provider normalisation). **Do not move the string mapping in this plan** - leave it in the handler as an `internal static` method, ready for that plan to relocate.
2. **Domain policy** - a knockout tie is scored on the 90-minute result, so once regulation ends the match is Completed for prediction purposes. That moves to Domain here.

The seam between them is a new neutral `MatchPhase` enum.

### New Domain types

**File: `src\ThePredictions.Domain\Common\Enumerations\MatchPhase.cs`**

```csharp
namespace ThePredictions.Domain.Common.Enumerations;

/// <summary>
/// Provider-neutral description of where a fixture is in its lifecycle,
/// used to decide the domain MatchStatus without knowing raw provider status codes.
/// </summary>
public enum MatchPhase
{
    NotStarted,

    /// <summary>In play, within the regulation 90 minutes (including half time).</summary>
    InPlay,

    /// <summary>Regulation has ended but the tie has not (break before extra time, extra time, penalties).</summary>
    PastRegulation,

    /// <summary>The tie is finished (full time, after extra time, or after penalties).</summary>
    Finished,

    Postponed
}
```

**File: `src\ThePredictions.Domain\Services\KnockoutScoringPolicy.cs`**

```csharp
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Domain.Services;

/// <summary>
/// A knockout tie is scored on the 90-minute result: once regulation ends the result can no
/// longer change for prediction purposes, so the match is treated as Completed and the
/// regulation (full-time) score is used rather than the running aggregate.
/// </summary>
public static class KnockoutScoringPolicy
{
    public static (int HomeScore, int AwayScore) SelectScore(bool isKnockout, int? regulationHomeScore, int? regulationAwayScore, int currentHomeScore, int currentAwayScore)
    {
        if (isKnockout && regulationHomeScore.HasValue && regulationAwayScore.HasValue)
            return (regulationHomeScore.Value, regulationAwayScore.Value);

        return (currentHomeScore, currentAwayScore);
    }

    public static MatchStatus ResolveStatus(MatchPhase phase, bool isKnockout) => phase switch
    {
        MatchPhase.Finished => MatchStatus.Completed,
        MatchPhase.PastRegulation when isKnockout => MatchStatus.Completed,
        MatchPhase.PastRegulation or MatchPhase.InPlay => MatchStatus.InProgress,
        MatchPhase.Postponed => MatchStatus.Postponed,
        _ => MatchStatus.Scheduled
    };
}
```

**New property on `src\ThePredictions.Domain\Models\Match.cs`** (replaces the handler's `IsKnockoutMatch`; `TournamentRoundNameParser` is in `ThePredictions.Domain.Common`, so add `using ThePredictions.Domain.Common;` to Match.cs):

```csharp
public bool IsKnockout =>
    !string.IsNullOrWhiteSpace(ApiRoundName) &&
    TournamentRoundNameParser.TryParseStage(ApiRoundName, out var stage) &&
    TournamentRoundNameParser.IsKnockoutStage(stage);
```

### Handler after

In `UpdateScoresForNextRoundCommandHandler.cs`:

1. Add `using ThePredictions.Domain.Services;`.
2. Replace the `Select` body (lines 63-74) with:

   ```csharp
   var matchResults = liveFixtures.Where(f => f.Fixture != null && f.Goals != null).Select(fixture =>
   {
       var localMatch = activeRound.Matches.First(m => m.ExternalId == fixture.Fixture!.Id);
       var isKnockout = isTournament && localMatch.IsKnockout;
       var (homeScore, awayScore) = KnockoutScoringPolicy.SelectScore(
           isKnockout,
           fixture.Score?.FullTime?.Home,
           fixture.Score?.FullTime?.Away,
           fixture.Goals!.Home.GetValueOrDefault(),
           fixture.Goals.Away.GetValueOrDefault());
       return new MatchResultDto(
           localMatch.Id,
           homeScore,
           awayScore,
           KnockoutScoringPolicy.ResolveStatus(GetMatchPhase(fixture.Fixture!.Status.Short), isKnockout)
       );
   }).ToList();
   ```

3. Delete `GetScoreForMatch`, `IsKnockoutMatch`, and `GetMatchStatus` (lines 83-118) and add the phase mapping in their place:

   ```csharp
   // Raw api-sports.io status codes; relocating this mapping behind IFootballDataService is
   // owned by docs/todo/architecture/application-infrastructure-leaks/README.md.
   internal static MatchPhase GetMatchPhase(string apiStatus) => apiStatus switch
   {
       "FT" or "AET" or "PEN" => MatchPhase.Finished,
       "BT" or "ET" or "P" => MatchPhase.PastRegulation,
       "HT" or "1H" or "2H" or "LIVE" => MatchPhase.InPlay,
       "PST" => MatchPhase.Postponed,
       _ => MatchPhase.NotStarted
   };
   ```

   Equivalence check (do not skip): the old arm `"HT" or "1H" or "2H" or "ET" or "BT" or "P" or "LIVE" => InProgress` listed `ET`/`BT`/`P` again for the non-knockout case; in the new shape those codes map to `PastRegulation`, and `ResolveStatus` maps `PastRegulation` without knockout to `InProgress`. The composed behaviour is identical for every code.

### Domain tests

**New file: `tests\Unit\ThePredictions.Domain.Tests.Unit\Services\KnockoutScoringPolicyTests.cs`**

`SelectScore` branches (isKnockout true/false; each regulation score null/present):

- `SelectScore_ShouldReturnRegulationScore_WhenKnockoutAndRegulationScoresPresent` - `(true, 1, 1, 2, 1)` returns `(1, 1)` (the commit's motivating case: 1-1 after 90, extra-time goal makes the aggregate 2-1)
- `SelectScore_ShouldReturnCurrentScore_WhenNotKnockout` - `(false, 1, 1, 3, 0)` returns `(3, 0)`
- `SelectScore_ShouldReturnCurrentScore_WhenKnockoutButRegulationHomeScoreMissing` - `(true, null, 1, 1, 1)` returns `(1, 1)`
- `SelectScore_ShouldReturnCurrentScore_WhenKnockoutButRegulationAwayScoreMissing` - `(true, 1, null, 1, 1)` returns `(1, 1)`

`ResolveStatus` branches (all five phases, plus the knockout split on `PastRegulation`):

- `ResolveStatus_ShouldBeCompleted_WhenPhaseFinished` - theory over `isKnockout` true and false
- `ResolveStatus_ShouldBeCompleted_WhenKnockoutPastRegulation`
- `ResolveStatus_ShouldBeInProgress_WhenNonKnockoutPastRegulation`
- `ResolveStatus_ShouldBeInProgress_WhenInPlay` - theory over `isKnockout` true and false
- `ResolveStatus_ShouldBePostponed_WhenPhasePostponed`
- `ResolveStatus_ShouldBeScheduled_WhenPhaseNotStarted`

**Additions to `tests\Unit\ThePredictions.Domain.Tests.Unit\Models\MatchTests.cs`** (port of the four `IsKnockoutMatch` tests, using the public constructor / `CreatePlaceholder` exactly as the current Application tests do):

- `IsKnockout_ShouldBeFalse_WhenApiRoundNameMissing`
- `IsKnockout_ShouldBeFalse_WhenApiRoundNameUnrecognised` - `"Friendly"`
- `IsKnockout_ShouldBeFalse_WhenGroupStage` - `"Group Stage - 1"`
- `IsKnockout_ShouldBeTrue_WhenKnockoutRoundName` - `"Round of 16"`

`MatchPhase` has no logic; no tests needed for the enum itself.

### Application test updates

Rewrite `tests\Unit\ThePredictions.Application.Tests.Unit\Features\Admin\Rounds\Commands\UpdateScoresForNextRoundCommandHandlerTests.cs` to cover only the surviving internal method. Delete the `GetScoreForMatch`, `IsKnockoutMatch` and `GetMatchStatus` tests (their logic is now covered in Domain) and replace with:

- `GetMatchPhase_ShouldBeFinished_WhenApiStatusIsFinal` - theory: `FT`, `AET`, `PEN`
- `GetMatchPhase_ShouldBePastRegulation_WhenTieContinuesBeyondNinetyMinutes` - theory: `BT`, `ET`, `P`
- `GetMatchPhase_ShouldBeInPlay_WhenWithinRegulation` - theory: `HT`, `1H`, `2H`, `LIVE`
- `GetMatchPhase_ShouldBePostponed_WhenApiStatusIsPst`
- `GetMatchPhase_ShouldBeNotStarted_WhenApiStatusIsUnrecognised` - theory: `NS`, `TBD`, `""`

---

## Part 3 - Round lifecycle state machine out of UpdateMatchResultsCommandHandler

### Current code

`src\ThePredictions.Application\Features\Admin\Rounds\Commands\UpdateMatchResultsCommandHandler.cs`. Two blocks sequence the Published -> InProgress -> Completed state machine inline:

Lines 32 and 54-64 (start transition):

```csharp
var wasRoundPublished = round.Status == RoundStatus.Published;
...
var isRoundStarting = wasRoundPublished && matchesToUpdate.Any(m => m.Status is MatchStatus.InProgress or MatchStatus.Completed);
if (isRoundStarting)
{
    round.UpdateStatus(RoundStatus.InProgress, dateTimeProvider);
    await roundRepository.UpdateAsync(round, cancellationToken);
    await statsService.TakeRoundStartSnapshotsAsync(round.Id, cancellationToken);

    var isLastRoundOfSeason = await roundRepository.IsLastRoundOfSeasonAsync(round.Id, round.SeasonId, cancellationToken);
    if (isLastRoundOfSeason)
        await boostService.AutoApplyUnusedBoostsForLastRoundAsync(round.Id, cancellationToken);
}
```

Lines 94-97 (complete transition; the block continues through prize processing and digest emails to line 118):

```csharp
if (round.Matches.All(m => m.Status is MatchStatus.Completed or MatchStatus.Postponed))
{
    round.UpdateStatus(RoundStatus.Completed, dateTimeProvider);
    await roundRepository.UpdateAsync(round, cancellationToken);
    ...
```

### Entity vs domain service - the decision

Both transition rules are pure functions of the Round aggregate's own state (its status and its matches' statuses), so they belong on the **Round entity** (`src\ThePredictions.Domain\Models\Round.cs`), next to the existing `UpdateStatus`. No domain service is warranted. Everything else in the handler is cross-aggregate orchestration and stays put: snapshot triggers (`TakeRoundStartSnapshotsAsync`), last-round boost auto-apply, prediction outcome updates, stats refreshes, prize processing, digest and prize emails.

### New entity methods (add to `Round.cs` after `UpdateStatus`)

```csharp
/// <summary>
/// Transitions a Published round to InProgress once any of its matches has kicked off.
/// Returns true when the transition happened, so callers can trigger start-of-round side effects.
/// </summary>
public bool TryStart(IDateTimeProvider dateTimeProvider)
{
    if (Status != RoundStatus.Published)
        return false;

    if (!_matches.Any(m => m.Status is MatchStatus.InProgress or MatchStatus.Completed))
        return false;

    UpdateStatus(RoundStatus.InProgress, dateTimeProvider);
    return true;
}

/// <summary>
/// Marks the round Completed once every match is Completed or Postponed. Deliberately not
/// gated on the current status: re-running results for an already-completed round returns
/// true again so the (idempotent) end-of-round side effects can self-heal.
/// </summary>
public bool TryComplete(IDateTimeProvider dateTimeProvider)
{
    if (!_matches.All(m => m.Status is MatchStatus.Completed or MatchStatus.Postponed))
        return false;

    UpdateStatus(RoundStatus.Completed, dateTimeProvider);
    return true;
}
```

### Handler after

1. Delete line 32 (`var wasRoundPublished = ...`).
2. Replace lines 54-56 with:

   ```csharp
   if (round.TryStart(dateTimeProvider))
   {
   ```

   (the body from `await roundRepository.UpdateAsync(round, cancellationToken);` onwards is unchanged).
3. Replace lines 94-97 with:

   ```csharp
   if (round.TryComplete(dateTimeProvider))
   {
       await roundRepository.UpdateAsync(round, cancellationToken);
   ```

   (the rest of the completion block is unchanged).

Recorded micro-behaviour decisions:

- **TryStart scans all matches, not just this request's batch.** The old code checked `matchesToUpdate.Any(...)`; the new code checks every match in the round. Because `Match.UpdateScore` never changes `Round.Status`, and any earlier call that left a match InProgress/Completed would already have flipped the round off Published, the two are equivalent in every reachable flow. The one divergence (an admin manually reverting a started round to Published) now self-corrects on the next results tick, which is the more correct outcome. Accept it.
- **TryComplete keeps the old "re-complete" semantics.** The old code re-entered the completion block on every subsequent result update for a completed round (prizes/digest/prize emails are idempotent by design - see the comments at lines 111-117). `TryComplete` preserves that; do not add a `Status == Completed` early-out. `UpdateStatus` already preserves `CompletedDateUtc` when re-setting Completed (its `originalStatus != Completed` guard), so the completion timestamp is not clobbered.

### Domain tests (add to `tests\Unit\ThePredictions.Domain.Tests.Unit\Models\RoundTests.cs`)

Use the file's existing helpers/style (`TestDateTimeProvider _dateTimeProvider`, `new Round(id: ..., ...)` public constructor with a `matches:` list; build matches with the full public `Match` constructor and explicit IDs as `PredictionDomainServiceTests` does).

`TryStart` branches (status guard true/false; any-match-started true/false):

- `TryStart_ShouldReturnFalse_WhenRoundIsDraft`
- `TryStart_ShouldReturnFalse_WhenRoundAlreadyInProgress`
- `TryStart_ShouldReturnFalse_WhenPublishedButNoMatchHasStarted` - all matches Scheduled; status stays Published
- `TryStart_ShouldStartRound_WhenPublishedAndMatchInProgress` - returns true; `Status` becomes `InProgress`
- `TryStart_ShouldStartRound_WhenPublishedAndMatchCompleted`

`TryComplete` branches (all-finished true/false; plus the re-complete path):

- `TryComplete_ShouldReturnFalse_WhenAnyMatchStillInProgress` - status unchanged
- `TryComplete_ShouldCompleteRound_WhenAllMatchesCompleted` - returns true; `Status` is `Completed`; `CompletedDateUtc` equals `_dateTimeProvider.UtcNow`
- `TryComplete_ShouldCompleteRound_WhenMatchesAreMixOfCompletedAndPostponed`
- `TryComplete_ShouldCompleteRound_WhenRoundHasNoMatches` - documents the `All()` on empty semantics (unreachable via the handler, which early-returns when nothing was updated, but the branch must be covered)
- `TryComplete_ShouldPreserveCompletedDate_WhenRoundAlreadyCompleted` - complete once, advance `_dateTimeProvider` with `AdvanceBy`, call again: returns true and `CompletedDateUtc` is unchanged

### Application test updates

None. There are no existing unit tests for `UpdateMatchResultsCommandHandler` (verified).

---

## Part 4 - Round scheduling policy out of SyncSeasonWithApiCommandHandler

### Current code

`src\ThePredictions.Application\Features\Admin\Seasons\Commands\SyncSeasonWithApiCommandHandler.cs` (546 lines, the largest file in Application). Domain policy trapped inside:

- **Fixture filtering** - duplicated verbatim between the league path (lines 62-80) and the tournament path (lines 318-336); flagged as June 2026 audit item 4.7 (last bullet: "Fixture-filtering block duplicated inside `SyncSeasonWithApiCommandHandler` (league vs tournament paths) -> extract").
- **Round summarising** (lines 83-106): per API round, exclude postponed fixtures, take the median kick-off (`fixturesInApiRound[fixturesInApiRound.Count / 2]`), parse the trailing round number (`TryParseRoundNumber`, lines 533-538), sort by median then round number.
- **`CalculateRoundWindows`** (lines 495-531): gap-based window boundaries at the midpoints between consecutive round medians.
- **Fixture-to-window allocation** (lines 111-128): first window where `WindowStart <= date < WindowEnd`, else unplaceable.
- **Postponement reconciliation** - duplicated between lines 209-225 (league Phase 4b) and lines 462-479 (tournament): `PST` and not already Postponed/Completed -> `Postpone()`; not `PST` but currently Postponed -> `Reschedule()`.
- **Round date realignment** - duplicated with a slight variation between lines 191-207 (league: non-postponed matches) and lines 441-460 (tournament: confirmed, non-postponed matches): round start = earliest active match, deadline = start minus 30 minutes.
- **Tournament placeholder filling** (lines 379-439): match by external id (date refresh), else fill an unassigned placeholder for the fixture's stage (assign teams, date, external id, API round name, and a per-stage custom lock time of earliest-in-stage minus 30 minutes for non-primary stages of combined rounds), else add as an extra match.
- Private records `ValidFixture`, `RoundFixtureSummary`, `RoundWindow` (lines 540-544).

**Hard constraint (June 2026 audit, finding 2.7 / decision 7): this handler is deliberately non-transactional.** Round-by-round persistence keeps database locks short while calling a slow external API; the sync is re-runnable and self-healing. This refactor must not change persistence sequencing: rounds are still created via `roundRepository.CreateAsync` inline in the loop (line 151), Phase 7 still runs `MoveMatchesToRoundAsync` for every moved batch **before** any `roundRepository.UpdateAsync`, and no transaction is introduced.

What stays in the handler (orchestration, per the template): the API fetch, repository loads, the `matchesByExternalId` index, round creation, the per-fixture move/add reconciliation with its `movedMatchesByTargetRound` persistence bookkeeping (lines 155-189 - already expressed through the aggregate methods `RemoveMatch`/`AcceptMatch`/`AddMatch`/`UpdateDate`), stale-match deletion (predictions check is a repository call), unplaceable-fixture logging, Phase 7 persistence, and `PublishUpcomingRoundsCommand`.

### New Domain types (new folder `src\ThePredictions.Domain\Services\Scheduling\`, namespace `ThePredictions.Domain.Services.Scheduling`, one public type per file)

**`FixtureSnapshot.cs`** - provider-neutral fixture facts (note `IsPostponed` replaces the raw `"PST"` string, which stays in the handler):

```csharp
[ExcludeFromCodeCoverage]
public record FixtureSnapshot(int ExternalId, DateTime MatchDateTimeUtc, int HomeTeamId, int AwayTeamId, string ApiRoundName, bool IsPostponed);
```

**`RoundFixtureSummary.cs`**

```csharp
[ExcludeFromCodeCoverage]
public record RoundFixtureSummary(string ApiRoundName, int RoundNumber, DateTime MedianDateUtc);
```

**`RoundWindow.cs`**

```csharp
[ExcludeFromCodeCoverage]
public record RoundWindow(string ApiRoundName, int RoundNumber, DateTime WindowStartUtc, DateTime WindowEndUtc);
```

**`FixtureAllocation.cs`**

```csharp
[ExcludeFromCodeCoverage]
public record FixtureAllocation(IReadOnlyDictionary<string, IReadOnlyList<FixtureSnapshot>> FixturesByApiRoundName, IReadOnlyList<FixtureSnapshot> UnplaceableFixtures);
```

**`TournamentSyncResult.cs`**

```csharp
[ExcludeFromCodeCoverage]
public record TournamentSyncResult(bool RoundChanged, IReadOnlyList<int> ExtraMatchExternalIds);
```

(each file needs `using System.Diagnostics.CodeAnalysis;`)

**`RoundSchedulingService.cs`** - static class; pure, deterministic ports of Phases 2, 3 and the two private helpers. One cohesive service is preferred over splitting into `FixtureAllocationService` + `RoundWindowCalculator` because the three methods form a single pipeline over the same types (summaries feed windows feed allocation) and none has independent callers.

```csharp
namespace ThePredictions.Domain.Services.Scheduling;

/// <summary>
/// Pure scheduling policy for allocating API fixtures to rounds: rounds are summarised by the
/// median kick-off of their non-postponed fixtures, window boundaries fall at the midpoints
/// between consecutive medians, and each fixture lands in the window containing its kick-off.
/// </summary>
public static class RoundSchedulingService
{
    public static bool TryParseRoundNumber(string apiRoundName, out int roundNumber)
    // exact port of handler lines 533-538

    public static IReadOnlyList<RoundFixtureSummary> SummariseRounds(IEnumerable<string> apiRoundNames, IReadOnlyList<FixtureSnapshot> fixtures)
    // port of handler lines 83-106: skip names that fail TryParseRoundNumber; per round take
    // fixtures where f.ApiRoundName == apiRoundName && !f.IsPostponed ordered by MatchDateTimeUtc;
    // skip empty; median = fixturesInApiRound[fixturesInApiRound.Count / 2].MatchDateTimeUtc;
    // sort the result by MedianDateUtc then RoundNumber and return it

    public static IReadOnlyList<RoundWindow> CalculateRoundWindows(IReadOnlyList<RoundFixtureSummary> sortedSummaries)
    // exact port of handler lines 495-531 (0 -> empty; 1 -> MinValue..MaxValue; else midpoint
    // boundaries computed in ticks with DateTimeKind.Utc)

    public static FixtureAllocation AllocateFixtures(IReadOnlyList<FixtureSnapshot> fixtures, IReadOnlyList<RoundWindow> windows)
    // port of handler lines 111-128: first window where MatchDateTimeUtc >= WindowStartUtc
    // && MatchDateTimeUtc < WindowEndUtc, grouped by window.ApiRoundName; no window -> unplaceable
}
```

**`TournamentRoundSynchroniser.cs`** - static class; port of the tournament placeholder-filling block (handler lines 379-439). Operates purely on domain types and returns what the handler needs for logging and persistence:

```csharp
namespace ThePredictions.Domain.Services.Scheduling;

public static class TournamentRoundSynchroniser
{
    /// <summary>
    /// Applies synced fixtures to a tournament round: refreshes dates on already-synced matches,
    /// fills unassigned placeholder matches for the fixture's stage (setting a custom lock time of
    /// earliest-in-stage minus 30 minutes for non-primary stages of combined rounds), and adds any
    /// fixture without a placeholder as a new match. Returns whether the round changed and the
    /// external ids of matches added beyond the expected count (for the caller to log).
    /// </summary>
    public static TournamentSyncResult ApplyFixtures(Round round, IReadOnlyList<FixtureSnapshot> fixtures, IReadOnlyList<TournamentStage> stages, TournamentStage primaryStage)
}
```

Port notes (decisions, so the implementer does not need to ask):

- The original calls `TournamentRoundNameParser.TryParseStage(fixture.ApiRoundName, out var fixtureStage)` without checking the return value, because the caller's grouping step already discarded unparseable names. In the domain method, `continue` (skip the fixture) when the parse fails - a defensive branch that must have a test.
- The original guards `if (stageFixtures.Any())` before computing the earliest-in-stage lock time. That check is provably always true (the current fixture is itself in `fixtures` and matches its own stage), so it is an untestable branch; **drop it** in the port, per the repo rule that unreachable code is removed rather than excluded.
- Existing-match date refresh, placeholder matching (`!m.AreTeamsConfirmed && m.ExternalId == null && m.ApiRoundName == TournamentRoundNameParser.GetDefaultDisplayName(fixtureStage)`), the `stages.Count > 1 && fixtureStage != primaryStage` custom-lock condition, and the `round.AddMatch(...)` fallback are ported verbatim.

**New method on `Match.cs`** (removes the postponement duplication):

```csharp
/// <summary>
/// Reconciles this match's status with the provider's postponement flag.
/// Returns true when the status changed.
/// </summary>
public bool ApplyPostponementStatus(bool isPostponed)
{
    if (isPostponed && Status is not (MatchStatus.Postponed or MatchStatus.Completed))
    {
        Postpone();
        return true;
    }

    if (!isPostponed && Status == MatchStatus.Postponed)
    {
        Reschedule();
        return true;
    }

    return false;
}
```

**New method on `Round.cs`** (removes the date-realignment duplication):

```csharp
private const int DeadlineMinutesBeforeStart = 30;

/// <summary>
/// Realigns the round's start date (and its deadline, 30 minutes earlier) to the earliest
/// confirmed, non-postponed match. Returns true when the dates changed.
/// </summary>
public bool RescheduleFromMatches()
{
    var activeMatches = _matches.Where(m => m.AreTeamsConfirmed && m.Status != MatchStatus.Postponed).ToList();
    if (!activeMatches.Any())
        return false;

    var earliestMatchDateUtc = activeMatches.Min(m => m.MatchDateTimeUtc);
    if (earliestMatchDateUtc == StartDateUtc)
        return false;

    UpdateDetails(RoundNumber, DisplayName, earliestMatchDateUtc, earliestMatchDateUtc.AddMinutes(-DeadlineMinutesBeforeStart), Status, ApiRoundName);
    return true;
}
```

Recorded decision: the league path filtered only on `Status != MatchStatus.Postponed` while the tournament path also required `AreTeamsConfirmed`. The unified filter includes `AreTeamsConfirmed`; this is behaviour-preserving because league-path rounds never contain placeholder matches (`AddMatch` always sets both team ids; placeholders are created only for tournaments).

### Ordered handler refactor steps

1. Add `using ThePredictions.Domain.Services.Scheduling;` to `SyncSeasonWithApiCommandHandler.cs`.
2. Add the audit-mandated comment at the top of `Handle` (also ticks June finding 2.7's action item):

   ```csharp
   // Deliberately non-transactional (June 2026 audit, decision 7): round-by-round persistence
   // keeps database locks short while calling a slow external API, and the sync is re-runnable
   // and self-healing. Do NOT make this command ITransactionalRequest and do NOT reorder the
   // persistence sequencing (moves before round updates).
   ```

3. Extract the shared fixture filter as a private handler method and call it from **both** paths, replacing lines 62-80 and 318-336 (this closes June item 4.7, last bullet):

   ```csharp
   private const string PostponedApiStatus = "PST";

   private static List<FixtureSnapshot> BuildFixtureSnapshots(IEnumerable<FixtureResponse> apiFixtures, Dictionary<int, Team> teamsByApiId)
   {
       var snapshots = new List<FixtureSnapshot>();

       foreach (var fixture in apiFixtures)
       {
           if (fixture.Fixture == null || fixture.Teams?.Home == null || fixture.Teams?.Away == null || fixture.League?.RoundName == null)
               continue;

           if (!teamsByApiId.TryGetValue(fixture.Teams.Home.Id, out var homeTeam) ||
               !teamsByApiId.TryGetValue(fixture.Teams.Away.Id, out var awayTeam))
               continue;

           snapshots.Add(new FixtureSnapshot(
               fixture.Fixture.Id,
               fixture.Fixture.Date.UtcDateTime,
               homeTeam.Id,
               awayTeam.Id,
               fixture.League.RoundName,
               fixture.Fixture.Status.Short == PostponedApiStatus));
       }

       return snapshots;
   }
   ```

   (`ITeamRepository.GetByApiIdsAsync` returns `Task<Dictionary<int, Team>>`, hence the parameter type.)
4. Replace Phases 2 and 3 of the league path (lines 82-128) with:

   ```csharp
   var fixtureSnapshots = BuildFixtureSnapshots(apiFixtures, teamsByApiId);
   var roundSummaries = RoundSchedulingService.SummariseRounds(apiRoundNames, fixtureSnapshots);
   var roundWindows = RoundSchedulingService.CalculateRoundWindows(roundSummaries);
   var allocation = RoundSchedulingService.AllocateFixtures(fixtureSnapshots, roundWindows);
   ```

   Downstream renames: `fixturesByRound[...]` becomes `allocation.FixturesByApiRoundName`, `unplaceableFixtures` becomes `allocation.UnplaceableFixtures`, `validFixtures` becomes `fixtureSnapshots` (Phases 4b and 5 iterate it), and `fixture.ApiStatus == "PST"` comparisons become `fixture.IsPostponed`.
5. Phase 4 (lines 134-207): keep the window loop, round creation, and the per-fixture move/add reconciliation exactly as they are (only the record rename applies). Replace the trailing date-realignment block (lines 191-207) with:

   ```csharp
   if (round.RescheduleFromMatches())
       allChangedRoundIds.Add(round.Id);
   ```

6. Phase 4b (lines 209-225): replace the body with:

   ```csharp
   foreach (var fixture in fixtureSnapshots)
   {
       if (!matchesByExternalId.TryGetValue(fixture.ExternalId, out var existing))
           continue;

       if (existing.Match.ApplyPostponementStatus(fixture.IsPostponed))
           allChangedRoundIds.Add(existing.Round.Id);
   }
   ```

7. Tournament path (`HandleTournamentSyncAsync`): replace the filtering block (lines 318-336) with `var validFixtures = BuildFixtureSnapshots(apiFixtures, teamsByApiId);`; keep the stage-to-mapping grouping and its warning logs (lines 338-358) in the handler. Inside the per-mapping loop, replace lines 376-479 with:

   ```csharp
   var syncResult = TournamentRoundSynchroniser.ApplyFixtures(round, fixtures, mapping.GetStageList(), mapping.GetPrimaryStage());

   foreach (var externalId in syncResult.ExtraMatchExternalIds)
       logger.LogInformation("Tournament sync: added extra match (ExternalId: {ExternalId}) beyond expected count to Round (ID: {RoundId})", externalId, round.Id);

   if (syncResult.RoundChanged)
       allChangedRoundIds.Add(round.Id);

   if (round.RescheduleFromMatches())
       allChangedRoundIds.Add(round.Id);

   foreach (var fixture in fixtures)
   {
       var match = round.Matches.FirstOrDefault(m => m.ExternalId == fixture.ExternalId);
       if (match != null && match.ApplyPostponementStatus(fixture.IsPostponed))
           allChangedRoundIds.Add(round.Id);
   }
   ```

   (Order preserved: fixture application, then date realignment, then postponements - matching the original sequence.)
8. Delete the now-unused private members: `CalculateRoundWindows`, `TryParseRoundNumber`, and the `ValidFixture`, `RoundFixtureSummary`, `RoundWindow` records (lines 495-544).
9. Do NOT touch Phase 5 (stale matches), Phase 6 (unplaceable logging), Phase 7 (persistence order), or Phase 8 (`PublishUpcomingRoundsCommand`), beyond the variable renames from step 4.

### Domain tests

**New file: `tests\Unit\ThePredictions.Domain.Tests.Unit\Services\Scheduling\RoundSchedulingServiceTests.cs`**

`TryParseRoundNumber`:

- `TryParseRoundNumber_ShouldReturnRoundNumber_WhenNameEndsWithNumber` - `"Regular Season - 12"` -> true, 12
- `TryParseRoundNumber_ShouldReturnFalse_WhenNameHasNoSeparator` - `"Final"`
- `TryParseRoundNumber_ShouldReturnFalse_WhenSuffixIsNotNumeric` - `"Group Stage - A"`

`SummariseRounds`:

- `SummariseRounds_ShouldSkipRound_WhenNameHasNoRoundNumber`
- `SummariseRounds_ShouldSkipRound_WhenRoundHasNoFixtures`
- `SummariseRounds_ShouldOnlyUseFixturesForTheRound_WhenOtherRoundsHaveFixtures`
- `SummariseRounds_ShouldExcludePostponedFixtures_WhenComputingMedian`
- `SummariseRounds_ShouldUseMiddleFixture_WhenOddFixtureCount`
- `SummariseRounds_ShouldUseUpperMiddleFixture_WhenEvenFixtureCount` - index `Count / 2` of the date-ordered list
- `SummariseRounds_ShouldOrderByMedianDate_WhenMediansDiffer`
- `SummariseRounds_ShouldOrderByRoundNumber_WhenMediansAreEqual` - covers the comparer tie-break branch

`CalculateRoundWindows`:

- `CalculateRoundWindows_ShouldReturnEmpty_WhenNoSummaries`
- `CalculateRoundWindows_ShouldReturnSingleOpenEndedWindow_WhenOneSummary` - `WindowStartUtc == DateTime.MinValue`, `WindowEndUtc == DateTime.MaxValue`
- `CalculateRoundWindows_ShouldSplitAtMidpoint_WhenTwoSummaries` - boundary at the tick midpoint of the two medians; first window `MinValue..midpoint`, second `midpoint..MaxValue`
- `CalculateRoundWindows_ShouldBoundMiddleWindowByBothMidpoints_WhenThreeSummaries`

`AllocateFixtures`:

- `AllocateFixtures_ShouldAllocateFixture_WhenDateInsideWindow`
- `AllocateFixtures_ShouldGroupFixturesByRound_WhenMultipleFixturesShareWindow`
- `AllocateFixtures_ShouldAllocateToLaterWindow_WhenDateEqualsBoundary` - window end is exclusive, next start inclusive
- `AllocateFixtures_ShouldMarkFixtureUnplaceable_WhenNoWindowContainsDate` - empty windows list

**New file: `tests\Unit\ThePredictions.Domain.Tests.Unit\Services\Scheduling\TournamentRoundSynchroniserTests.cs`** (build rounds/matches with public constructors and explicit IDs; a placeholder match is one with null team ids, `ExternalId == null`, and `ApiRoundName` set to the stage display name, e.g. `"Semi-finals"`):

- `ApplyFixtures_ShouldUpdateMatchDate_WhenExistingMatchDateChanged` - `RoundChanged` true
- `ApplyFixtures_ShouldReportNoChange_WhenExistingMatchDateUnchanged` - `RoundChanged` false, no extras
- `ApplyFixtures_ShouldSkipFixture_WhenStageUnrecognised` - fixture with `ApiRoundName = "Friendly"`; nothing changes
- `ApplyFixtures_ShouldFillPlaceholder_WhenUnassignedPlaceholderExistsForStage` - teams assigned, date updated, external id and API round name set, `RoundChanged` true
- `ApplyFixtures_ShouldSetCustomLockTime_WhenFillingNonPrimaryStageOfCombinedRound` - stages `[SemiFinals, Final]`, primary `SemiFinals`, a Final fixture: `CustomLockTimeUtc` equals earliest Final fixture minus 30 minutes
- `ApplyFixtures_ShouldNotSetCustomLockTime_WhenFillingPrimaryStage`
- `ApplyFixtures_ShouldNotSetCustomLockTime_WhenRoundHasSingleStage` - stages `[Final]` only
- `ApplyFixtures_ShouldAddNewMatch_WhenNoPlaceholderAvailable` - match count grows; external id appears in `ExtraMatchExternalIds`; `RoundChanged` true

**Additions to `tests\Unit\ThePredictions.Domain.Tests.Unit\Models\MatchTests.cs`** (`ApplyPostponementStatus`, all four condition combinations plus the completed guard):

- `ApplyPostponementStatus_ShouldPostponeMatch_WhenProviderPostponedAndMatchScheduled` - returns true; status `Postponed`; scores cleared
- `ApplyPostponementStatus_ShouldReturnFalse_WhenProviderPostponedAndMatchAlreadyPostponed`
- `ApplyPostponementStatus_ShouldReturnFalse_WhenProviderPostponedAndMatchCompleted` - completed results are never un-done by a stale PST
- `ApplyPostponementStatus_ShouldRescheduleMatch_WhenProviderActiveAndMatchPostponed` - returns true; status `Scheduled`
- `ApplyPostponementStatus_ShouldReturnFalse_WhenProviderActiveAndMatchNotPostponed`

**Additions to `tests\Unit\ThePredictions.Domain.Tests.Unit\Models\RoundTests.cs`** (`RescheduleFromMatches`):

- `RescheduleFromMatches_ShouldReturnFalse_WhenRoundHasNoMatches`
- `RescheduleFromMatches_ShouldReturnFalse_WhenAllMatchesPostponed`
- `RescheduleFromMatches_ShouldReturnFalse_WhenAllMatchesArePlaceholders` - unconfirmed teams are ignored
- `RescheduleFromMatches_ShouldReturnFalse_WhenEarliestMatchEqualsStartDate`
- `RescheduleFromMatches_ShouldUpdateStartAndDeadline_WhenEarliestMatchDiffers` - `StartDateUtc` = earliest active match; `DeadlineUtc` = 30 minutes earlier; postponed/placeholder matches excluded from the minimum

### Application test updates

None. There are no existing unit tests for `SyncSeasonWithApiCommandHandler` (verified).

---

## Out of scope

- Moving the raw api-sports.io status-code mapping (`GetMatchPhase`) behind `IFootballDataService`, and any other provider-DTO normalisation - owned by the parallel plan `docs\todo\architecture\application-infrastructure-leaks\README.md` (not yet present in the repo at the time of writing; Part 2 leaves the mapping as a clearly commented `internal static` seam for it).
- Making `SyncSeasonWithApiCommandHandler` transactional or reordering its persistence - explicitly forbidden (June 2026 audit decision 7).
- Extracting the league-path per-fixture move/add reconciliation (handler lines 155-189) into Domain - it is persistence bookkeeping (`movedMatchesByTargetRound` feeds `MoveMatchesToRoundAsync`) already expressed through aggregate methods; extracting it would couple a domain service to the repository's move semantics.
- The other June audit item 4.7 bullets (`ValidateSeasonAgainstApiAsync` duplication, season-stats SELECT, `Ordinal()`, etc.) - only the fixture-filtering bullet is closed here.
- Fixing the direct `DateTime.UtcNow` at `UpdateScoresForNextRoundCommandHandler` line 39 (June audit item 4.1's `IDateTimeProvider` sweep) - a separate mechanical clean-up; do not bundle it in.
- Adding new behaviour of any kind. Every part is a behaviour-preserving extraction; the only intentional micro-deviations are the ones recorded inline (empty-stages guard in Part 1, `TryStart` scanning all matches in Part 3, the unified `RescheduleFromMatches` filter and the dropped always-true `stageFixtures.Any()` check in Part 4).
- Database changes: none of these extractions touch persistence shape, so `docs/guides/database-schema.md` and the DatabaseTools refresh tool need no updates.

## Verification checklist

Run after **each** part, and once more after all four:

- [ ] `dotnet build ThePredictions.sln /p:TreatWarningsAsErrors=true` - clean build (this also catches xUnit1051)
- [ ] `"tools\Test Coverage\coverage-unit.bat"` - all unit tests pass and the report shows **100% line and 100% branch coverage for ThePredictions.Domain**
- [ ] Part 1: `CreateSeasonCommandHandler` no longer contains `GetStageForMatchIndex`; `TournamentRoundNameParser.GetStageForMatchIndex` exists with the tests listed
- [ ] Part 2: `UpdateScoresForNextRoundCommandHandler` contains only `GetMatchPhase` as an internal static (no `GetScoreForMatch` / `IsKnockoutMatch` / `GetMatchStatus`); `KnockoutScoringPolicy`, `MatchPhase` and `Match.IsKnockout` exist; the rewritten Application test file covers only `GetMatchPhase`
- [ ] Part 3: `UpdateMatchResultsCommandHandler` no longer reads `wasRoundPublished` or calls `UpdateStatus` directly; `Round.TryStart` / `Round.TryComplete` exist with the tests listed
- [ ] Part 4: `SyncSeasonWithApiCommandHandler` contains a single `BuildFixtureSnapshots` (no duplicated filtering block), no `CalculateRoundWindows` / `TryParseRoundNumber` / private records, and the non-transactional comment at the top of `Handle`; grep confirms `MoveMatchesToRoundAsync` still runs before the Phase 7 `UpdateAsync` loop
- [ ] No new `.sql` files, no database or DI registration changes (all new domain types are static or entity members)
- [ ] UK English spelling and plain hyphens throughout the new code and comments
