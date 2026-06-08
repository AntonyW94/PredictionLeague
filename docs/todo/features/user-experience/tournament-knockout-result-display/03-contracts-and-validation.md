# Task 3: Contracts & Validation

**Parent Feature:** [Displaying Knockout Results (Extra Time & Penalties)](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Extend the DTOs that carry the match result (`MatchResultDto` for writes,
`MatchInRoundDto` for reads), and update validation + the test builder.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `src/ThePredictions.Contracts/Admin/Matches/MatchResultDto.cs` | Modify | Carry ET/pens from capture → command |
| `src/ThePredictions.Contracts/Admin/Rounds/MatchInRoundDto.cs` | Modify | Carry ET/pens read → client |
| `src/ThePredictions.Validators/Admin/Matches/MatchResultDtoValidator.cs` | Modify | Validate the new fields |
| `tests/Shared/ThePredictions.Tests.Builders/Admin/Matches/MatchResultDtoBuilder.cs` | Modify | Builder support |
| `tests/Unit/ThePredictions.Validators.Tests.Unit/Admin/Matches/MatchResultDtoValidatorTests.cs` | Modify | Validator tests |

## Implementation Steps

### Step 1: Extend `MatchResultDto`

```csharp
public record MatchResultDto(
    int MatchId,
    int HomeScore,
    int AwayScore,
    MatchStatus Status,
    int? AfterExtraTimeHomeScore = null,
    int? AfterExtraTimeAwayScore = null,
    int? PenaltyHomeScore = null,
    int? PenaltyAwayScore = null);
```

Defaults keep existing call sites compiling; the sync path (Task 4) and the
admin path (Task 6) both populate them.

### Step 2: Extend `MatchInRoundDto`

Append the four nullable params **after** the existing optional placeholder
params:

```csharp
public record MatchInRoundDto(
    int Id,
    DateTime MatchDateTimeUtc,
    int? MatchNumber,
    int? HomeTeamId,
    string? HomeTeamName,
    string? HomeTeamShortName,
    string? HomeTeamAbbreviation,
    string? HomeTeamLogoUrl,
    int? AwayTeamId,
    string? AwayTeamName,
    string? AwayTeamShortName,
    string? AwayTeamAbbreviation,
    string? AwayTeamLogoUrl,
    int? ActualHomeTeamScore,
    int? ActualAwayTeamScore,
    MatchStatus Status,
    string? PlaceholderHomeName = null,
    string? PlaceholderAwayName = null,
    int? AfterExtraTimeHomeScore = null,
    int? AfterExtraTimeAwayScore = null,
    int? PenaltyHomeScore = null,
    int? PenaltyAwayScore = null);
```

The two query handlers that build this are updated in **Task 5**.

### Step 3: Validate the new fields in `MatchResultDtoValidator`

Only validate when present. **Penalty scores can exceed 9**, so do not reuse the
0–9 rule for them:

```csharp
RuleFor(x => x.AfterExtraTimeHomeScore)
    .InclusiveBetween(0, 9)
    .When(x => x.AfterExtraTimeHomeScore.HasValue)
    .WithMessage("After-extra-time home score must be between 0 and 9.");

RuleFor(x => x.AfterExtraTimeAwayScore)
    .InclusiveBetween(0, 9)
    .When(x => x.AfterExtraTimeAwayScore.HasValue)
    .WithMessage("After-extra-time away score must be between 0 and 9.");

RuleFor(x => x.PenaltyHomeScore)
    .InclusiveBetween(0, 30)
    .When(x => x.PenaltyHomeScore.HasValue)
    .WithMessage("Penalty home score must be between 0 and 30.");

RuleFor(x => x.PenaltyAwayScore)
    .InclusiveBetween(0, 30)
    .When(x => x.PenaltyAwayScore.HasValue)
    .WithMessage("Penalty away score must be between 0 and 30.");
```

### Step 4: Builder + validator tests

* Add fluent helpers to `MatchResultDtoBuilder` (e.g. `WithExtraTime(int, int)`,
  `WithPenalties(int, int)`).
* Add validator tests for: valid present values, null values (pass), out‑of‑range
  after‑ET (fail), out‑of‑range penalties (fail).

## Verification

- [ ] Both records compile; all existing call sites still build.
- [ ] Validator passes when the new fields are null and when in range; fails when out of range.
- [ ] `dotnet build /p:TreatWarningsAsErrors=true` clean (watch xUnit1051 — use `CancellationToken.None`).

## Notes

- No other DTO needs these fields: `ActiveRoundMatchDto` (dashboard) shows the *predicted* score, and `PredictionScoreDto` is a prediction.
</content>
