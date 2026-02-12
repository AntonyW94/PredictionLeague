# Task: Supporting Entity Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the remaining entity factory methods and business logic for `Winning`, `LeaguePrizeSetting`, `ApplicationUser`, `PasswordResetToken`, and `RefreshToken`.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/PredictionLeague.Domain.Tests/Models/WinningTests.cs` | Create | Winning factory tests |
| `tests/PredictionLeague.Domain.Tests/Models/LeaguePrizeSettingTests.cs` | Create | LeaguePrizeSetting factory tests |
| `tests/PredictionLeague.Domain.Tests/Models/ApplicationUserTests.cs` | Create | ApplicationUser factory and update tests |
| `tests/PredictionLeague.Domain.Tests/Models/PasswordResetTokenTests.cs` | Create | Token generation and expiry tests |
| `tests/PredictionLeague.Domain.Tests/Models/RefreshTokenTests.cs` | Create | Revocation and computed property tests |

## Implementation Steps

### Step 1: Winning.Create tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateWinning_WhenValidParametersProvided` | All valid | Properties set correctly |
| `Create_ShouldThrowException_WhenUserIdIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenLeaguePrizeSettingIdIsZero` | `0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenAmountIsNegative` | `-1` | `ArgumentException` |
| `Create_ShouldAllowZeroAmount` | `0m` | No exception |
| `Create_ShouldAcceptNullRoundNumber` | `null` | No exception |
| `Create_ShouldAcceptNullMonth` | `null` | No exception |

### Step 2: LeaguePrizeSetting.Create tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreatePrizeSetting_WhenValidParametersProvided` | All valid | Properties set correctly |
| `Create_ShouldThrowException_WhenLeagueIdIsZero` | `0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenRankIsZero` | `0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenPrizeAmountIsNegative` | `-1` | `ArgumentException` |
| `Create_ShouldAllowZeroPrizeAmount` | `0m` | No exception |

### Step 3: ApplicationUser.Create tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateUser_WhenValidParametersProvided` | All valid | Properties set, UserName = email |
| `Create_ShouldSetUserNameToEmail` | `"test@example.com"` | `UserName = "test@example.com"` |
| `Create_ShouldThrowException_WhenFirstNameIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenLastNameIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenEmailIsEmpty` | `""` | `ArgumentException` |

### Step 4: ApplicationUser.UpdateDetails tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDetails_ShouldUpdateProperties_WhenValid` | Valid params | FirstName, LastName, PhoneNumber updated |
| `UpdateDetails_ShouldAcceptNullPhoneNumber` | `null` | `PhoneNumber = null` |
| `UpdateDetails_ShouldThrowException_WhenFirstNameIsEmpty` | `""` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenLastNameIsEmpty` | `""` | `ArgumentException` |

### Step 5: PasswordResetToken.Create tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldGenerateToken_WhenValidUserIdProvided` | Valid userId | Token is not null or empty |
| `Create_ShouldGenerateUrlSafeToken_WhenCreated` | Valid userId | Token contains no `+`, `/`, or `=` |
| `Create_ShouldSetExpiryToOneHour_WhenDefaultExpiryUsed` | Default param | `ExpiresAtUtc` ~1 hour from now |
| `Create_ShouldSetCustomExpiry_WhenExpiryHoursProvided` | `expiryHours: 24` | `ExpiresAtUtc` ~24 hours from now |
| `Create_ShouldThrowException_WhenUserIdIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldGenerateUniqueTokens_WhenCalledMultipleTimes` | Two calls | Different tokens |

### Step 6: PasswordResetToken.IsExpired tests

| Test | Scenario | Expected |
|------|----------|----------|
| `IsExpired_ShouldReturnFalse_WhenExpiryIsInFuture` | ExpiresAtUtc in future | `false` |
| `IsExpired_ShouldReturnTrue_WhenExpiryIsInPast` | ExpiresAtUtc in past | `true` |

Use the public constructor to set up tokens with specific expiry times for IsExpired tests.

### Step 7: RefreshToken tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Revoke_ShouldSetRevokedTimestamp_WhenCalled` | Active token | `Revoked` is not null |
| `IsExpired_ShouldReturnFalse_WhenExpiresInFuture` | Future expiry | `false` |
| `IsExpired_ShouldReturnTrue_WhenExpiresInPast` | Past expiry | `true` |
| `IsActive_ShouldReturnTrue_WhenNotRevokedAndNotExpired` | Valid, not revoked | `true` |
| `IsActive_ShouldReturnFalse_WhenRevoked` | Revoked token | `false` |
| `IsActive_ShouldReturnFalse_WhenExpired` | Past expiry, not revoked | `false` |

## Code Patterns to Follow

```csharp
public class PasswordResetTokenTests
{
    [Fact]
    public void Create_ShouldGenerateUrlSafeToken_WhenCreated()
    {
        // Act
        var token = PasswordResetToken.Create("user-1");

        // Assert
        token.Token.Should().NotContain("+");
        token.Token.Should().NotContain("/");
        token.Token.Should().NotContain("=");
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenExpiryIsInPast()
    {
        // Arrange — use public constructor with past expiry
        var token = new PasswordResetToken(
            token: "test-token",
            userId: "user-1",
            createdAtUtc: DateTime.UtcNow.AddHours(-2),
            expiresAtUtc: DateTime.UtcNow.AddHours(-1));

        // Assert
        token.IsExpired.Should().BeTrue();
    }
}
```

## Verification

- [ ] All factory validation tests pass
- [ ] PasswordResetToken generates URL-safe tokens (no +, /, =)
- [ ] PasswordResetToken generates unique tokens on each call
- [ ] Token expiry computed properties work correctly
- [ ] RefreshToken.IsActive combines revocation and expiry checks
- [ ] ApplicationUser.UserName set to email
- [ ] `dotnet test` passes

## Edge Cases to Consider

- PasswordResetToken expiry boundary (exactly at ExpiresAtUtc — currently `>` means equal to boundary is not expired)
- RefreshToken expiry boundary (uses `>=` — equal to boundary IS expired)
- Winning with zero amount (valid — e.g., placeholder prizes)
