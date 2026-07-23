using FluentAssertions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class AwardedBadgeTests
{
    private static readonly DateTime AwardedUtc = new(2026, 5, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_ShouldSetAllProperties_WhenScopeAndProvenanceProvided()
    {
        var badge = AwardedBadge.Create("user-1", "round-winner", AwardedUtc, leagueId: 3, roundId: 12, seasonId: 4, detail: "Round 12");

        badge.BadgeKey.Should().Be("round-winner");
        badge.UserId.Should().Be("user-1");
        badge.AwardedUtc.Should().Be(AwardedUtc);
        badge.LeagueId.Should().Be(3);
        badge.RoundId.Should().Be(12);
        badge.SeasonId.Should().Be(4);
        badge.Detail.Should().Be("Round 12");
    }

    [Fact]
    public void Create_ShouldAllowNullScopeAndProvenance_ForLifetimeBadge()
    {
        var badge = AwardedBadge.Create("user-1", "champion", AwardedUtc);

        badge.LeagueId.Should().BeNull();
        badge.RoundId.Should().BeNull();
        badge.SeasonId.Should().BeNull();
        badge.Detail.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldThrow_WhenUserIdBlank()
    {
        var act = () => AwardedBadge.Create(" ", "champion", AwardedUtc);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenBadgeKeyBlank()
    {
        var act = () => AwardedBadge.Create("user-1", " ", AwardedUtc);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenAwardedUtcDefault()
    {
        var act = () => AwardedBadge.Create("user-1", "champion", default);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_ShouldThrow_WhenLeagueIdNotPositive(int leagueId)
    {
        var act = () => AwardedBadge.Create("user-1", "champion", AwardedUtc, leagueId: leagueId);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_ShouldThrow_WhenRoundIdNotPositive(int roundId)
    {
        var act = () => AwardedBadge.Create("user-1", "sharpshooter-1", AwardedUtc, roundId: roundId);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_ShouldThrow_WhenSeasonIdNotPositive(int seasonId)
    {
        var act = () => AwardedBadge.Create("user-1", "marksman-1", AwardedUtc, seasonId: seasonId);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_ShouldHydrateFromDatabaseValues()
    {
        var badge = new AwardedBadge(7, "user-1", "marksman-2", AwardedUtc, 3, null, 4, "10 exact scores");

        badge.Id.Should().Be(7);
        badge.UserId.Should().Be("user-1");
        badge.BadgeKey.Should().Be("marksman-2");
        badge.AwardedUtc.Should().Be(AwardedUtc);
        badge.LeagueId.Should().Be(3);
        badge.RoundId.Should().BeNull();
        badge.SeasonId.Should().Be(4);
        badge.Detail.Should().Be("10 exact scores");
    }
}
