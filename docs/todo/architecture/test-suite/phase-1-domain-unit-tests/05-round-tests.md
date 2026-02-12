# Task: Round Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the `Round` entity factory method, status transitions, match management, and validation.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/RoundTests.cs` | Create | All Round unit tests |

## Implementation Steps

### Step 1: Round.Create factory tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateRound_WhenValidParametersProvided` | All valid | Properties set, Status = Draft |
| `Create_ShouldSetStatusToDraft_WhenCreated` | Valid input | `Status = RoundStatus.Draft` |
| `Create_ShouldSetLastReminderSentUtcToNull_WhenCreated` | Valid input | `LastReminderSentUtc = null` |
| `Create_ShouldAcceptNullApiRoundName` | `apiRoundName: null` | No exception |
| `Create_ShouldThrowException_WhenSeasonIdIsZero` | `seasonId: 0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenSeasonIdIsNegative` | `seasonId: -1` | `ArgumentException` |
| `Create_ShouldThrowException_WhenRoundNumberIsZero` | `roundNumber: 0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenStartDateIsDefault` | `startDateUtc: default` | `ArgumentException` |
| `Create_ShouldThrowException_WhenDeadlineIsDefault` | `deadlineUtc: default` | `ArgumentException` |
| `Create_ShouldThrowException_WhenDeadlineIsAfterStartDate` | `deadline >= startDate` | `ArgumentException` |

Note: The validation rule is `deadline < startDate` (predictions must be submitted before the round starts).

### Step 2: UpdateStatus tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateStatus_ShouldSetCompletedDate_WhenTransitioningToCompleted` | Any status → Completed | `CompletedDateUtc` is set |
| `UpdateStatus_ShouldClearCompletedDate_WhenTransitioningFromCompleted` | Completed → InProgress | `CompletedDateUtc = null` |
| `UpdateStatus_ShouldNotSetCompletedDate_WhenTransitioningBetweenNonCompletedStatuses` | Draft → Published | `CompletedDateUtc = null` |
| `UpdateStatus_ShouldUpdateStatus_WhenCalled` | Any valid status | `Status` property updated |

### Step 3: AddMatch tests

| Test | Scenario | Expected |
|------|----------|----------|
| `AddMatch_ShouldAddMatch_WhenValidTeamsProvided` | Different team IDs | `Matches.Count` increases |
| `AddMatch_ShouldThrowException_WhenTeamPlaysItself` | `homeTeamId == awayTeamId` | `ArgumentException` with "cannot play against itself" |
| `AddMatch_ShouldThrowException_WhenDuplicateMatchExists` | Same home/away pair already added | `ArgumentException` with "already exists" |
| `AddMatch_ShouldAllowReverseFixture` | A vs B exists, add B vs A | No exception (different fixture) |

### Step 4: RemoveMatch tests

| Test | Scenario | Expected |
|------|----------|----------|
| `RemoveMatch_ShouldRemoveMatch_WhenMatchExists` | Existing match ID | `Matches.Count` decreases |
| `RemoveMatch_ShouldDoNothing_WhenMatchDoesNotExist` | Non-existent match ID | No exception, count unchanged |

### Step 5: UpdateDetails tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDetails_ShouldUpdateAllProperties_WhenValid` | All valid | Properties updated |
| `UpdateDetails_ShouldThrowException_WhenRoundNumberIsZero` | `roundNumber: 0` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenDeadlineAfterStartDate` | Invalid dates | `ArgumentException` |

### Step 6: UpdateLastReminderSent test

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateLastReminderSent_ShouldSetTimestamp_WhenCalled` | Any round | `LastReminderSentUtc` is not null |

## Verification

- [ ] Factory creates round in Draft status
- [ ] All validation paths throw correct exceptions
- [ ] CompletedDateUtc set/cleared correctly on status transitions
- [ ] Self-play prevention works
- [ ] Duplicate match prevention works
- [ ] Reverse fixtures allowed (A vs B and B vs A are different matches)
- [ ] `dotnet test` passes

## Edge Cases to Consider

- Deadline exactly equal to start date (should fail — deadline must be before start)
- Adding a match after transitioning to Completed status (no guard against this currently)
- RemoveMatch with ID 0 (should do nothing gracefully)
