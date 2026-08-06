using FluentAssertions;
using ThePredictions.Infrastructure.Formatters;
using Xunit;

namespace ThePredictions.Infrastructure.Tests.Unit.Formatters;

/// <summary>
/// Deadlines in emails are quoted in UK local time, because that is what players read them in.
/// Getting the summer/winter offset wrong would tell someone the wrong hour to predict by.
/// </summary>
public class UkEmailDateFormatterTests
{
    private readonly UkEmailDateFormatter _formatter = new();

    [Fact]
    public void FormatDeadline_ShouldQuoteGmt_ForAWinterDate()
    {
        var deadlineUtc = new DateTime(2026, 1, 17, 14, 30, 0, DateTimeKind.Utc);

        _formatter.FormatDeadline(deadlineUtc).Should().Be("Saturday, 17 January 2026 at 14:30 (GMT)");
    }

    [Fact]
    public void FormatDeadline_ShouldShiftAnHourAndQuoteBst_ForASummerDate()
    {
        // British Summer Time is UTC+1, so 14:30 UTC is quoted as 15:30 to the player.
        var deadlineUtc = new DateTime(2026, 7, 18, 14, 30, 0, DateTimeKind.Utc);

        _formatter.FormatDeadline(deadlineUtc).Should().Be("Saturday, 18 July 2026 at 15:30 (BST)");
    }

    [Fact]
    public void FormatDeadline_ShouldStayOnGmt_JustBeforeTheClocksGoForward()
    {
        // BST starts 01:00 UTC on the last Sunday in March 2026 (the 29th).
        var deadlineUtc = new DateTime(2026, 3, 29, 0, 30, 0, DateTimeKind.Utc);

        _formatter.FormatDeadline(deadlineUtc).Should().EndWith("(GMT)");
    }

    [Fact]
    public void FormatDeadline_ShouldSwitchToBst_OnceTheClocksGoForward()
    {
        var deadlineUtc = new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc);

        _formatter.FormatDeadline(deadlineUtc).Should().EndWith("(BST)");
    }

    [Fact]
    public void FormatDeadline_ShouldReturnToGmt_WhenTheClocksGoBack()
    {
        // BST ends 01:00 UTC on the last Sunday in October 2026 (the 25th).
        var deadlineUtc = new DateTime(2026, 10, 25, 1, 30, 0, DateTimeKind.Utc);

        _formatter.FormatDeadline(deadlineUtc).Should().EndWith("(GMT)");
    }

    [Fact]
    public void FormatDeadline_ShouldFallBackToUtc_OnAHostWithNoUkTimeZoneData()
    {
        var formatter = new UkEmailDateFormatter("Not/A-Real-Time-Zone");
        var deadlineUtc = new DateTime(2026, 7, 18, 14, 30, 0, DateTimeKind.Utc);

        formatter.FormatDeadline(deadlineUtc).Should().Be("Saturday, 18 July 2026 at 14:30 (UTC)");
    }
}
