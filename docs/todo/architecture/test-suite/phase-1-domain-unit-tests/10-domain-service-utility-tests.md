# Task: Domain Service and Utility Tests

**Parent Feature:** [Phase 1: Domain Unit Tests](./README.md)

## Status

**Not Started** | In Progress | Complete

## Goal

Test the `PredictionDomainService.SubmitPredictions` method and the `NameValidator` utility class.

## Files to Modify

| File | Action | Purpose |
|------|--------|---------|
| `tests/ThePredictions.Domain.Tests/Services/PredictionDomainServiceTests.cs` | Create | SubmitPredictions tests |
| `tests/ThePredictions.Domain.Tests/Common/Validation/NameValidatorTests.cs` | Create | Name validation and sanitisation tests |

## Implementation Steps

### Step 1: PredictionDomainService.SubmitPredictions tests

Create a Round with a future deadline for success-path tests and a past deadline for failure tests using the public constructor.

| Test | Scenario | Expected |
|------|----------|----------|
| `SubmitPredictions_ShouldReturnPredictions_WhenDeadlineNotPassed` | Future deadline | Returns predictions matching input |
| `SubmitPredictions_ShouldCreateCorrectNumberOfPredictions_WhenMultipleScoresProvided` | 3 scores | 3 predictions returned |
| `SubmitPredictions_ShouldSetUserIdOnAllPredictions_WhenCreated` | `userId: "user-1"` | All predictions have `UserId = "user-1"` |
| `SubmitPredictions_ShouldThrowException_WhenDeadlineHasPassed` | Past deadline | `InvalidOperationException` |
| `SubmitPredictions_ShouldThrowException_WhenRoundIsNull` | `null` | `ArgumentNullException` |

```csharp
// Helper to create Round with future deadline
private static Round CreateRoundWithFutureDeadline() =>
    new(id: 1, seasonId: 1, roundNumber: 1,
        startDateUtc: DateTime.UtcNow.AddDays(2),
        deadlineUtc: DateTime.UtcNow.AddDays(1),
        status: RoundStatus.Published,
        apiRoundName: null,
        lastReminderSentUtc: null,
        matches: null);

// Helper to create Round with past deadline
private static Round CreateRoundWithPastDeadline() =>
    new(id: 1, seasonId: 1, roundNumber: 1,
        startDateUtc: DateTime.UtcNow.AddDays(-1),
        deadlineUtc: DateTime.UtcNow.AddDays(-2),
        status: RoundStatus.InProgress,
        apiRoundName: null,
        lastReminderSentUtc: null,
        matches: null);
```

### Step 2: NameValidator.IsValid tests

The regex pattern allows: letters (any language), combining marks, apostrophes, hyphens, spaces, periods.

| Test | Input | Expected |
|------|-------|----------|
| `IsValid_ShouldReturnTrue_WhenNameContainsOnlyLetters` | `"John"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsSpaces` | `"John Smith"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsApostrophe` | `"O'Brien"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsHyphen` | `"Mary-Jane"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsPeriod` | `"Dr. Smith"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsUnicodeLetters` | `"José García"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameContainsCombiningMarks` | `"naïve"` | `true` |
| `IsValid_ShouldReturnTrue_WhenNameIsNullOrWhitespace` | `null`, `""`, `" "` | `true` (by design) |
| `IsValid_ShouldReturnFalse_WhenNameContainsNumbers` | `"John123"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsHtmlCharacters` | `"<script>"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsAmpersand` | `"Tom & Jerry"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsEmoji` | `"John 😀"` | `false` |
| `IsValid_ShouldReturnFalse_WhenNameContainsSpecialSymbols` | `"user@name"` | `false` |

### Step 3: NameValidator.Sanitize tests

| Test | Input | Expected |
|------|-------|----------|
| `Sanitise_ShouldReturnSameName_WhenNameIsValid` | `"John Smith"` | `"John Smith"` |
| `Sanitise_ShouldRemoveNumbers_WhenPresent` | `"John123"` | `"John"` |
| `Sanitise_ShouldRemoveHtmlCharacters_WhenPresent` | `"<b>John</b>"` | `"bJohnb"` |
| `Sanitise_ShouldRemoveSpecialSymbols_WhenPresent` | `"user@name#!"` | `"username"` |
| `Sanitise_ShouldTrimResult_WhenTrailingSpacesRemain` | `"John 123"` | `"John"` (trimmed) |
| `Sanitise_ShouldReturnEmpty_WhenNameIsNull` | `null` | `""` |
| `Sanitise_ShouldReturnEmpty_WhenNameIsWhitespace` | `" "` | `""` |
| `Sanitise_ShouldPreserveApostrophesAndHyphens` | `"O'Brien-Smith"` | `"O'Brien-Smith"` |
| `Sanitise_ShouldPreserveUnicodeLetters` | `"José"` | `"José"` |

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
- [ ] NameValidator allows all Unicode letters and combining marks
- [ ] NameValidator blocks HTML, numbers, emojis, special symbols
- [ ] Sanitise removes unsafe characters and trims result
- [ ] `dotnet test` passes

## Edge Cases to Consider

- NameValidator.IsValid returns `true` for null/empty/whitespace — this is intentional (validation of required-ness is handled elsewhere)
- Sanitise with a name that becomes empty after stripping all characters
- PredictionDomainService with empty predictions list (should return empty collection without error)
- Names with combining diacritical marks (e.g., `e\u0301` for "é")
