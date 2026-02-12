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

## Implementation Steps

### Step 1: LeagueMember.Create factory tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateMember_WhenValidParametersProvided` | Valid input | Properties set correctly |
| `Create_ShouldSetStatusToPending_WhenCreated` | Valid input | `Status = LeagueMemberStatus.Pending` |
| `Create_ShouldSetIsAlertDismissedToFalse_WhenCreated` | Valid input | `IsAlertDismissed = false` |
| `Create_ShouldSetApprovedAtUtcToNull_WhenCreated` | Valid input | `ApprovedAtUtc = null` |
| `Create_ShouldThrowException_WhenLeagueIdIsZero` | `leagueId: 0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenLeagueIdIsNegative` | `leagueId: -1` | `ArgumentException` |
| `Create_ShouldThrowException_WhenUserIdIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenUserIdIsWhitespace` | `" "` | `ArgumentException` |

### Step 2: Approve tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Approve_ShouldSetStatusToApproved_WhenPending` | Status = Pending | `Status = Approved` |
| `Approve_ShouldSetApprovedAtUtc_WhenPending` | Status = Pending | `ApprovedAtUtc` is not null |
| `Approve_ShouldThrowException_WhenAlreadyApproved` | Status = Approved | `InvalidOperationException` |
| `Approve_ShouldThrowException_WhenRejected` | Status = Rejected | `InvalidOperationException` |

### Step 3: Reject tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Reject_ShouldSetStatusToRejected_WhenPending` | Status = Pending | `Status = Rejected` |
| `Reject_ShouldResetIsAlertDismissed_WhenPending` | Status = Pending | `IsAlertDismissed = false` |
| `Reject_ShouldThrowException_WhenAlreadyApproved` | Status = Approved | `InvalidOperationException` |
| `Reject_ShouldThrowException_WhenAlreadyRejected` | Status = Rejected | `InvalidOperationException` |

### Step 4: DismissAlert test

| Test | Scenario | Expected |
|------|----------|----------|
| `DismissAlert_ShouldSetIsAlertDismissedToTrue_WhenCalled` | Any member | `IsAlertDismissed = true` |

## Code Patterns to Follow

```csharp
public class LeagueMemberTests
{
    [Fact]
    public void Approve_ShouldThrowException_WhenAlreadyApproved()
    {
        // Arrange — use public constructor to set up non-Pending status
        var member = new LeagueMember(
            leagueId: 1, userId: "user-1",
            status: LeagueMemberStatus.Approved,
            isAlertDismissed: false,
            joinedAtUtc: DateTime.UtcNow,
            approvedAtUtc: DateTime.UtcNow,
            roundResults: null);

        // Act
        var act = () => member.Approve();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*pending*");
    }
}
```

## Verification

- [ ] Factory creates Pending member with correct defaults
- [ ] Approve only works from Pending status
- [ ] Reject only works from Pending status
- [ ] Reject resets IsAlertDismissed
- [ ] DismissAlert always sets flag to true
- [ ] `dotnet test` passes

## Edge Cases to Consider

- Calling DismissAlert when already dismissed (should be idempotent)
- Approve/Reject from every non-Pending status (Approved, Rejected)
