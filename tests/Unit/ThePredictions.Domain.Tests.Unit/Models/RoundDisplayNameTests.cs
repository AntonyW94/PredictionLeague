using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

/// <summary>
/// The round's display name, previously a CASE expression written out in SQL in both
/// GetRoundCompletionQueryHandler and ReminderService - the second rule those two files duplicated.
/// </summary>
public class RoundDisplayNameTests
{
    [Fact]
    public void GetDisplayNameOrDefault_ShouldUseTheDisplayName_WhenOneIsSet()
    {
        Round("Quarter Finals", roundNumber: 7).GetDisplayNameOrDefault().Should().Be("Quarter Finals");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetDisplayNameOrDefault_ShouldFallBackToTheRoundNumber_WhenTheDisplayNameIsBlank(string displayName)
    {
        // The SQL used LEN(LTRIM(RTRIM(...))) > 0, so whitespace counted as absent.
        Round(displayName, roundNumber: 12).GetDisplayNameOrDefault().Should().Be("Round 12");
    }

    private static Round Round(string displayName, int roundNumber) =>
        new(
            id: 1, seasonId: 1, roundNumber: roundNumber, displayName: displayName,
            startDateUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            deadlineUtc: new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc),
            status: RoundStatus.Published, apiRoundName: null, lastReminderSentUtc: null,
            matches: null, resultsDigestSentUtc: null);
}
