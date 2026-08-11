using FluentAssertions;
using ThePredictions.Domain.Services.Badges;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services.Badges;

/// <summary>
/// Which of two badge collections is worth more - the score the badges leaderboard is ranked on.
/// </summary>
public class BadgeTallyTests
{
    private static readonly DateTime Earlier = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Later = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CompareTo_ShouldPutMoreBadgesAhead()
    {
        var more = new BadgeTally(5, Later);
        var fewer = new BadgeTally(4, Earlier);

        more.CompareTo(fewer).Should().BePositive();
        fewer.CompareTo(more).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_ShouldPutWhoeverGotThereFirstAhead_WhenTheCountsAreLevel()
    {
        // Sooner is better, so the earlier date is the greater tally. This is the tie-break the leaderboard has
        // always used; folding it into the score is what lets it award positions rather than only sort rows.
        var sooner = new BadgeTally(5, Earlier);
        var later = new BadgeTally(5, Later);

        sooner.CompareTo(later).Should().BePositive();
        later.CompareTo(sooner).Should().BeNegative();
    }

    [Fact]
    public void CompareTo_ShouldTreatIdenticalTalliesAsLevel()
    {
        new BadgeTally(5, Earlier).CompareTo(new BadgeTally(5, Earlier)).Should().Be(0);
    }

    [Fact]
    public void CompareTo_ShouldTreatEveryoneWithNoBadgesAsLevel()
    {
        // Nine of our own players hold nothing at all. They share the last position rather than being ordered
        // against each other by something none of them has.
        new BadgeTally(0, null).CompareTo(new BadgeTally(0, null)).Should().Be(0);
    }

    [Fact]
    public void CompareTo_ShouldPutAPlayerWithNoBadgesBehind()
    {
        var none = new BadgeTally(0, null);
        var some = new BadgeTally(1, Later);

        none.CompareTo(some).Should().BeNegative();
        some.CompareTo(none).Should().BePositive();
    }

    [Fact]
    public void CompareTo_ShouldPutADatedTallyAheadOfAnUndatedOneOnTheSameCount()
    {
        // Not a state the leaderboard can reach - no badges means no date - but the comparison is defined rather
        // than left to chance, and "never" is behind any real date.
        var dated = new BadgeTally(0, Later);
        var undated = new BadgeTally(0, null);

        dated.CompareTo(undated).Should().BePositive();
        undated.CompareTo(dated).Should().BeNegative();
    }
}
