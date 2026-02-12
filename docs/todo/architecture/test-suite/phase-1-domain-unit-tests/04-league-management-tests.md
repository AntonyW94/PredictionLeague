# Task: League Management Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test League entity factory methods (`Create`, `CreateOfficialPublicLeague`), entry code generation, member management (`AddMember`, `RemoveMember`), prize management, and detail updates.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/PredictionLeague.Domain.Tests/Models/LeagueManagementTests.cs` | Create | Factory, member, prize, and update tests |

## Implementation Steps

### Step 1: Create a helper Season for validation

Many League methods require a `Season` parameter for validation. Create a shared helper:

```csharp
// Season with start date well in the future
private static Season CreateFutureSeason() =>
    new(id: 1, name: "2025/26", startDateUtc: DateTime.UtcNow.AddMonths(2),
        endDateUtc: DateTime.UtcNow.AddMonths(8), isActive: true, numberOfRounds: 38, apiLeagueId: null);
```

### Step 2: League.Create factory tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateLeague_WhenValidParametersProvided` | All valid | Properties set, IsFree based on price, HasPrizes = false |
| `Create_ShouldSetIsFreeTrue_WhenPriceIsZero` | `price: 0` | `IsFree = true` |
| `Create_ShouldSetIsFreeFalse_WhenPriceIsPositive` | `price: 10` | `IsFree = false` |
| `Create_ShouldThrowException_WhenNameIsNull` | `name: null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenNameIsWhitespace` | `name: " "` | `ArgumentException` |
| `Create_ShouldThrowException_WhenAdministratorUserIdIsEmpty` | `administratorUserId: ""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenSeasonIdIsZero` | `seasonId: 0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenEntryDeadlineIsInThePast` | Past deadline | `ArgumentException` |
| `Create_ShouldThrowException_WhenEntryDeadlineIsAfterSeasonStart` | Deadline >= season start | `ArgumentException` |
| `Create_ShouldSetEntryCodeToNull_WhenCreated` | Valid input | `EntryCode = null` |
| `Create_ShouldSetHasPrizesToFalse_WhenCreated` | Valid input | `HasPrizes = false` |

### Step 3: League.CreateOfficialPublicLeague tests

| Test | Scenario | Expected |
|------|----------|----------|
| `CreateOfficialPublicLeague_ShouldSetNameWithOfficialPrefix` | `seasonName: "2025/26"` | `Name = "Official 2025/26 League"` |
| `CreateOfficialPublicLeague_ShouldUsePublicLeagueSettings` | Any valid input | `PointsForExactScore` and `PointsForCorrectResult` from `PublicLeagueSettings` |

### Step 4: GenerateEntryCode tests (requires NSubstitute)

| Test | Scenario | Expected |
|------|----------|----------|
| `GenerateEntryCode_ShouldSetSixCharacterCode_WhenCodeIsUnique` | Mock returns `true` first time | `EntryCode.Length == 6` |
| `GenerateEntryCode_ShouldContainOnlyAlphanumericCharacters` | Mock returns `true` | Matches `^[A-Z0-9]{6}$` |
| `GenerateEntryCode_ShouldRetryUntilUnique_WhenFirstCodeIsNotUnique` | Mock returns `false` then `true` | `IsCodeUnique` called at least twice |

### Step 5: AddMember tests

| Test | Scenario | Expected |
|------|----------|----------|
| `AddMember_ShouldAddMember_WhenUserIdIsValid` | Valid userId, deadline in future | `Members.Count == 1` |
| `AddMember_ShouldThrowException_WhenUserIdIsNull` | `null` | `ArgumentNullException` |
| `AddMember_ShouldThrowException_WhenUserIsAlreadyMember` | Add same userId twice | `InvalidOperationException` |
| `AddMember_ShouldThrowException_WhenDeadlineHasPassed` | League with past deadline | `InvalidOperationException` |

Note: For deadline tests, build the League using the public constructor with a past `EntryDeadlineUtc`.

### Step 6: RemoveMember tests

| Test | Scenario | Expected |
|------|----------|----------|
| `RemoveMember_ShouldRemoveMember_WhenMemberExists` | Existing member | `Members.Count` decreases |
| `RemoveMember_ShouldDoNothing_WhenMemberDoesNotExist` | Non-existent userId | No exception, count unchanged |

### Step 7: UpdateDetails tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDetails_ShouldUpdateAllProperties_WhenValid` | Valid params | All properties updated |
| `UpdateDetails_ShouldThrowException_WhenNameIsEmpty` | `""` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenDeadlineAfterSeasonStart` | Invalid deadline | `ArgumentException` |

### Step 8: Prize management tests

| Test | Scenario | Expected |
|------|----------|----------|
| `DefinePrizes_ShouldSetHasPrizesTrue_WhenPrizesProvided` | Non-empty list | `HasPrizes = true` |
| `DefinePrizes_ShouldSetHasPrizesFalse_WhenEmptyList` | `[]` | `HasPrizes = false` |
| `DefinePrizes_ShouldSetHasPrizesFalse_WhenNull` | `null` | `HasPrizes = false` |
| `DefinePrizes_ShouldClearExistingPrizes_WhenCalledAgain` | Call twice | Only second set remains |
| `SetPrizeFundOverride_ShouldSetAmount_WhenValueProvided` | `100m` | `PrizeFundOverride = 100` |
| `SetPrizeFundOverride_ShouldClearAmount_WhenNull` | `null` | `PrizeFundOverride = null` |

### Step 9: ReassignAdministrator tests

| Test | Scenario | Expected |
|------|----------|----------|
| `ReassignAdministrator_ShouldUpdateAdministratorUserId` | Valid userId | Property updated |
| `ReassignAdministrator_ShouldThrowException_WhenUserIdIsEmpty` | `""` | `ArgumentException` |

## Verification

- [ ] All factory validation paths tested
- [ ] IsFree derived correctly from price
- [ ] Entry code generation retries on duplicates
- [ ] AddMember enforces deadline and duplicate checks
- [ ] Prize management toggles HasPrizes correctly
- [ ] `dotnet test` passes

## Edge Cases to Consider

- Entry deadline exactly equal to season start date (should fail — must be "at least one day before")
- Price of exactly 0m vs 0.01m for IsFree boundary
- Adding member with whitespace userId (should fail)
- GenerateEntryCode when uniqueness checker always returns false (infinite loop — document as known limitation)
