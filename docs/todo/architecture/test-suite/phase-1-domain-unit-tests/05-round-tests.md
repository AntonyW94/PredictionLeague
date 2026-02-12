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
| `Create_ShouldSetCompletedDateUtcToNull_WhenCreated` | Valid input | `CompletedDateUtc = null` |
| `Create_ShouldInitialiseEmptyMatchesCollection_WhenCreated` | Valid input | `Matches` is empty |
| `Create_ShouldAcceptNullApiRoundName` | `apiRoundName: null` | No exception |
| `Create_ShouldSetApiRoundName_WhenProvided` | `apiRoundName: "GW1"` | `ApiRoundName = "GW1"` |
| `Create_ShouldThrowException_WhenSeasonIdIsZero` | `seasonId: 0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenSeasonIdIsNegative` | `seasonId: -1` | `ArgumentException` |
| `Create_ShouldThrowException_WhenRoundNumberIsZero` | `roundNumber: 0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenRoundNumberIsNegative` | `roundNumber: -1` | `ArgumentException` |
| `Create_ShouldThrowException_WhenStartDateIsDefault` | `startDateUtc: default` | `ArgumentException` |
| `Create_ShouldThrowException_WhenDeadlineIsDefault` | `deadlineUtc: default` | `ArgumentException` |
| `Create_ShouldThrowException_WhenDeadlineIsAfterStartDate` | `deadline > startDate` | `ArgumentException` |
| `Create_ShouldThrowException_WhenDeadlineEqualsStartDate` | `deadline == startDate` | `ArgumentException` |

Note: The validation rule is `deadline < startDate` (predictions must be submitted before the round starts).

### Step 2: UpdateStatus tests

Test all transition paths to/from `Completed`:

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateStatus_ShouldSetCompletedDate_WhenTransitioningFromDraftToCompleted` | Draft → Completed | `CompletedDateUtc` is set |
| `UpdateStatus_ShouldSetCompletedDate_WhenTransitioningFromPublishedToCompleted` | Published → Completed | `CompletedDateUtc` is set |
| `UpdateStatus_ShouldSetCompletedDate_WhenTransitioningFromInProgressToCompleted` | InProgress → Completed | `CompletedDateUtc` is set |
| `UpdateStatus_ShouldClearCompletedDate_WhenTransitioningFromCompletedToDraft` | Completed → Draft | `CompletedDateUtc = null` |
| `UpdateStatus_ShouldClearCompletedDate_WhenTransitioningFromCompletedToPublished` | Completed → Published | `CompletedDateUtc = null` |
| `UpdateStatus_ShouldClearCompletedDate_WhenTransitioningFromCompletedToInProgress` | Completed → InProgress | `CompletedDateUtc = null` |
| `UpdateStatus_ShouldNotSetCompletedDate_WhenTransitioningBetweenNonCompletedStatuses` | Draft → Published | `CompletedDateUtc = null` |
| `UpdateStatus_ShouldNotResetCompletedDate_WhenAlreadyCompletedAndStaysCompleted` | Completed → Completed | `CompletedDateUtc` remains set |
| `UpdateStatus_ShouldUpdateStatusProperty_WhenCalled` | Any valid status | `Status` property updated |

### Step 3: AddMatch tests

| Test | Scenario | Expected |
|------|----------|----------|
| `AddMatch_ShouldAddMatch_WhenValidTeamsProvided` | Different team IDs | `Matches.Count` increases |
| `AddMatch_ShouldCreateMatchWithScheduledStatus_WhenAdded` | Valid teams | Added match has `Status = Scheduled` |
| `AddMatch_ShouldSetCorrectTeamIds_WhenAdded` | homeTeamId: 1, awayTeamId: 2 | Match has correct team IDs |
| `AddMatch_ShouldThrowException_WhenTeamPlaysItself` | `homeTeamId == awayTeamId` | `ArgumentException` with "cannot play against itself" |
| `AddMatch_ShouldThrowException_WhenDuplicateMatchExists` | Same home/away pair already added | `ArgumentException` with "already exists" |
| `AddMatch_ShouldAllowReverseFixture` | A vs B exists, add B vs A | No exception (different fixture) |
| `AddMatch_ShouldAddMultipleMatches_WhenDifferentTeamPairs` | Several matches | All added successfully |

### Step 4: RemoveMatch tests

| Test | Scenario | Expected |
|------|----------|----------|
| `RemoveMatch_ShouldRemoveMatch_WhenMatchExists` | Existing match ID | `Matches.Count` decreases |
| `RemoveMatch_ShouldDoNothing_WhenMatchDoesNotExist` | Non-existent match ID | No exception, count unchanged |
| `RemoveMatch_ShouldLeaveOtherMatches_WhenRemovingOne` | Two matches, remove one | Other match still present |

### Step 5: UpdateDetails tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDetails_ShouldUpdateAllProperties_WhenValid` | All valid | RoundNumber, StartDateUtc, DeadlineUtc, Status, ApiRoundName updated |
| `UpdateDetails_ShouldNotChangeSeasonId_WhenUpdating` | Valid params | `SeasonId` unchanged |
| `UpdateDetails_ShouldThrowException_WhenRoundNumberIsZero` | `roundNumber: 0` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenRoundNumberIsNegative` | `roundNumber: -1` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenStartDateIsDefault` | `default(DateTime)` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenDeadlineIsDefault` | `default(DateTime)` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenDeadlineAfterStartDate` | Invalid dates | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenDeadlineEqualsStartDate` | Equal dates | `ArgumentException` |

### Step 6: UpdateLastReminderSent tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateLastReminderSent_ShouldSetTimestamp_WhenCalled` | Any round | `LastReminderSentUtc` is not null |
| `UpdateLastReminderSent_ShouldUpdateTimestamp_WhenCalledAgain` | Call twice | `LastReminderSentUtc` updates |

## Verification

- [ ] Factory creates round in Draft status with null CompletedDateUtc and LastReminderSentUtc
- [ ] All validation paths throw correct exceptions (zero, negative, default dates)
- [ ] Deadline boundary: equal to start date should fail
- [ ] CompletedDateUtc set when transitioning TO Completed from every non-Completed status
- [ ] CompletedDateUtc cleared when transitioning FROM Completed to every non-Completed status
- [ ] CompletedDateUtc stays set when Completed → Completed
- [ ] Non-Completed to non-Completed transitions don't affect CompletedDateUtc
- [ ] Self-play prevention works in AddMatch
- [ ] Duplicate match prevention works in AddMatch
- [ ] Reverse fixtures allowed (A vs B and B vs A are different matches)
- [ ] UpdateDetails validates same rules as Create
- [ ] `dotnet test` passes

## Edge Cases to Consider

- Deadline exactly equal to start date (should fail — deadline must be before start)
- Adding a match after transitioning to Completed status (no guard against this currently)
- RemoveMatch with ID 0 (should do nothing gracefully)
- UpdateStatus from Completed to Completed (CompletedDateUtc should not be reset)
- UpdateDetails does not change SeasonId
