# Task: LeagueMember Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the `LeagueMember` entity factory method and status transition methods (`Approve`, `Reject`, `DismissAlert`).

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/LeagueMemberTests.cs` | Create | All LeagueMember unit tests |

## Prerequisites

`LeagueMember.Create` and `Approve` now accept `IDateTimeProvider`. Use a `FakeDateTimeProvider` to assert exact timestamps:

```csharp
private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
```

## Implementation Steps

### Step 1: LeagueMember.Create factory tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateMember_WhenValidParametersProvided` | Valid input | Properties set correctly |
| `Create_ShouldSetStatusToPending_WhenCreated` | Valid input | `Status = LeagueMemberStatus.Pending` |
| `Create_ShouldSetIsAlertDismissedToFalse_WhenCreated` | Valid input | `IsAlertDismissed = false` |
| `Create_ShouldSetApprovedAtUtcToNull_WhenCreated` | Valid input | `ApprovedAtUtc = null` |
| `Create_ShouldSetJoinedAtUtc_WhenCreated` | Valid input | `JoinedAtUtc` matches `dateTimeProvider.UtcNow` exactly |
| `Create_ShouldInitialiseEmptyRoundResultsCollection_WhenCreated` | Valid input | `RoundResults` is empty |
| `Create_ShouldThrowException_WhenLeagueIdIsZero` | `leagueId: 0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenLeagueIdIsNegative` | `leagueId: -1` | `ArgumentException` |
| `Create_ShouldThrowException_WhenUserIdIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenUserIdIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenUserIdIsWhitespace` | `" "` | `ArgumentException` |

### Step 2: Approve tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Approve_ShouldSetStatusToApproved_WhenPending` | Status = Pending | `Status = Approved` |
| `Approve_ShouldSetApprovedAtUtc_WhenPending` | Status = Pending | `ApprovedAtUtc` matches `dateTimeProvider.UtcNow` exactly |
| `Approve_ShouldThrowException_WhenAlreadyApproved` | Status = Approved | `InvalidOperationException` with "pending" |
| `Approve_ShouldThrowException_WhenRejected` | Status = Rejected | `InvalidOperationException` with "pending" |

### Step 3: Reject tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Reject_ShouldSetStatusToRejected_WhenPending` | Status = Pending | `Status = Rejected` |
| `Reject_ShouldResetIsAlertDismissed_WhenPending` | Status = Pending, IsAlertDismissed = true | `IsAlertDismissed = false` |
| `Reject_ShouldSetIsAlertDismissedToFalse_WhenAlreadyFalse` | Status = Pending, IsAlertDismissed = false | `IsAlertDismissed = false` |
| `Reject_ShouldThrowException_WhenAlreadyApproved` | Status = Approved | `InvalidOperationException` with "pending" |
| `Reject_ShouldThrowException_WhenAlreadyRejected` | Status = Rejected | `InvalidOperationException` with "pending" |

### Step 4: DismissAlert tests

| Test | Scenario | Expected |
|------|----------|----------|
| `DismissAlert_ShouldSetIsAlertDismissedToTrue_WhenCalled` | Any member | `IsAlertDismissed = true` |
| `DismissAlert_ShouldBeIdempotent_WhenCalledMultipleTimes` | Already dismissed | `IsAlertDismissed = true` (no error) |

## Code Patterns to Follow

```csharp
public class LeagueMemberTests
{
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Create_ShouldSetJoinedAtUtc_WhenCreated()
    {
        // Act
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);

        // Assert
        member.JoinedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void Approve_ShouldSetApprovedAtUtc_WhenPending()
    {
        // Arrange
        var member = LeagueMember.Create(1, "user-1", _dateTimeProvider);
        var approvalTime = new DateTime(2025, 6, 16, 10, 0, 0, DateTimeKind.Utc);
        _dateTimeProvider.UtcNow = approvalTime;

        // Act
        member.Approve(_dateTimeProvider);

        // Assert
        member.ApprovedAtUtc.Should().Be(approvalTime);
    }

    [Fact]
    public void Approve_ShouldThrowException_WhenAlreadyApproved()
    {
        // Arrange — use public constructor to set up non-Pending status
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: LeagueMemberStatus.Approved,
            isAlertDismissed: false,
            joinedAtUtc: _dateTimeProvider.UtcNow,
            approvedAtUtc: _dateTimeProvider.UtcNow,
            roundResults: null);

        // Act
        var act = () => member.Approve(_dateTimeProvider);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*pending*");
    }

    [Fact]
    public void Reject_ShouldResetIsAlertDismissed_WhenPending()
    {
        // Arrange — use public constructor with IsAlertDismissed = true
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: LeagueMemberStatus.Pending,
            isAlertDismissed: true,
            joinedAtUtc: _dateTimeProvider.UtcNow,
            approvedAtUtc: null,
            roundResults: null);

        // Act
        member.Reject();

        // Assert
        member.IsAlertDismissed.Should().BeFalse();
    }
}
```

## Verification

- [ ] Factory creates Pending member with correct defaults (IsAlertDismissed = false, ApprovedAtUtc = null)
- [ ] JoinedAtUtc is set on creation
- [ ] RoundResults collection initialised empty
- [ ] Approve only works from Pending status (throws for Approved and Rejected)
- [ ] Approve sets ApprovedAtUtc
- [ ] Reject only works from Pending status (throws for Approved and Rejected)
- [ ] Reject always sets IsAlertDismissed to false (including when already false)
- [ ] DismissAlert always sets flag to true and is idempotent
- [ ] All factory validation paths tested (null, empty, whitespace for userId; zero, negative for leagueId)
- [ ] `dotnet test` passes

## Edge Cases to Consider

- Calling DismissAlert when already dismissed (should be idempotent)
- Approve/Reject from every non-Pending status (Approved, Rejected)
- Reject resets IsAlertDismissed even if it was true (constructor allows setting it to true for test setup)
- With `FakeDateTimeProvider`, `JoinedAtUtc` and `ApprovedAtUtc` can be asserted exactly
