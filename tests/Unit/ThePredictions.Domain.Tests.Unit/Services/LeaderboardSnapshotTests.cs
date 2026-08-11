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

    [Fact]
    public void PlacesGained_ShouldBePositive_WhenTheyHaveClimbed()
    {
        // Fifth before the round, third after it: two places gained.
        LeaderboardSnapshot.PlacesGained(snapshotRank: 5, currentRank: 3).Should().Be(2);
    }

    [Fact]
    public void PlacesGained_ShouldBeNegative_WhenTheyHaveDropped()
    {
        LeaderboardSnapshot.PlacesGained(snapshotRank: 3, currentRank: 5).Should().Be(-2);
    }

    [Fact]
    public void PlacesGained_ShouldBeZero_WhenTheyHeldTheirPlace()
    {
        // Zero and null mean different things to a player: this one is "you held your place".
        LeaderboardSnapshot.PlacesGained(snapshotRank: 3, currentRank: 3).Should().Be(0);
    }

    [Fact]
    public void PlacesGained_ShouldBeNothing_WhenThereIsNoEarlierPosition()
    {
        // A player who has only just joined has nothing to have moved from, and an arrow pointing sideways would be a
        // claim we cannot make.
        LeaderboardSnapshot.PlacesGained(snapshotRank: null, currentRank: 3).Should().BeNull();
    }

    [Fact]
    public void PlacesGained_ShouldBeNothing_WhenThereIsNoCurrentPosition()
    {
        LeaderboardSnapshot.PlacesGained(snapshotRank: 3, currentRank: null).Should().BeNull();
    }
}
