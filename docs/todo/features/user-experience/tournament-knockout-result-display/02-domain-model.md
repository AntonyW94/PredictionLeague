# Task 2: Domain Model (`Match`)

**Parent Feature:** [Displaying Knockout Results (Extra Time & Penalties)](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Add the four ET/penalty properties to the `Match` entity and carry them through
`UpdateScore` / `Postpone`, keeping the Domain project at **100% line + branch
coverage**.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Domain/Models/Match.cs` | Modify | Properties, constructor, `UpdateScore`, `Postpone` |
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/MatchTests.cs` | Modify | Cover new logic to 100% |

## Implementation Steps

### Step 1: Add properties

```csharp
public int? AfterExtraTimeHomeScore { get; private set; }
public int? AfterExtraTimeAwayScore { get; private set; }
public int? PenaltyHomeScore { get; private set; }
public int? PenaltyAwayScore { get; private set; }
```

### Step 2: Extend the public (Dapper) constructor

The public constructor at `Match.cs:30` is used by Dapper hydration. Append the
four parameters after `apiRoundName` and assign them in the body. Update any
test/builder call sites that use this constructor (see Task 3 builder, and the
tests below).

### Step 3: Extend `UpdateScore`

Carry the ET/penalty values; clear them (with `ActualScore`) when reverted to
`Scheduled`/`Postponed`. Follow project style (UK spelling, statements on their
own line).

```csharp
public void UpdateScore(int homeScore, int awayScore, MatchStatus status,
    int? afterExtraTimeHomeScore, int? afterExtraTimeAwayScore,
    int? penaltyHomeScore, int? penaltyAwayScore)
{
    Guard.Against.Negative(homeScore);
    Guard.Against.Negative(awayScore);

    if (afterExtraTimeHomeScore.HasValue)
        Guard.Against.Negative(afterExtraTimeHomeScore.Value);

    if (afterExtraTimeAwayScore.HasValue)
        Guard.Against.Negative(afterExtraTimeAwayScore.Value);

    if (penaltyHomeScore.HasValue)
        Guard.Against.Negative(penaltyHomeScore.Value);

    if (penaltyAwayScore.HasValue)
        Guard.Against.Negative(penaltyAwayScore.Value);

    if (status is MatchStatus.Scheduled or MatchStatus.Postponed)
    {
        ActualHomeTeamScore = null;
        ActualAwayTeamScore = null;
        AfterExtraTimeHomeScore = null;
        AfterExtraTimeAwayScore = null;
        PenaltyHomeScore = null;
        PenaltyAwayScore = null;
    }
    else
    {
        ActualHomeTeamScore = homeScore;
        ActualAwayTeamScore = awayScore;
        AfterExtraTimeHomeScore = afterExtraTimeHomeScore;
        AfterExtraTimeAwayScore = afterExtraTimeAwayScore;
        PenaltyHomeScore = penaltyHomeScore;
        PenaltyAwayScore = penaltyAwayScore;
    }

    Status = status;
}
```

### Step 4: Extend `Postpone()`

`Postpone()` (`Match.cs:139`) must also null the four new columns alongside the
existing `ActualHomeTeamScore` / `ActualAwayTeamScore`.

## Tests (required — 100% line + branch)

In `MatchTests.cs`, using the public constructor with an explicit id and
`CancellationToken.None` where relevant; naming `MethodName_ShouldX_WhenY()`:

- [ ] `UpdateScore_ShouldStoreExtraTimeAndPenaltyScores_WhenSupplied`
- [ ] `UpdateScore_ShouldStoreExtraTimeScores_WhenNoPenalties` (penalty args null)
- [ ] `UpdateScore_ShouldClearExtraTimeAndPenaltyScores_WhenStatusScheduled`
- [ ] `UpdateScore_ShouldClearExtraTimeAndPenaltyScores_WhenStatusPostponed`
- [ ] `UpdateScore_ShouldThrow_WhenExtraTimeScoreNegative` (covers each `HasValue` guard branch)
- [ ] `UpdateScore_ShouldThrow_WhenPenaltyScoreNegative`
- [ ] `Postpone_ShouldClearExtraTimeAndPenaltyScores`

## Verification

- [ ] `tools\Test Coverage\coverage-unit.bat` shows **100% line and branch** for Domain.
- [ ] `dotnet build /p:TreatWarningsAsErrors=true` is clean.
- [ ] Existing `Match` tests/builders updated for the new constructor parameters.

## Edge Cases to Consider

- Each nullable guard branch (value present vs null) for full branch coverage.
- Reverting a completed knockout to `Scheduled`/`Postponed` clears all six score fields.

## Notes

- The only production caller of `UpdateScore` is `UpdateMatchResultsCommandHandler` (Task 4); both the sync and admin paths flow through it.
</content>
