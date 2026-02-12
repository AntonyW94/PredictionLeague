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

## Prerequisites

`Winning.Create`, `PasswordResetToken.Create`, `PasswordResetToken.IsExpired`, `RefreshToken.IsExpired`/`IsActive`, and `RefreshToken.Revoke` all use `IDateTimeProvider`. Use a `FakeDateTimeProvider` for deterministic time control:

```csharp
private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
```

## Implementation Steps

### Step 1: Winning.Create tests

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateWinning_WhenValidParametersProvided` | All valid | Properties set correctly |
| `Create_ShouldSetAwardedDateUtc_WhenCreated` | Valid input | `AwardedDateUtc` matches `dateTimeProvider.UtcNow` exactly |
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

Note: Token generation (URL-safe base64) now lives in the command handler, not the entity. The domain factory accepts the token string as a parameter and validates it.

| Test | Scenario | Expected |
|------|----------|----------|
| `Create_ShouldCreateToken_WhenValidParametersProvided` | Valid token and userId | Properties set correctly |
| `Create_ShouldSetToken_WhenProvided` | `token: "test-token-123"` | `Token = "test-token-123"` |
| `Create_ShouldSetUserId_WhenCreated` | `"user-1"` | `UserId = "user-1"` |
| `Create_ShouldSetCreatedAtUtc_WhenCreated` | Valid params | `CreatedAtUtc` matches `dateTimeProvider.UtcNow` exactly |
| `Create_ShouldSetExpiryToOneHour_WhenDefaultExpiryUsed` | Default param | `ExpiresAtUtc` equals `dateTimeProvider.UtcNow.AddHours(1)` exactly |
| `Create_ShouldSetCustomExpiry_WhenExpiryHoursProvided` | `expiryHours: 24` | `ExpiresAtUtc` equals `dateTimeProvider.UtcNow.AddHours(24)` exactly |
| `Create_ShouldThrowException_WhenTokenIsNull` | `token: null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenTokenIsEmpty` | `token: ""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenTokenIsWhitespace` | `token: " "` | `ArgumentException` |
| `Create_ShouldThrowException_WhenUserIdIsNull` | `userId: null` | `ArgumentNullException` |
| `Create_ShouldThrowException_WhenUserIdIsEmpty` | `userId: ""` | `ArgumentException` |
| `Create_ShouldThrowException_WhenUserIdIsWhitespace` | `userId: " "` | `ArgumentException` |

### Step 6: PasswordResetToken.IsExpired tests

`IsExpired` is now a method accepting `IDateTimeProvider`. Advance the fake time to test boundaries deterministically:

| Test | Scenario | Expected |
|------|----------|----------|
| `IsExpired_ShouldReturnFalse_WhenExpiryIsInFuture` | `dateTimeProvider.UtcNow` before `ExpiresAtUtc` | `false` |
| `IsExpired_ShouldReturnTrue_WhenExpiryIsInPast` | `dateTimeProvider.UtcNow` after `ExpiresAtUtc` | `true` |
| `IsExpired_ShouldReturnFalse_WhenExactlyAtExpiry` | `dateTimeProvider.UtcNow == ExpiresAtUtc` | `false` (uses `>`, not `>=`) |

Note: `IsExpired` uses `>` (strictly greater than), meaning at the exact expiry moment the token is NOT expired. With `FakeDateTimeProvider`, this boundary is now testable deterministically.

### Step 7: RefreshToken tests

`IsExpired`, `IsActive`, and `Revoke` are now methods accepting `IDateTimeProvider`. Use the fake to control all time checks:

| Test | Scenario | Expected |
|------|----------|----------|
| `Revoke_ShouldSetRevokedTimestamp_WhenCalled` | Active token | `Revoked` matches `dateTimeProvider.UtcNow` exactly |
| `Revoke_ShouldMakeTokenInactive_WhenTokenWasActive` | Active token | `IsActive(dateTimeProvider)` returns false |
| `IsExpired_ShouldReturnFalse_WhenExpiresInFuture` | `dateTimeProvider.UtcNow` before `Expires` | `false` |
| `IsExpired_ShouldReturnTrue_WhenExpiresInPast` | `dateTimeProvider.UtcNow` after `Expires` | `true` |
| `IsExpired_ShouldReturnTrue_WhenExactlyAtExpiry` | `dateTimeProvider.UtcNow == Expires` | `true` (uses `>=`, not `>`) |
| `IsActive_ShouldReturnTrue_WhenNotRevokedAndNotExpired` | Valid, not revoked | `true` |
| `IsActive_ShouldReturnFalse_WhenRevoked` | Revoked token | `false` |
| `IsActive_ShouldReturnFalse_WhenExpired` | Past expiry, not revoked | `false` |
| `IsActive_ShouldReturnFalse_WhenBothRevokedAndExpired` | Revoked + past expiry | `false` |

Note: `RefreshToken.IsExpired` uses `>=` (expired AT the exact moment), while `PasswordResetToken.IsExpired` uses `>` (NOT expired at the exact moment). With `FakeDateTimeProvider`, both boundary cases are now testable deterministically.

## Code Patterns to Follow

```csharp
public class PasswordResetTokenTests
{
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void Create_ShouldCreateToken_WhenValidParametersProvided()
    {
        // Act
        var token = PasswordResetToken.Create("test-token-abc", "user-1", _dateTimeProvider);

        // Assert
        token.Token.Should().Be("test-token-abc");
        token.UserId.Should().Be("user-1");
        token.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
        token.ExpiresAtUtc.Should().Be(_dateTimeProvider.UtcNow.AddHours(1));
    }

    [Fact]
    public void IsExpired_ShouldReturnFalse_WhenExactlyAtExpiry()
    {
        // Arrange
        var token = PasswordResetToken.Create("test-token-abc", "user-1", _dateTimeProvider);

        // Advance time to exactly the expiry moment
        _dateTimeProvider.UtcNow = token.ExpiresAtUtc;

        // Assert — uses > (strictly greater), so equal is NOT expired
        token.IsExpired(_dateTimeProvider).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenExpiryIsInPast()
    {
        // Arrange
        var token = PasswordResetToken.Create("test-token-abc", "user-1", _dateTimeProvider);

        // Advance time past expiry
        _dateTimeProvider.UtcNow = token.ExpiresAtUtc.AddSeconds(1);

        // Assert
        token.IsExpired(_dateTimeProvider).Should().BeTrue();
    }
}

public class RefreshTokenTests
{
    private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void IsExpired_ShouldReturnTrue_WhenExactlyAtExpiry()
    {
        // Arrange
        var token = new RefreshToken
        {
            UserId = "user-1",
            Token = "test-token",
            Created = _dateTimeProvider.UtcNow,
            Expires = _dateTimeProvider.UtcNow.AddDays(7)
        };

        // Advance time to exactly the expiry moment
        _dateTimeProvider.UtcNow = token.Expires;

        // Assert — uses >= so equal IS expired (different from PasswordResetToken)
        token.IsExpired(_dateTimeProvider).Should().BeTrue();
    }

    [Fact]
    public void Revoke_ShouldSetRevokedTimestamp_WhenCalled()
    {
        // Arrange
        var token = new RefreshToken
        {
            UserId = "user-1",
            Token = "test-token",
            Created = _dateTimeProvider.UtcNow,
            Expires = _dateTimeProvider.UtcNow.AddDays(7)
        };

        // Act
        token.Revoke(_dateTimeProvider);

        // Assert
        token.Revoked.Should().Be(_dateTimeProvider.UtcNow);
        token.IsActive(_dateTimeProvider).Should().BeFalse();
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
- [ ] PasswordResetToken.Create validates token (null, empty, whitespace)
- [ ] PasswordResetToken.Create validates userId (null, empty, whitespace)
- [ ] PasswordResetToken stores Token, UserId, and CreatedAtUtc
- [ ] Token expiry computed properties work correctly for both token types
- [ ] RefreshToken.IsActive combines revocation and expiry checks (all 4 combinations)
- [ ] RefreshToken.Revoke sets Revoked timestamp and makes token inactive
- [ ] `dotnet test` passes

## Edge Cases to Consider

- PasswordResetToken.Create now accepts a token string — URL-safe generation is tested in the Application layer, not here
- PasswordResetToken expiry boundary (exactly at ExpiresAtUtc — `>` means equal to boundary is NOT expired) — now deterministically testable with `FakeDateTimeProvider`
- RefreshToken expiry boundary (uses `>=` — equal to boundary IS expired) — now deterministically testable with `FakeDateTimeProvider`
- Winning with zero amount (valid — e.g., placeholder prizes)
- ApplicationUser inherits from IdentityUser — some properties come from the base class
- Revoked + expired RefreshToken (both conditions false — still inactive)
