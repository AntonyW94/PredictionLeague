# Task: Supporting Entity Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the remaining entity factory methods and business logic for `Winning`, `LeaguePrizeSetting`, `ApplicationUser`, `PasswordResetToken`, and `RefreshToken`.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/WinningTests.cs` | Create | Winning factory tests |
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/LeaguePrizeSettingTests.cs` | Create | LeaguePrizeSetting factory tests |
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/ApplicationUserTests.cs` | Create | ApplicationUser factory and update tests |
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/PasswordResetTokenTests.cs` | Create | Token generation and expiry tests |
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Models/RefreshTokenTests.cs` | Create | Revocation and computed property tests |

## Implementation Steps

### Step 1: Winning.Create tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateWinning_WhenValidParametersProvided` | All valid | Properties set correctly |
| `Create_ShouldSetAwardedDateUtc_WhenCreated` | Valid input | `AwardedDateUtc` is set (not default) |
| `Create_ShouldThrowException_WhenUserIdIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenUserIdIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenUserIdIsWhitespace` | `" "` | `ArgumentException` |
| `Create_ShouldThrowException_WhenLeaguePrizeSettingIdIsZero` | `0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenLeaguePrizeSettingIdIsNegative` | `-1` | `ArgumentException` |
| `Create_ShouldThrowException_WhenAmountIsNegative` | `-1` | `ArgumentException` |
| `Create_ShouldAllowZeroAmount` | `0m` | No exception |
| `Create_ShouldAcceptNullRoundNumber` | `roundNumber: null` | No exception |
| `Create_ShouldAcceptNullMonth` | `month: null` | No exception |
| `Create_ShouldSetRoundNumber_WhenProvided` | `roundNumber: 5` | `RoundNumber = 5` |
| `Create_ShouldSetMonth_WhenProvided` | `month: 3` | `Month = 3` |
| `Create_ShouldAcceptBothRoundNumberAndMonth` | Both non-null | No exception, both set |

### Step 2: LeaguePrizeSetting.Create tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreatePrizeSetting_WhenValidParametersProvided` | All valid | Properties set correctly |
| `Create_ShouldSetPrizeType_WhenProvided` | `PrizeType.Overall` | `PrizeType` matches input |
| `Create_ShouldThrowException_WhenLeagueIdIsZero` | `0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenLeagueIdIsNegative` | `-1` | `ArgumentException` |
| `Create_ShouldThrowException_WhenRankIsZero` | `0` | `ArgumentException` |
| `Create_ShouldThrowException_WhenRankIsNegative` | `-1` | `ArgumentException` |
| `Create_ShouldThrowException_WhenPrizeAmountIsNegative` | `-1` | `ArgumentException` |
| `Create_ShouldAllowZeroPrizeAmount` | `0m` | No exception |

### Step 3: ApplicationUser.Create tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateUser_WhenValidParametersProvided` | All valid | Properties set, UserName = email |
| `Create_ShouldSetUserNameToEmail` | `"test@example.com"` | `UserName = "test@example.com"` |
| `Create_ShouldSetEmailToProvidedValue` | `"test@example.com"` | `Email = "test@example.com"` |
| `Create_ShouldThrowException_WhenFirstNameIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenFirstNameIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenFirstNameIsWhitespace` | `" "` | `ArgumentException` |
| `Create_ShouldThrowException_WhenLastNameIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenLastNameIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenLastNameIsWhitespace` | `" "` | `ArgumentException` |
| `Create_ShouldThrowException_WhenEmailIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenEmailIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenEmailIsWhitespace` | `" "` | `ArgumentException` |

### Step 4: ApplicationUser.UpdateDetails tests

| Test | Scenario | Expected |
|------|----------|----------|
| `UpdateDetails_ShouldUpdateProperties_WhenValid` | Valid params | FirstName, LastName, PhoneNumber updated |
| `UpdateDetails_ShouldAcceptNullPhoneNumber` | `null` | `PhoneNumber = null` |
| `UpdateDetails_ShouldSetPhoneNumber_WhenProvided` | `"07700900000"` | `PhoneNumber = "07700900000"` |
| `UpdateDetails_ShouldNotChangeEmail_WhenUpdating` | Valid params | `Email` unchanged |
| `UpdateDetails_ShouldNotChangeUserName_WhenUpdating` | Valid params | `UserName` unchanged |
| `UpdateDetails_ShouldThrowException_WhenFirstNameIsNull` | `null` | `ArgumentNullException` |
| `UpdateDetails_ShouldThrowException_WhenFirstNameIsEmpty` | `""` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenFirstNameIsWhitespace` | `" "` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenLastNameIsNull` | `null` | `ArgumentNullException` |
| `UpdateDetails_ShouldThrowException_WhenLastNameIsEmpty` | `""` | `ArgumentException` |
| `UpdateDetails_ShouldThrowException_WhenLastNameIsWhitespace` | `" "` | `ArgumentException` |

### Step 5: PasswordResetToken.Create tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldGenerateToken_WhenValidUserIdProvided` | Valid userId | Token is not null or empty |
| `Create_ShouldGenerateUrlSafeToken_WhenCreated` | Valid userId | Token contains no `+`, `/`, or `=` |
| `Create_ShouldSetUserId_WhenCreated` | `"user-1"` | `UserId = "user-1"` |
| `Create_ShouldSetCreatedAtUtc_WhenCreated` | Valid userId | `CreatedAtUtc` is set (approximately now) |
| `Create_ShouldSetExpiryToOneHour_WhenDefaultExpiryUsed` | Default param | `ExpiresAtUtc` ~1 hour from now |
| `Create_ShouldSetCustomExpiry_WhenExpiryHoursProvided` | `expiryHours: 24` | `ExpiresAtUtc` ~24 hours from now |
| `Create_ShouldThrowException_WhenUserIdIsNull` | `null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenUserIdIsEmpty` | `""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenUserIdIsWhitespace` | `" "` | `ArgumentException` |
| `Create_ShouldGenerateUniqueTokens_WhenCalledMultipleTimes` | Two calls | Different tokens |

### Step 6: PasswordResetToken.IsExpired tests

| Test | Scenario | Expected |
|------|----------|----------|
| `IsExpired_ShouldReturnFalse_WhenExpiryIsInFuture` | ExpiresAtUtc in future | `false` |
| `IsExpired_ShouldReturnTrue_WhenExpiryIsInPast` | ExpiresAtUtc in past | `true` |

Note: The boundary — `IsExpired` uses `>` (strictly greater than), meaning at the exact expiry moment the token is NOT expired. This is documented but not easily testable due to timing.

Use the public constructor to set up tokens with specific expiry times for IsExpired tests.

### Step 7: RefreshToken tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Revoke_ShouldSetRevokedTimestamp_WhenCalled` | Active token | `Revoked` is not null |
| `Revoke_ShouldMakeTokenInactive_WhenTokenWasActive` | Active token | `IsActive` becomes false |
| `IsExpired_ShouldReturnFalse_WhenExpiresInFuture` | Future expiry | `false` |
| `IsExpired_ShouldReturnTrue_WhenExpiresInPast` | Past expiry | `true` |
| `IsActive_ShouldReturnTrue_WhenNotRevokedAndNotExpired` | Valid, not revoked | `true` |
| `IsActive_ShouldReturnFalse_WhenRevoked` | Revoked token | `false` |
| `IsActive_ShouldReturnFalse_WhenExpired` | Past expiry, not revoked | `false` |
| `IsActive_ShouldReturnFalse_WhenBothRevokedAndExpired` | Revoked + past expiry | `false` |

Note: `RefreshToken.IsExpired` uses `>=` (expired AT the exact moment), while `PasswordResetToken.IsExpired` uses `>` (NOT expired at the exact moment). This difference is worth documenting in the tests.

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

public class RefreshTokenTests
{
    [Fact]
    public void IsActive_ShouldReturnFalse_WhenBothRevokedAndExpired()
    {
        // Arrange
        var token = new RefreshToken
        {
            UserId = "user-1",
            Token = "test-token",
            Created = DateTime.UtcNow.AddDays(-7),
            Expires = DateTime.UtcNow.AddDays(-1),
            Revoked = DateTime.UtcNow.AddDays(-2)
        };

        // Assert
        token.IsActive.Should().BeFalse();
    }
}
```

## Verification

- [ ] All factory validation tests pass (null, empty, whitespace for all string params)
- [ ] Winning allows zero amount and accepts null round number and month
- [ ] Winning sets AwardedDateUtc on creation
- [ ] LeaguePrizeSetting validates leagueId, rank (zero, negative), and negative prizeAmount
- [ ] LeaguePrizeSetting preserves PrizeType enum value
- [ ] ApplicationUser.UserName always set to email
- [ ] ApplicationUser.UpdateDetails does not change Email or UserName
- [ ] PasswordResetToken generates URL-safe tokens (no +, /, =)
- [ ] PasswordResetToken generates unique tokens on each call
- [ ] PasswordResetToken stores UserId and CreatedAtUtc
- [ ] Token expiry computed properties work correctly for both token types
- [ ] RefreshToken.IsActive combines revocation and expiry checks (all 4 combinations)
- [ ] RefreshToken.Revoke sets Revoked timestamp and makes token inactive
- [ ] `dotnet test` passes

## Edge Cases to Consider

- PasswordResetToken expiry boundary (exactly at ExpiresAtUtc — currently `>` means equal to boundary is not expired)
- RefreshToken expiry boundary (uses `>=` — equal to boundary IS expired)
- Winning with zero amount (valid — e.g., placeholder prizes)
- ApplicationUser inherits from IdentityUser — some properties come from the base class
- Revoked + expired RefreshToken (both conditions false — still inactive)
