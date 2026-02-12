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
| `Create_ShouldThrowException_WhenTeamPlaysItself` | `homeTeamId == awayTeamId` | `ArgumentException` |
| `Create_ShouldThrowException_WhenMatchDateIsDefault` | `default(DateTime)` | `ArgumentException` |
| `Create_ShouldAcceptNullExternalId` | `externalId: null` | No exception |

### Step 2: Match.UpdateScore tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateScore_ShouldSetScores_WhenStatusIsCompleted` | `(2, 1, Completed)` | Scores set, Status = Completed |
| `UpdateScore_ShouldSetScores_WhenStatusIsInProgress` | `(1, 0, InProgress)` | Scores set, Status = InProgress |
| `UpdateScore_ShouldClearScores_WhenStatusIsScheduled` | `(2, 1, Scheduled)` | Scores = null, Status = Scheduled |
| `UpdateScore_ShouldThrowException_WhenHomeScoreIsNegative` | `(-1, 0, Completed)` | `ArgumentException` |
| `UpdateScore_ShouldThrowException_WhenAwayScoreIsNegative` | `(0, -1, Completed)` | `ArgumentException` |
| `UpdateScore_ShouldAllowZeroScores_WhenStatusIsCompleted` | `(0, 0, Completed)` | Scores set to 0 |

### Step 3: Match.UpdateDetails tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDetails_ShouldUpdateProperties_WhenValid` | Valid params | All properties updated |
| `UpdateDetails_ShouldThrowException_WhenTeamPlaysItself` | Same team IDs | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenMatchDateIsDefault` | `default(DateTime)` | `ArgumentException` |

### Step 4: Match.UpdateDate test

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDate_ShouldUpdateMatchDateTime_WhenCalled` | New date | `MatchDateTimeUtc` updated |

### Step 5: Team.Create factory tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateTeam_WhenValidParametersProvided` | All valid | Properties set correctly |
| `Create_ShouldThrowException_WhenNameIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenShortNameIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenLogoUrlIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenAbbreviationIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenAbbreviationIsTooShort` | `"AB"` (2 chars) | `ArgumentException` |
| `Create_ShouldThrowException_WhenAbbreviationIsTooLong` | `"ABCD"` (4 chars) | `ArgumentException` |
| `Create_ShouldAcceptNullApiTeamId` | `apiTeamId: null` | No exception |

### Step 6: Team.UpdateDetails tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDetails_ShouldUpdateAllProperties_WhenValid` | Valid params | All properties updated |
| `UpdateDetails_ShouldThrowException_WhenAbbreviationLengthInvalid` | `"AB"` | `ArgumentException` |

## Verification

- [ ] Match self-play prevention works in Create and UpdateDetails
- [ ] UpdateScore clears scores when status is Scheduled
- [ ] UpdateScore sets scores when status is not Scheduled
- [ ] Team abbreviation must be exactly 3 characters
- [ ] All nullable parameters accepted
- [ ] `dotnet test` passes

## Edge Cases to Consider

- Match with team IDs 0 (valid — no guard against zero team IDs, only self-play)
- UpdateScore with very high scores (e.g., 20-0)
- Team abbreviation of exactly 3 characters including special characters
