# Task: League Management Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test League entity factory methods (`Create`, `CreateOfficialPublicLeague`), entry code generation, member management (`AddMember`, `RemoveMember`), prize management, and detail updates.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/LeagueManagementTests.cs` | Create | Factory, member, prize, and update tests |

## Prerequisites

All tests that call `League.Create`, `League.CreateOfficialPublicLeague`, or `League.AddMember` require a `FakeDateTimeProvider`. Set it to a fixed time and create helper dates relative to it:

```csharp
private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
```

## Implementation Steps

### Step 1: Create a helper Season for validation

Many League methods require a `Season` parameter for validation. Create a shared helper using dates relative to the fake time:

```csharp
private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

// Season with start date well in the future (relative to fake time)
private Season CreateFutureSeason() =>
    new(id: 1, name: "2025/26", startDateUtc: _dateTimeProvider.UtcNow.AddMonths(2),
        endDateUtc: _dateTimeProvider.UtcNow.AddMonths(8), isActive: true, numberOfRounds: 38, apiLeagueId: null);

// Entry deadline in the future but before season start
private DateTime FutureDeadline => _dateTimeProvider.UtcNow.AddMonths(1);
```

### Step 2: League.Create factory tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateLeague_WhenValidParametersProvided` | All valid | Properties set, IsFree based on price, HasPrizes = false |
| `Create_ShouldSetIsFreeTrue_WhenPriceIsZero` | `price: 0` | `IsFree = true` |
| `Create_ShouldSetIsFreeFalse_WhenPriceIsPositive` | `price: 10` | `IsFree = false` |
| `Create_ShouldSetIsFreeFalse_WhenPriceIsMinimalPositive` | `price: 0.01m` | `IsFree = false` |
| `Create_ShouldSetCreatedAtUtc_WhenCreated` | Valid input | `CreatedAtUtc` matches `dateTimeProvider.UtcNow` exactly |
| `Create_ShouldSetEntryCodeToNull_WhenCreated` | Valid input | `EntryCode = null` |
| `Create_ShouldSetHasPrizesToFalse_WhenCreated` | Valid input | `HasPrizes = false` |
| `Create_ShouldSetPrizeFundOverrideToNull_WhenCreated` | Valid input | `PrizeFundOverride = null` |
| `Create_ShouldSetPointsForExactScore_WhenProvided` | `pointsForExactScore: 5` | `PointsForExactScore = 5` |
| `Create_ShouldSetPointsForCorrectResult_WhenProvided` | `pointsForCorrectResult: 3` | `PointsForCorrectResult = 3` |
| `Create_ShouldThrowException_WhenNameIsNull` | `name: null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenNameIsEmpty` | `name: ""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenNameIsWhitespace` | `name: " "` | `ArgumentException` |
| `Create_ShouldThrowException_WhenAdministratorUserIdIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenAdministratorUserIdIsEmpty` | `administratorUserId: ""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenAdministratorUserIdIsWhitespace` | `" "` | `ArgumentException` |
| `Create_ShouldThrowException_WhenSeasonIdIsZero` | `seasonId: 0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenSeasonIdIsNegative` | `seasonId: -1` | `ArgumentException` |
| `Create_ShouldThrowException_WhenEntryDeadlineIsInThePast` | Deadline before `dateTimeProvider.UtcNow` | `ArgumentException` |
| `Create_ShouldThrowException_WhenEntryDeadlineIsAfterSeasonStart` | Deadline >= season start | `ArgumentException` |
| `Create_ShouldThrowException_WhenEntryDeadlineEqualToSeasonStart` | Deadline same day as start | `ArgumentException` |

### Step 3: League.CreateOfficialPublicLeague tests

| Test | Scenario | Expected |
|------|----------|----------|
| `CreateOfficialPublicLeague_ShouldSetNameWithOfficialPrefix` | `seasonName: "2025/26"` | `Name = "Official 2025/26 League"` |
| `CreateOfficialPublicLeague_ShouldUsePublicLeagueSettings` | Any valid input | `PointsForExactScore = 5`, `PointsForCorrectResult = 3` |
| `CreateOfficialPublicLeague_ShouldSetPriceCorrectly` | `price: 10m` | `Price = 10m` |
| `CreateOfficialPublicLeague_ShouldThrowException_WhenDeadlineIsInThePast` | Past deadline | `ArgumentException` (validation inherited from Create) |

### Step 4: SetEntryCode tests

| Test | Scenario | Expected |
|------|----------|----------|
| `SetEntryCode_ShouldSetEntryCode_WhenValidCodeProvided` | `"ABC123"` | `EntryCode = "ABC123"` |
| `SetEntryCode_ShouldThrowException_WhenCodeIsNull` | `null` | `ArgumentNullException` |
| `SetEntryCode_ShouldThrowException_WhenCodeIsEmpty` | `""` | `ArgumentException` |
| `SetEntryCode_ShouldThrowException_WhenCodeIsWhitespace` | `" "` | `ArgumentException` |

Note: Entry code generation (`GenerateRandomEntryCode`) now lives in the command handler, not the entity. The domain only validates and stores the code via `SetEntryCode`.

### Step 5: AddMember tests

| Test | Scenario | Expected |
|------|----------|----------|
| `AddMember_ShouldAddMember_WhenUserIdIsValid` | Valid userId, deadline in future | `Members.Count == 1` |
| `AddMember_ShouldCreateMemberWithPendingStatus` | Valid userId | Added member has `Status = Pending` |
| `AddMember_ShouldAddMultipleMembers_WhenDifferentUserIds` | Two different userIds | `Members.Count == 2` |
| `AddMember_ShouldThrowException_WhenUserIdIsNull` | `null` | `ArgumentNullException` |
| `AddMember_ShouldThrowException_WhenUserIdIsEmpty` | `""` | `ArgumentException` |
| `AddMember_ShouldThrowException_WhenUserIdIsWhitespace` | `" "` | `ArgumentException` |
| `AddMember_ShouldThrowException_WhenUserIsAlreadyMember` | Add same userId twice | `InvalidOperationException` |
| `AddMember_ShouldThrowException_WhenDeadlineHasPassed` | League with past deadline | `InvalidOperationException` |

Note: For deadline tests, build the League using the public constructor with an `EntryDeadlineUtc` before `dateTimeProvider.UtcNow`. Pass `_dateTimeProvider` to `AddMember`.

### Step 6: RemoveMember tests

| Test | Scenario | Expected |
|------|----------|----------|
| `RemoveMember_ShouldRemoveMember_WhenMemberExists` | Existing member | `Members.Count` decreases |
| `RemoveMember_ShouldDoNothing_WhenMemberDoesNotExist` | Non-existent userId | No exception, count unchanged |
| `RemoveMember_ShouldLeaveOtherMembers_WhenRemovingOne` | Two members, remove one | Remaining member still present |

### Step 7: UpdateDetails tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDetails_ShouldUpdateAllProperties_WhenValid` | Valid params | Name, Price, EntryDeadlineUtc, PointsForExactScore, PointsForCorrectResult updated |
| `UpdateDetails_ShouldNotChangeId_WhenUpdating` | Valid params | `Id` unchanged |
| `UpdateDetails_ShouldNotChangeSeasonId_WhenUpdating` | Valid params | `SeasonId` unchanged |
| `UpdateDetails_ShouldNotChangeAdministratorUserId_WhenUpdating` | Valid params | `AdministratorUserId` unchanged |
| `UpdateDetails_ShouldNotChangeCreatedAtUtc_WhenUpdating` | Valid params | `CreatedAtUtc` unchanged |
| `UpdateDetails_ShouldThrowException_WhenNameIsNull` | `null` | `ArgumentNullException` |
| `UpdateDetails_ShouldThrowException_WhenNameIsEmpty` | `""` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenDeadlineIsInThePast` | Past deadline | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenDeadlineAfterSeasonStart` | Invalid deadline | `ArgumentException` |

### Step 8: Prize management tests

| Test | Scenario | Expected |
|------|----------|----------|
| `DefinePrizes_ShouldSetHasPrizesTrue_WhenPrizesProvided` | Non-empty list | `HasPrizes = true` |
| `DefinePrizes_ShouldSetHasPrizesFalse_WhenEmptyList` | `[]` | `HasPrizes = false` |
| `DefinePrizes_ShouldSetHasPrizesFalse_WhenNull` | `null` | `HasPrizes = false` |
| `DefinePrizes_ShouldClearExistingPrizes_WhenCalledAgain` | Call twice | Only second set remains |
| `DefinePrizes_ShouldPopulatePrizeSettingsCollection_WhenPrizesProvided` | Non-empty list | `PrizeSettings` contains correct items |
| `DefinePrizes_ShouldClearPrizeSettingsCollection_WhenCalledWithEmpty` | Previously had prizes, call with `[]` | `PrizeSettings` is empty |
| `SetPrizeFundOverride_ShouldSetAmount_WhenValueProvided` | `100m` | `PrizeFundOverride = 100` |
| `SetPrizeFundOverride_ShouldClearAmount_WhenNull` | `null` | `PrizeFundOverride = null` |

### Step 9: ReassignAdministrator tests

| Test | Scenario | Expected |
|------|----------|----------|
| `ReassignAdministrator_ShouldUpdateAdministratorUserId` | Valid userId | Property updated |
| `ReassignAdministrator_ShouldThrowException_WhenUserIdIsNull` | `null` | `ArgumentNullException` |
| `ReassignAdministrator_ShouldThrowException_WhenUserIdIsEmpty` | `""` | `ArgumentException` |
| `ReassignAdministrator_ShouldThrowException_WhenUserIdIsWhitespace` | `" "` | `ArgumentException` |

## Verification

- [ ] All factory validation paths tested (null, empty, whitespace for all string params)
- [ ] IsFree derived correctly from price (0m vs 0.01m boundary)
- [ ] SetEntryCode validates null, empty, whitespace
- [ ] AddMember enforces deadline, duplicate, null/empty/whitespace userId checks
- [ ] AddMember creates members with Pending status
- [ ] RemoveMember is safe for non-existent members
- [ ] UpdateDetails updates only the correct properties (not Id, SeasonId, etc.)
- [ ] Prize management toggles HasPrizes correctly
- [ ] DefinePrizes clears and replaces existing prizes
- [ ] ReassignAdministrator validates null, empty, whitespace
- [ ] `dotnet test` passes

## Edge Cases to Consider

- Entry deadline exactly equal to season start date (should fail — must be "at least one day before")
- Price of exactly 0m vs 0.01m for IsFree boundary
- CreateOfficialPublicLeague inherits all validation from Create
- Adding member with whitespace userId (should fail)
- SetEntryCode with an empty string (should fail)
- UpdateDetails should not modify immutable properties (Id, SeasonId, CreatedAtUtc, AdministratorUserId)
- With `FakeDateTimeProvider`, `CreatedAtUtc` can be asserted exactly and deadline validation is deterministic
