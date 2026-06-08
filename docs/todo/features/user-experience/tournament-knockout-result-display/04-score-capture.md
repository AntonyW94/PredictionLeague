# Task 4: Score Capture & Status Mapping

**Parent Feature:** [Displaying Knockout Results (Extra Time & Penalties)](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Capture the running ET aggregate and shootout tally on every poll (so the UI can
show a live ticker), keep the match `InProgress` through `BT`/`ET`/`P`, and pass
the values through to `Match.UpdateScore`.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Application/Features/Admin/Rounds/Commands/UpdateScoresForNextRoundCommandHandler.cs` | Modify | `GetScoreForMatch` (capture) + `GetMatchStatus` + `Handle` wiring |
| `src/ThePredictions.Application/Features/Admin/Rounds/Commands/UpdateMatchResultsCommandHandler.cs` | Modify | Pass new values into `UpdateScore` |
| `src/ThePredictions.Infrastructure/Repositories/RoundRepository.cs` | Modify | **Persist** the four columns in `UpdateMatchScoresAsync` (see Step 5) |
| `tests/Unit/ThePredictions.Application.Tests.Unit/...UpdateScoresForNextRoundCommandHandlerTests.cs` | Modify | Cover capture + status mapping |

## Background

`score.fulltime` = 90′ score; `goals` = after‑extra‑time aggregate (it excludes
the shootout, so it is stable during penalties); `score.penalty` = shootout
tally. The single score‑capture point is `UpdateScoresForNextRoundCommandHandler`
(the live‑scores command just fans out to it per active season).

## Implementation Steps

### Step 1: `GetScoreForMatch` — capture live + final

Capture ET/penalty values whenever the tie is past 90 minutes — live
(`BT`/`ET`/`P`) as well as finished (`AET`/`PEN`). The 90′ logic is unchanged.

```csharp
internal static (int HomeScore, int AwayScore,
                 int? AfterExtraTimeHomeScore, int? AfterExtraTimeAwayScore,
                 int? PenaltyHomeScore, int? PenaltyAwayScore)
    GetScoreForMatch(FixtureResponse fixture, Match localMatch, bool isTournament, string apiStatus)
{
    var ninetyMinuteHome = fixture.Goals!.Home.GetValueOrDefault();
    var ninetyMinuteAway = fixture.Goals.Away.GetValueOrDefault();

    if (isTournament && IsKnockoutMatch(localMatch))
    {
        var fulltime = fixture.Score?.FullTime;
        if (fulltime?.Home != null && fulltime.Away != null)
        {
            ninetyMinuteHome = fulltime.Home.Value;
            ninetyMinuteAway = fulltime.Away.Value;

            // Tie went past 90 minutes — extra time / break / shootout (live or finished).
            if (apiStatus is "BT" or "ET" or "P" or "AET" or "PEN")
            {
                var afterEtHome = fixture.Goals.Home.GetValueOrDefault();
                var afterEtAway = fixture.Goals.Away.GetValueOrDefault();

                int? penHome = null;
                int? penAway = null;
                if (fixture.Score?.Penalty is { Home: not null, Away: not null } pens)
                {
                    penHome = pens.Home;
                    penAway = pens.Away;
                }

                return (ninetyMinuteHome, ninetyMinuteAway, afterEtHome, afterEtAway, penHome, penAway);
            }
        }
    }

    return (ninetyMinuteHome, ninetyMinuteAway, null, null, null, null);
}
```

### Step 2: `GetMatchStatus` — keep the score visible during ET/penalties

Map the post‑90 in‑progress phases to `InProgress`; otherwise they fall to the
default `Scheduled` and `UpdateScore` clears the score mid‑match.

```csharp
private static MatchStatus GetMatchStatus(string apiStatus) => apiStatus switch
{
    "FT" or "AET" or "PEN" => MatchStatus.Completed,
    "HT" or "1H" or "2H" or "ET" or "BT" or "P" or "LIVE" => MatchStatus.InProgress,
    "PST" => MatchStatus.Postponed,
    _ => MatchStatus.Scheduled
};
```

(`BT` = break before/within extra time, `P` = shootout in progress, `LIVE` =
generic in‑play. `AET`/`PEN` stay the *finished* states that settle the caption.)

### Step 3: `Handle` — pass status + build the richer DTO

```csharp
var (homeScore, awayScore, afterEtHome, afterEtAway, penHome, penAway) =
    GetScoreForMatch(fixture, localMatch, isTournament, fixture.Fixture!.Status.Short);

return new MatchResultDto(localMatch.Id, homeScore, awayScore,
    GetMatchStatus(fixture.Fixture!.Status.Short),
    afterEtHome, afterEtAway, penHome, penAway);
```

### Step 4: `UpdateMatchResultsCommandHandler` call site (~line 47)

```csharp
matchToUpdate.UpdateScore(
    matchResult.HomeScore,
    matchResult.AwayScore,
    matchResult.Status,
    matchResult.AfterExtraTimeHomeScore,
    matchResult.AfterExtraTimeAwayScore,
    matchResult.PenaltyHomeScore,
    matchResult.PenaltyAwayScore);
```

### Step 5: Persist the columns — `RoundRepository.UpdateMatchScoresAsync`

**This is the actual write path and currently only sets three columns** — it
must include the four new ones or nothing we capture is ever saved. Update both
the SQL and the anonymous parameter projection (`RoundRepository.cs:471`):

```csharp
const string sql = @"
UPDATE
    [Matches]
SET
    [ActualHomeTeamScore] = @ActualHomeTeamScore,
    [ActualAwayTeamScore] = @ActualAwayTeamScore,
    [AfterExtraTimeHomeScore] = @AfterExtraTimeHomeScore,
    [AfterExtraTimeAwayScore] = @AfterExtraTimeAwayScore,
    [PenaltyHomeScore] = @PenaltyHomeScore,
    [PenaltyAwayScore] = @PenaltyAwayScore,
    [Status] = @Status
WHERE
    [Id] = @Id;";

var command = new CommandDefinition(
    commandText: sql,
    parameters: matches.Select(m => new
    {
        m.Id,
        m.ActualHomeTeamScore,
        m.ActualAwayTeamScore,
        m.AfterExtraTimeHomeScore,
        m.AfterExtraTimeAwayScore,
        m.PenaltyHomeScore,
        m.PenaltyAwayScore,
        Status = m.Status.ToString()
    }),
    transaction: Transaction,
    cancellationToken: cancellationToken
);
```

The `IRoundRepository.UpdateMatchScoresAsync(List<Match>, CancellationToken)`
signature is unchanged — only the SQL/projection inside changes.

> **Read path needs no SQL change.** `RoundRepository` hydrates `Match` via
> `SELECT r.*, m.*` (`GetRoundsWithMatchesSql`) and Dapper maps the columns to
> the `Match` constructor **by name**, so the new `m.*` columns flow in once the
> constructor gains the parameters (Task 2). Match creation (`AddMatchSql`) and
> detail edits (`UpdateAsync`) deliberately don't touch score columns, so the new
> nullable columns stay `NULL` on create and are preserved on edit — no change
> needed there.

## Idempotency note

The four columns are now written while the match is `InProgress` too. That is
safe: `UpdateScore` runs each poll, the 90′ `ActualScore` is unchanged so
prediction outcomes / stable stats stay idempotent, and the columns are cleared
only on revert to `Scheduled`/`Postponed`.

## Tests

- [ ] `GetScoreForMatch` returns after‑ET + penalties for a `PEN` knockout fixture.
- [ ] returns after‑ET, null penalties for an `AET` (and for live `ET`/`BT`) knockout fixture.
- [ ] returns running penalties for a live `P` (shootout‑in‑progress) fixture.
- [ ] returns nulls for a 90′‑finished knockout (`FT`).
- [ ] returns nulls for group‑stage / league matches.
- [ ] `GetMatchStatus`: `BT`/`P`/`LIVE` → `InProgress`; `AET`/`PEN` → `Completed`; unknown → `Scheduled`.

## Verification

- [ ] Build clean with `/p:TreatWarningsAsErrors=true`.
- [ ] During an `ET`/`BT`/`P` poll the match remains `InProgress` and keeps its 90′ score; ET/penalty columns are **written to the database** (confirm via `UpdateMatchScoresAsync`, not just the in‑memory entity).
- [ ] On `AET`/`PEN` the final ET/penalty values persist and the match flips to `Completed`.
</content>
