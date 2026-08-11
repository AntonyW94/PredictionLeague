using FluentAssertions;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services;

/// <summary>
/// Whether an overall table's cached pre-round position is worth showing - the rule behind the small
/// rank-change arrow, shared by a league's own table and the dashboard's tile.
/// </summary>
public class LeaderboardSnapshotTests
{
    [Fact]
    public void RankToShow_ShouldReturnTheCachedRank_OnceARoundHasFinished()
    {
        LeaderboardSnapshot.RankToShow(cachedRank: 4, hasCompletedRound: true).Should().Be(4);
    }

    [Fact]
    public void RankToShow_ShouldReturnNothing_BeforeAnyRoundHasFinished()
    {
        // There is no earlier position to have moved from, so an arrow would measure against nothing.
        LeaderboardSnapshot.RankToShow(cachedRank: 4, hasCompletedRound: false).Should().BeNull();
    }

    [Fact]
    public void RankToShow_ShouldReturnNothing_WhenNoRankIsCached()
    {
        LeaderboardSnapshot.RankToShow(cachedRank: null, hasCompletedRound: true).Should().BeNull();
    }
}
