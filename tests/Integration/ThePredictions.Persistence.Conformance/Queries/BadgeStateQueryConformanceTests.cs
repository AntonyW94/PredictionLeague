using FluentAssertions;
using ThePredictions.Application.Features.Badges.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IBadgeStateQuery"/> implementation must return.
///
/// Badge progress is never stored, so this read is the whole raw material behind it: every award, every round with what
/// the player did in it, and the two things that are not about rounds. What it must <b>not</b> do is any of the working
/// out - there is no longest run, no "their latest season", no ever-present arithmetic and no abbreviated name here,
/// because all four are rules.
/// </summary>
public abstract class BadgeStateQueryConformanceTests
{
    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime AwardedUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    protected abstract IBadgeStateQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    #region The player

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_ForAnIdThatMatchesNoPlayer()
    {
        // Act
        var data = await Query.ExecuteAsync("no-such-user", CancellationToken.None);

        // Assert - the page still has to render, so this is empty rather than absent.
        data.OwnerFirstName.Should().BeNull();
        data.OwnerLastName.Should().BeNull();
        data.Awards.Should().BeEmpty();
        data.LeaguesJoined.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnBothPartsOfThePlayersNameUnabbreviated()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - "Ada L" is a rule, so the read hands over the parts rather than the result.
        data.OwnerFirstName.Should().Be("Ada");
        data.OwnerLastName.Should().Be("Lovelace");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountOnlyTheLeaguesTheyWereApprovedFor()
    {
        // Arrange - one approved, one still pending, and one they were rejected from.
        var backdrop = await Seed.AddBackdropAsync();

        var approvedLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Approved League");
        var pendingLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Pending League");
        var rejectedLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Rejected League");

        await Seed.AddLeagueMemberAsync(approvedLeagueId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(pendingLeagueId, backdrop.UserId, LeagueMemberStatus.Pending);
        await Seed.AddLeagueMemberAsync(rejectedLeagueId, backdrop.UserId, LeagueMemberStatus.Rejected);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - asking to join is not joining.
        data.LeaguesJoined.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCountAnotherPlayersLeagues()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, otherUserId);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        data.LeaguesJoined.Should().Be(0);
    }

    #endregion

    #region Awards

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOneRowPerAward()
    {
        // Arrange - the same badge won in two different rounds, which is how a repeatable badge is stored.
        var backdrop = await Seed.AddBackdropAsync();

        var firstRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var secondRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));

        await Seed.AddUserBadgeAsync(backdrop.UserId, "round-winner", AwardedUtc, firstRoundId);
        await Seed.AddUserBadgeAsync(backdrop.UserId, "round-winner", AwardedUtc.AddDays(7), secondRoundId);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - ungrouped, because the badges page counts the awards and the leaderboard counts the badges.
        data.Awards.Should().HaveCount(2);
        data.Awards.Select(award => award.BadgeKey).Should().AllBe("round-winner");
        data.Awards.Select(award => award.AwardedUtc).Should().BeEquivalentTo([AwardedUtc, AwardedUtc.AddDays(7)]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnAnotherPlayersAwards()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        await Seed.AddUserBadgeAsync(otherUserId, "founder", AwardedUtc);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        data.Awards.Should().BeEmpty();
    }

    #endregion

    #region Rounds

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoRounds_WhenThereAreNone()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        data.Rounds.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryRoundOfEverySeasonWithItsStatus()
    {
        // Arrange - two seasons, because which season a metric is about is a rule and the read cannot pre-empt it.
        var backdrop = await Seed.AddBackdropAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        var completedRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline, RoundStatus.Completed);
        var draftRoundId = await Seed.AddRoundAsync(otherSeasonId, 1, Deadline.AddYears(1), RoundStatus.Draft);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - even a draft, because the read has no way to know which badge is asking.
        data.Rounds.Should().HaveCount(2);
        data.Rounds.Single(round => round.SeasonId == backdrop.SeasonId).Status.Should().Be(RoundStatus.Completed);
        data.Rounds.Single(round => round.SeasonId == otherSeasonId).Status.Should().Be(RoundStatus.Draft);
        data.Rounds.Select(round => round.RoundNumber).Should().AllBeEquivalentTo(1);

        // Two rounds with the same number in different seasons must not be confused with each other.
        completedRoundId.Should().NotBe(draftRoundId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSayWhetherARoundHasBeenScoredForAnybodyAtAll()
    {
        // Arrange - one round scored for somebody else, one scored for nobody.
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var scoredRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));

        await Seed.AddRoundResultAsync(scoredRoundId, otherUserId, exactScoreCount: 2);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - somebody else's result still makes the round one this player was absent from rather than one that
        // never happened, and that is the difference between a broken streak and a skipped round.
        data.Rounds.Single(round => round.RoundNumber == 1).HasAnyResult.Should().BeTrue();
        data.Rounds.Single(round => round.RoundNumber == 2).HasAnyResult.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoExactScoreCount_ForARoundThePlayerWasNotScoredIn()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        await Seed.AddRoundResultAsync(roundId, otherUserId, exactScoreCount: 2);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - nothing, not zero. Absent and scored-nothing are different states and only one of them decides
        // which season a player's totals are about.
        data.Rounds.Single().UserExactScoreCount.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheirExactScoreCount_IncludingNone()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var scoringRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var blankRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));

        await Seed.AddRoundResultAsync(scoringRoundId, backdrop.UserId, exactScoreCount: 3);
        await Seed.AddRoundResultAsync(blankRoundId, backdrop.UserId, exactScoreCount: 0);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        data.Rounds.Single(round => round.RoundNumber == 1).UserExactScoreCount.Should().Be(3);
        data.Rounds.Single(round => round.RoundNumber == 2).UserExactScoreCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountTheMatchesInEachRound()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var emptyRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));

        await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        await Seed.AddMatchAsync(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - a round with no fixtures loaded yet counts none, which is a state the ever-present rule has to
        // cope with rather than a state the read should hide.
        data.Rounds.Single(round => round.RoundNumber == 1).MatchCount.Should().Be(2);
        data.Rounds.Single(round => round.RoundNumber == 2).MatchCount.Should().Be(0);
        emptyRoundId.Should().BePositive();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountOnlyTheirOwnPredictionsInEachRound()
    {
        // Arrange - two matches, this player predicted one of them and somebody else predicted both.
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var firstMatchId = await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        var secondMatchId = await Seed.AddMatchAsync(roundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        await Seed.AddPredictionAsync(firstMatchId, backdrop.UserId);
        await Seed.AddPredictionAsync(firstMatchId, otherUserId);
        await Seed.AddPredictionAsync(secondMatchId, otherUserId);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - one of two, so this round cost them the ever-present badge. Counting the crowd's predictions would
        // have handed it to them.
        var round = data.Rounds.Single();
        round.MatchCount.Should().Be(2);
        round.UserPredictionCount.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCountPredictionsFromAnotherRound()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        var firstRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var secondRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7));

        var firstMatchId = await Seed.AddMatchAsync(firstRoundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        await Seed.AddMatchAsync(secondRoundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        await Seed.AddPredictionAsync(firstMatchId, backdrop.UserId);

        // Act
        var data = await Query.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        data.Rounds.Single(round => round.RoundNumber == 1).UserPredictionCount.Should().Be(1);
        data.Rounds.Single(round => round.RoundNumber == 2).UserPredictionCount.Should().Be(0);
    }

    #endregion
}
