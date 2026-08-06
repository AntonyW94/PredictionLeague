using FluentAssertions;
using ThePredictions.Application.Features.Admin.Seasons.Commands;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Commands;

/// <summary>
/// Round windows decide which round a fixture belongs to when the football API reschedules it.
/// Boundaries sit at the midpoint between neighbouring rounds' median kick-off dates, so a moved
/// fixture lands in whichever round it is closer to.
/// </summary>
public class SyncSeasonWithApiRoundWindowTests
{
    private static SyncSeasonWithApiCommandHandler.RoundFixtureSummary Summary(int roundNumber, DateTime medianUtc) =>
        new($"Regular Season - {roundNumber}", roundNumber, medianUtc);

    private static DateTime Day(int day) => new(2026, 8, day, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CalculateRoundWindows_ShouldReturnNothing_WhenThereAreNoRounds()
    {
        SyncSeasonWithApiCommandHandler.CalculateRoundWindows([]).Should().BeEmpty();
    }

    [Fact]
    public void CalculateRoundWindows_ShouldGiveASingleRoundAnUnboundedWindow()
    {
        var windows = SyncSeasonWithApiCommandHandler.CalculateRoundWindows([Summary(1, Day(10))]);

        windows.Should().ContainSingle();
        windows[0].RoundNumber.Should().Be(1);
        windows[0].ApiRoundName.Should().Be("Regular Season - 1");
        windows[0].WindowStart.Should().Be(DateTime.MinValue);
        windows[0].WindowEnd.Should().Be(DateTime.MaxValue);
    }

    [Fact]
    public void CalculateRoundWindows_ShouldPlaceTheBoundaryMidwayBetweenTwoRounds()
    {
        var windows = SyncSeasonWithApiCommandHandler.CalculateRoundWindows([
            Summary(1, Day(10)),
            Summary(2, Day(20))
        ]);

        var expectedBoundary = Day(15);

        windows.Should().HaveCount(2);
        windows[0].WindowStart.Should().Be(DateTime.MinValue);
        windows[0].WindowEnd.Should().Be(expectedBoundary);
        windows[1].WindowStart.Should().Be(expectedBoundary);
        windows[1].WindowEnd.Should().Be(DateTime.MaxValue);
    }

    [Fact]
    public void CalculateRoundWindows_ShouldLeaveTheFirstAndLastWindowsOpenEnded()
    {
        var windows = SyncSeasonWithApiCommandHandler.CalculateRoundWindows([
            Summary(1, Day(5)),
            Summary(2, Day(12)),
            Summary(3, Day(19))
        ]);

        windows[0].WindowStart.Should().Be(DateTime.MinValue);
        windows[^1].WindowEnd.Should().Be(DateTime.MaxValue);
    }

    [Fact]
    public void CalculateRoundWindows_ShouldJoinWindowsWithoutGapsOrOverlaps()
    {
        var windows = SyncSeasonWithApiCommandHandler.CalculateRoundWindows([
            Summary(1, Day(3)),
            Summary(2, Day(9)),
            Summary(3, Day(17)),
            Summary(4, Day(26))
        ]);

        // Every fixture date must fall in exactly one window, so each window has to start
        // precisely where the previous one ended.
        for (var i = 1; i < windows.Count; i++)
            windows[i].WindowStart.Should().Be(windows[i - 1].WindowEnd);
    }

    [Fact]
    public void CalculateRoundWindows_ShouldContainEachRoundsOwnMedian()
    {
        var summaries = new List<SyncSeasonWithApiCommandHandler.RoundFixtureSummary>
        {
            Summary(1, Day(3)),
            Summary(2, Day(9)),
            Summary(3, Day(17))
        };

        var windows = SyncSeasonWithApiCommandHandler.CalculateRoundWindows(summaries);

        for (var i = 0; i < summaries.Count; i++)
        {
            summaries[i].MedianDateUtc.Should().BeOnOrAfter(windows[i].WindowStart);
            summaries[i].MedianDateUtc.Should().BeBefore(windows[i].WindowEnd);
        }
    }

    [Fact]
    public void CalculateRoundWindows_ShouldPreserveTheRoundOrderAndNames()
    {
        var windows = SyncSeasonWithApiCommandHandler.CalculateRoundWindows([
            Summary(1, Day(3)),
            Summary(2, Day(9)),
            Summary(3, Day(17))
        ]);

        windows.Select(w => w.RoundNumber).Should().Equal(1, 2, 3);
        windows.Select(w => w.ApiRoundName)
            .Should().Equal("Regular Season - 1", "Regular Season - 2", "Regular Season - 3");
    }

    [Fact]
    public void CalculateRoundWindows_ShouldCopeWithTwoRoundsSharingAMedian()
    {
        // A double-header week: both medians land on the same day, so the boundary sits on it too.
        var windows = SyncSeasonWithApiCommandHandler.CalculateRoundWindows([
            Summary(1, Day(10)),
            Summary(2, Day(10))
        ]);

        windows[0].WindowEnd.Should().Be(Day(10));
        windows[1].WindowStart.Should().Be(Day(10));
    }

    [Fact]
    public void CalculateRoundWindows_ShouldMarkTheBoundaryAsUtc()
    {
        var windows = SyncSeasonWithApiCommandHandler.CalculateRoundWindows([
            Summary(1, Day(10)),
            Summary(2, Day(20))
        ]);

        windows[0].WindowEnd.Kind.Should().Be(DateTimeKind.Utc);
    }
}
