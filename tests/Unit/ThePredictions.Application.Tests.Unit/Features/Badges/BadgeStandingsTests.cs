using FluentAssertions;
using ThePredictions.Application.Features.Badges;
using ThePredictions.Application.Features.Badges.Queries;
using ThePredictions.Domain.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Badges;

/// <summary>
/// The site-wide badges table: who is on it, what they have collected, and the positions they hold.
///
/// One rule for both the table and the dashboard tile's "3rd of 44" line. They used to be worked out by two
/// different SQL statements that disagreed with each other about most of the table.
/// </summary>
public class BadgeStandingsTests
{
    private static readonly DateTime March = new(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime August = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    #region Who is on the table

    [Fact]
    public void Of_ShouldReturnNothing_WhenThereAreNoAccounts()
    {
        BadgeStandings.Of(new BadgeLeaderboardData([], [])).Should().BeEmpty();
    }

    [Fact]
    public void Of_ShouldIncludeAPlayerWhoHasEarnedNothing()
    {
        // Everybody is on the table. Being last is a nudge; being missing is confusing.
        var standings = BadgeStandings.Of(Data([Player("u1", "Ada", "Lovelace")], []));

        standings.Should().HaveCount(1);
        standings[0].Item.Tally.BadgeCount.Should().Be(0);
        standings[0].Item.Tally.LastAwardedUtc.Should().BeNull();
        standings[0].Rank.Should().Be(1);
    }

    [Fact]
    public void Of_ShouldLeaveOutAnAccountThatNeverFinishedSigningUp()
    {
        // No name means the sign-up was abandoned. Listing them would put blanks on a public table and inflate the
        // "of 44 players" everyone else is measured against.
        var standings = BadgeStandings.Of(Data(
        [
            Player("u1", "Ada", "Lovelace"),
            Player("u2", null, null),
            Player("u3", string.Empty, "Nobody"),
            Player("u4", "   ", "Nobody")
        ], []));

        standings.Select(standing => standing.Item.UserId).Should().Equal("u1");
    }

    #endregion

    #region What each player has collected

    [Fact]
    public void Of_ShouldCountEachBadgeOnce_HoweverManyTimesItWasWon()
    {
        // This table is about how much of the collection someone holds, not how often they have won. The badges
        // page counts the same rows the other way.
        var standings = BadgeStandings.Of(Data([Player("u1", "Ada", "Lovelace")],
        [
            new BadgePlayerAwardRow("u1", "round-winner", March),
            new BadgePlayerAwardRow("u1", "round-winner", August),
            new BadgePlayerAwardRow("u1", "banked", March)
        ]));

        standings[0].Item.Tally.BadgeCount.Should().Be(2);
    }

    [Fact]
    public void Of_ShouldReportWhenTheyLastEarnedSomething()
    {
        var standings = BadgeStandings.Of(Data([Player("u1", "Ada", "Lovelace")],
        [
            new BadgePlayerAwardRow("u1", "banked", August),
            new BadgePlayerAwardRow("u1", "round-winner", March)
        ]));

        standings[0].Item.Tally.LastAwardedUtc.Should().Be(August);
    }

    [Fact]
    public void Of_ShouldNotGiveOnePlayersBadgesToAnother()
    {
        var standings = BadgeStandings.Of(Data(
        [
            Player("u1", "Ada", "Lovelace"),
            Player("u2", "Grace", "Hopper")
        ],
        [
            new BadgePlayerAwardRow("u1", "banked", March),
            new BadgePlayerAwardRow("u1", "round-winner", March)
        ]));

        Standing(standings, "u1").Item.Tally.BadgeCount.Should().Be(2);
        Standing(standings, "u2").Item.Tally.BadgeCount.Should().Be(0);
    }

    [Fact]
    public void Of_ShouldShowPlayersByTheirFirstNameAndLastInitial()
    {
        var standings = BadgeStandings.Of(Data([Player("u1", "Ada", "Lovelace")], []));

        standings[0].Item.DisplayName.Should().Be("Ada L");
        standings[0].Item.FullName.Should().Be("Ada Lovelace");
    }

    #endregion

    #region Positions

    [Fact]
    public void Of_ShouldPutMoreBadgesFirst()
    {
        var standings = BadgeStandings.Of(Data(
        [
            Player("u1", "Ada", "Lovelace"),
            Player("u2", "Grace", "Hopper")
        ],
        [
            new BadgePlayerAwardRow("u2", "banked", March),
            new BadgePlayerAwardRow("u2", "round-winner", March),
            new BadgePlayerAwardRow("u1", "banked", March)
        ]));

        standings.Select(standing => standing.Item.UserId).Should().Equal("u2", "u1");
        standings.Select(standing => standing.Rank).Should().Equal(1, 2);
    }

    [Fact]
    public void Of_ShouldPutWhoeverGotThereFirstAhead_WhenTheBadgeCountsAreLevel()
    {
        var standings = BadgeStandings.Of(Data(
        [
            Player("u1", "Ada", "Lovelace"),
            Player("u2", "Grace", "Hopper")
        ],
        [
            new BadgePlayerAwardRow("u1", "banked", August),
            new BadgePlayerAwardRow("u2", "banked", March)
        ]));

        standings.Select(standing => standing.Item.UserId).Should().Equal("u2", "u1");
        standings.Select(standing => standing.Rank).Should().Equal(1, 2);
    }

    [Fact]
    public void Of_ShouldGiveGenuinelyLevelPlayersTheSamePosition()
    {
        // The change this makes. The table used to number its rows one by one, so two players who were level on
        // everything were shown different positions decided by their names - while the dashboard tile worked the
        // same players out as sharing. Nine of our own players hold no badges at all.
        var standings = BadgeStandings.Of(Data(
        [
            Player("u1", "Ada", "Lovelace"),
            Player("u2", "Grace", "Hopper"),
            Player("u3", "Alan", "Turing")
        ],
        [
            new BadgePlayerAwardRow("u3", "banked", March)
        ]));

        Standing(standings, "u3").Rank.Should().Be(1);
        Standing(standings, "u1").Rank.Should().Be(2);
        Standing(standings, "u2").Rank.Should().Be(2);
    }

    [Fact]
    public void Of_ShouldLeaveAGapAfterAJointPosition()
    {
        // Two players joint first means nobody is second: the next player is third, as on every other leaderboard
        // in the application.
        var standings = BadgeStandings.Of(Data(
        [
            Player("u1", "Ada", "Lovelace"),
            Player("u2", "Grace", "Hopper"),
            Player("u3", "Alan", "Turing")
        ],
        [
            new BadgePlayerAwardRow("u1", "banked", March),
            new BadgePlayerAwardRow("u2", "banked", March)
        ]));

        Standing(standings, "u1").Rank.Should().Be(1);
        Standing(standings, "u2").Rank.Should().Be(1);
        Standing(standings, "u3").Rank.Should().Be(3);
    }

    [Fact]
    public void Of_ShouldOrderPlayersSharingAPositionByName()
    {
        // Alphabetical order decides how the rows read, never which position they hold - and it is the full name,
        // because two players called Ada Lovelace and Ada Lamarr are both shown as "Ada L".
        var standings = BadgeStandings.Of(Data(
        [
            Player("u1", "Ada", "Lovelace"),
            Player("u2", "Ada", "Lamarr")
        ], []));

        standings.Select(standing => standing.Item.UserId).Should().Equal("u2", "u1");
        standings.Select(standing => standing.Rank).Should().Equal(1, 1);
    }

    #endregion

    private static BadgeLeaderboardData Data(BadgePlayerRow[] players, BadgePlayerAwardRow[] awards) =>
        new(players, awards);

    private static BadgePlayerRow Player(string userId, string? firstName, string? lastName) =>
        new(userId, firstName, lastName);

    private static Ranked<BadgeStanding> Standing(IEnumerable<Ranked<BadgeStanding>> standings, string userId) =>
        standings.Single(standing => standing.Item.UserId == userId);
}
