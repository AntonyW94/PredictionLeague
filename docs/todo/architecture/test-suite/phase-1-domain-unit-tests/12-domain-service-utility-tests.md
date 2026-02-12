# Task: Domain Service and Utility Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the `PredictionDomainService.SubmitPredictions` method and the `NameValidator` utility class.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Services/PredictionDomainServiceTests.cs` | Create | SubmitPredictions tests |
| `tests/Unit/ThePredictions.Domain.Tests.Unit/Common/Validation/NameValidatorTests.cs` | Create | Name validation and sanitisation tests |

## Prerequisites

`PredictionDomainService` now accepts `IDateTimeProvider` via constructor injection. Instantiate it with a `FakeDateTimeProvider`:

```csharp
private readonly FakeDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));
private readonly PredictionDomainService _sut;

public PredictionDomainServiceTests()
{
    _sut = new PredictionDomainService(_dateTimeProvider);
}
```

## Implementation Steps

### Step 1: PredictionDomainService.SubmitPredictions tests

Create a Round with a deadline relative to `dateTimeProvider.UtcNow`:

| Test | Scenario | Expected |
|------|----------|----------|
| `SubmitPredictions_ShouldReturnPredictions_WhenDeadlineNotPassed` | Round deadline after `dateTimeProvider.UtcNow` | Returns predictions matching input |
| `SubmitPredictions_ShouldCreateCorrectNumberOfPredictions_WhenMultipleScoresProvided` | 3 scores | 3 predictions returned |
| `SubmitPredictions_ShouldSetUserIdOnAllPredictions_WhenCreated` | `userId: "user-1"` | All predictions have `UserId = "user-1"` |
| `SubmitPredictions_ShouldSetCorrectMatchIds_WhenCreated` | 3 different matchIds | Each prediction has correct MatchId |
| `SubmitPredictions_ShouldSetCorrectScores_WhenCreated` | Varied scores | Each prediction has correct HomeScore and AwayScore |
| `SubmitPredictions_ShouldSetPendingOutcome_WhenCreated` | Any valid input | All predictions have `Outcome = Pending` |
| `SubmitPredictions_ShouldReturnEmptyCollection_WhenEmptyPredictionsListProvided` | Empty list | Empty collection returned (no error) |
| `SubmitPredictions_ShouldReturnSinglePrediction_WhenOnlyOneScoreProvided` | 1 score | 1 prediction returned |
| `SubmitPredictions_ShouldThrowException_WhenDeadlineHasPassed` | Round deadline before `dateTimeProvider.UtcNow` | `InvalidOperationException` |
| `SubmitPredictions_ShouldThrowException_WhenRoundIsNull` | `null` | `ArgumentNullException` |

```csharp
// Helper to create Round with future deadline (relative to fake time)
private Round CreateRoundWithFutureDeadline() =>
    new(id: 1, seasonId: 1, roundNumber: 1,
        startDateUtc: _dateTimeProvider.UtcNow.AddDays(2),
        deadlineUtc: _dateTimeProvider.UtcNow.AddDays(1),
        status: RoundStatus.Published,
        apiRoundName: null,
        lastReminderSentUtc: null,
        matches: null);

// Helper to create Round with past deadline (relative to fake time)
private Round CreateRoundWithPastDeadline() =>
    new(id: 1, seasonId: 1, roundNumber: 1,
        startDateUtc: _dateTimeProvider.UtcNow.AddDays(-1),
        deadlineUtc: _dateTimeProvider.UtcNow.AddDays(-2),
        status: RoundStatus.InProgress,
        apiRoundName: null,
        lastReminderSentUtc: null,
        matches: null);
```

### Step 2: NameValidator.IsValid tests

The regex pattern allows: letters (any language), combining marks, apostrophes, hyphens, spaces, periods.

**Valid names (should return `true`):**

| Test | Input | Expected |
|------|-------|----------|
| `IsValid_ShouldReturnTrue_WhenNameContainsOnlyLetters` | `"John"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsSpaces` | `"John Smith"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsApostrophe` | `"O'Brien"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsHyphen` | `"Mary-Jane"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsPeriod` | `"Dr. Smith"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsUnicodeLetters` | `"José García"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsCombiningMarks` | `"naïve"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsChineseCharacters` | `"王明"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsArabicCharacters` | `"محمد"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameIsNull` | `null` | `true` (by design) |
| `IsValid_ShouldReturnTrue_WhenNameIsEmpty` | `""` | `true` (by design) |
| `IsValid_ShouldReturnTrue_WhenNameIsWhitespace` | `" "` | `true` (by design) |

**Invalid names (should return `false`):**

| Test | Input | Expected |
|------|-------|----------|
| `IsValid_ShouldReturnFalse_WhenNameContainsNumbers` | `"John123"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsHtmlAngleBrackets` | `"<script>"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsAmpersand` | `"Tom & Jerry"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsEmoji` | `"John 😀"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsAtSymbol` | `"user@name"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsHashSymbol` | `"Name#1"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsParentheses` | `"(John)"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsSquareBrackets` | `"[John]"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsForwardSlash` | `"John/Smith"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsBackslash` | `"John\\Smith"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsExclamationMark` | `"John!"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameIsOnlyNumbers` | `"12345"` | `false` |

### Step 3: NameValidator.Sanitise tests

| Test | Input | Expected |
|------|-------|----------|
| `Sanitise_ShouldReturnSameName_WhenNameIsValid` | `"John Smith"` | `"John Smith"` |
| `Sanitise_ShouldRemoveNumbers_WhenPresent` | `"John123"` | `"John"` |
| `Sanitise_ShouldRemoveHtmlCharacters_WhenPresent` | `"<b>John</b>"` | `"bJohnb"` |
| `Sanitise_ShouldRemoveSpecialSymbols_WhenPresent` | `"user@name#!"` | `"username"` |
| `Sanitise_ShouldRemoveEmoji_WhenPresent` | `"John 😀 Smith"` | `"John  Smith"` (note: trimmed result depends on position) |
| `Sanitise_ShouldTrimResult_WhenTrailingSpacesRemain` | `"John 123"` | `"John"` (trimmed) |
| `Sanitise_ShouldReturnEmpty_WhenNameIsNull` | `null` | `""` |
| `Sanitise_ShouldReturnEmpty_WhenNameIsWhitespace` | `" "` | `""` |
| `Sanitise_ShouldReturnEmpty_WhenNameIsEmpty` | `""` | `""` |
| `Sanitise_ShouldReturnEmpty_WhenAllCharactersAreUnsafe` | `"123!@#"` | `""` |
| `Sanitise_ShouldPreserveApostrophesAndHyphens` | `"O'Brien-Smith"` | `"O'Brien-Smith"` |
| `Sanitise_ShouldPreservePeriods` | `"Dr. Smith"` | `"Dr. Smith"` |
| `Sanitise_ShouldPreserveUnicodeLetters` | `"José"` | `"José"` |
| `Sanitise_ShouldPreserveChineseCharacters` | `"王明"` | `"王明"` |

## Code Patterns to Follow

```csharp
public class NameValidatorTests
{
    [Theory]
    [InlineData("John")]
    [InlineData("O'Brien")]
    [InlineData("Mary-Jane")]
    [InlineData("Dr. Smith")]
    [InlineData("José García")]
    [InlineData("王明")]
    public void IsValid_ShouldReturnTrue_WhenNameContainsAllowedCharacters(string name)
    {
        // Act
        var result = NameValidator.IsValid(name);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("John123")]
    [InlineData("<script>")]
    [InlineData("Tom & Jerry")]
    [InlineData("user@name")]
    [InlineData("(John)")]
    [InlineData("12345")]
    public void IsValid_ShouldReturnFalse_WhenNameContainsBlockedCharacters(string name)
    {
        // Act
        var result = NameValidator.IsValid(name);

        // Assert
        result.Should().BeFalse();
    }
}
```

## Verification

- [ ] PredictionDomainService enforces deadline
- [ ] PredictionDomainService creates correct number of predictions
- [ ] PredictionDomainService sets correct MatchId, scores, userId on each prediction
- [ ] PredictionDomainService returns empty collection for empty input (no error)
- [ ] NameValidator allows all Unicode letters (Latin, Chinese, Arabic) and combining marks
- [ ] NameValidator allows apostrophes, hyphens, spaces, periods
- [ ] NameValidator blocks HTML characters, numbers, emojis, special symbols
- [ ] NameValidator returns `true` for null/empty/whitespace (by design — required-ness validated elsewhere)
- [ ] Sanitise removes unsafe characters and trims result
- [ ] Sanitise preserves all allowed characters (letters, apostrophes, hyphens, periods)
- [ ] Sanitise returns empty string for null/whitespace/all-unsafe input
- [ ] `dotnet test` passes

## Edge Cases to Consider

- NameValidator.IsValid returns `true` for null/empty/whitespace — this is intentional (validation of required-ness is handled elsewhere)
- Sanitise with a name that becomes empty after stripping all characters
- PredictionDomainService with empty predictions list (should return empty collection without error)
- Names with combining diacritical marks (e.g., `e\u0301` for "é")
- Sanitise result may have internal spaces where removed characters were (e.g., `"John 😀 Smith"` → `"John  Smith"`)
- With `FakeDateTimeProvider`, deadline checks in PredictionDomainService are deterministic — no race conditions
