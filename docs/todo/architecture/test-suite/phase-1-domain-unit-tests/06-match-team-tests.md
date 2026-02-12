# Task: Match and Team Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the `Match` and `Team` entity factory methods, validation, and business logic methods.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/MatchTests.cs` | Create | All Match unit tests |
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/TeamTests.cs` | Create | All Team unit tests |

## Implementation Steps

### Step 1: Match.Create factory tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateMatch_WhenValidParametersProvided` | All valid | Properties set, Status = Scheduled, scores null |
| `Create_ShouldSetStatusToScheduled_WhenCreated` | Valid input | `Status = MatchStatus.Scheduled` |
| `Create_ShouldSetScoresToNull_WhenCreated` | Valid input | `ActualHomeTeamScore = null, ActualAwayTeamScore = null` |
| `Create_ShouldSetCustomLockTimeUtcToNull_WhenCreated` | Valid input | `CustomLockTimeUtc = null` |
| `Create_ShouldSetPlaceholderNamesToNull_WhenCreated` | Valid input | Both `PlaceholderHomeName` and `PlaceholderAwayName` null |
| `Create_ShouldThrowException_WhenTeamPlaysItself` | `homeTeamId == awayTeamId` | `ArgumentException` |
| `Create_ShouldThrowException_WhenTeamPlaysItselfWithZeroIds` | `homeTeamId: 0, awayTeamId: 0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenMatchDateIsDefault` | `default(DateTime)` | `ArgumentException` |
| `Create_ShouldAcceptNullExternalId` | `externalId: null` | No exception |
| `Create_ShouldSetExternalId_WhenProvided` | `externalId: 12345` | `ExternalId = 12345` |

### Step 2: Match.UpdateScore tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateScore_ShouldSetScores_WhenStatusIsCompleted` | `(2, 1, Completed)` | Scores set, Status = Completed |
| `UpdateScore_ShouldSetScores_WhenStatusIsInProgress` | `(1, 0, InProgress)` | Scores set, Status = InProgress |
| `UpdateScore_ShouldClearScores_WhenStatusIsScheduled` | `(2, 1, Scheduled)` | `ActualHomeTeamScore = null, ActualAwayTeamScore = null`, Status = Scheduled |
| `UpdateScore_ShouldIgnoreScoreValues_WhenStatusIsScheduled` | `(5, 3, Scheduled)` | Scores = null (values ignored) |
| `UpdateScore_ShouldThrowException_WhenHomeScoreIsNegative` | `(-1, 0, Completed)` | `ArgumentException` |
| `UpdateScore_ShouldThrowException_WhenAwayScoreIsNegative` | `(0, -1, Completed)` | `ArgumentException` |
| `UpdateScore_ShouldAllowZeroScores_WhenStatusIsCompleted` | `(0, 0, Completed)` | Scores set to 0 |
| `UpdateScore_ShouldAllowHighScores_WhenStatusIsCompleted` | `(9, 0, Completed)` | Scores set correctly |
| `UpdateScore_ShouldUpdateStatus_WhenCalled` | Any status | `Status` property updated |

### Step 3: Match.UpdateDetails tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDetails_ShouldUpdateProperties_WhenValid` | Valid params | `HomeTeamId`, `AwayTeamId`, `MatchDateTimeUtc` updated |
| `UpdateDetails_ShouldThrowException_WhenTeamPlaysItself` | Same team IDs | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenMatchDateIsDefault` | `default(DateTime)` | `ArgumentException` |
| `UpdateDetails_ShouldNotChangeStatus_WhenUpdating` | Valid params | `Status` unchanged |
| `UpdateDetails_ShouldNotChangeScores_WhenUpdating` | Valid params | `ActualHomeTeamScore`, `ActualAwayTeamScore` unchanged |

### Step 4: Match.UpdateDate tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDate_ShouldUpdateMatchDateTime_WhenCalled` | New date | `MatchDateTimeUtc` updated |
| `UpdateDate_ShouldAcceptAnyDateTime_WhenCalled` | Any DateTime | No validation, just assignment |

### Step 5: Team.Create factory tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateTeam_WhenValidParametersProvided` | All valid | Properties set correctly |
| `Create_ShouldThrowException_WhenNameIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenNameIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenShortNameIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenShortNameIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenLogoUrlIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenLogoUrlIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenAbbreviationIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenAbbreviationIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenAbbreviationIsTooShort` | `"AB"` (2 chars) | `ArgumentException` |
| `Create_ShouldThrowException_WhenAbbreviationIsTooLong` | `"ABCD"` (4 chars) | `ArgumentException` |
| `Create_ShouldCreateTeam_WhenAbbreviationIsExactlyThreeCharacters` | `"MUN"` | No exception, `Abbreviation = "MUN"` |
| `Create_ShouldAcceptNullApiTeamId` | `apiTeamId: null` | No exception |
| `Create_ShouldSetApiTeamId_WhenProvided` | `apiTeamId: 42` | `ApiTeamId = 42` |

### Step 6: Team.UpdateDetails tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDetails_ShouldUpdateAllProperties_WhenValid` | Valid params | All properties updated |
| `UpdateDetails_ShouldThrowException_WhenNameIsNull` | `null` | `ArgumentNullException` |
| `UpdateDetails_ShouldThrowException_WhenNameIsEmpty` | `""` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenShortNameIsEmpty` | `""` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenLogoUrlIsEmpty` | `""` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenAbbreviationIsTooShort` | `"AB"` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenAbbreviationIsTooLong` | `"ABCD"` | `ArgumentException` |
| `UpdateDetails_ShouldAcceptNullApiTeamId` | `apiTeamId: null` | No exception, `ApiTeamId = null` |

## Verification

- [ ] Match self-play prevention works in Create and UpdateDetails
- [ ] UpdateScore clears scores when status is Scheduled (ignores provided score values)
- [ ] UpdateScore sets scores when status is Completed or InProgress
- [ ] UpdateScore validates negative scores
- [ ] UpdateDetails does not change Status or Scores
- [ ] UpdateDate has no validation (accepts any DateTime)
- [ ] Team abbreviation must be exactly 3 characters
- [ ] All string params validated for null and empty
- [ ] All nullable parameters accepted (ExternalId, ApiTeamId)
- [ ] `dotnet test` passes

## Edge Cases to Consider

- Match with team IDs 0 (valid — no guard against zero team IDs, only self-play)
- Self-play with zero IDs (0 vs 0 should fail)
- UpdateScore with very high scores (e.g., 20-0)
- UpdateDate accepts default DateTime (no guard — different from Create and UpdateDetails)
- Team abbreviation of exactly 3 characters is the only valid length
