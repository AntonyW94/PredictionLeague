using FluentAssertions;
using ThePredictions.Application.Formatters;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Formatters;

public class ReminderUrgencyFormatterTests
{
    [Theory]
    [InlineData(0, "urgent")]            // deadline effectively now
    [InlineData(-30, "urgent")]          // just passed
    [InlineData(359, "urgent")]          // 5h59m - under 6h
    [InlineData(360, "soon")]            // exactly 6h
    [InlineData(720, "soon")]            // 12h
    [InlineData(1439, "soon")]           // 23h59m - under 24h
    [InlineData(1440, "relaxed")]        // exactly 24h
    [InlineData(4320, "relaxed")]        // 3 days
    public void GetUrgencyTier_ShouldClassifyByTimeRemaining(int minutesRemaining, string expected)
    {
        var tier = ReminderUrgencyFormatter.GetUrgencyTier(TimeSpan.FromMinutes(minutesRemaining));

        tier.Should().Be(expected);
    }

    [Theory]
    [InlineData(4320, "3 days")]         // exactly 3 days
    [InlineData(1740, "1 day")]          // 1 day 5 hours - rounds down
    [InlineData(1440, "1 day")]          // exactly 1 day (singular)
    [InlineData(360, "6 hours")]         // 6 hours
    [InlineData(150, "2 hours")]         // 2h30m - rounds down
    [InlineData(60, "1 hour")]           // exactly 1 hour (singular)
    [InlineData(45, "45 minutes")]       // 45 minutes
    [InlineData(1, "1 minute")]          // exactly 1 minute (singular)
    [InlineData(0, "less than a minute")]
    [InlineData(-30, "less than a minute")]
    public void FormatTimeRemaining_ShouldRoundDownToAHumanReadableUnit(int minutesRemaining, string expected)
    {
        var text = ReminderUrgencyFormatter.FormatTimeRemaining(TimeSpan.FromMinutes(minutesRemaining));

        text.Should().Be(expected);
    }
}
