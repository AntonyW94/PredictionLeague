using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeagueRecordsQuery"/> implementation must return.
///
/// Ten records used to be chosen inside one statement. None of them may be chosen here: an adapter that returned
/// only its own idea of the best round would leave nine rules unreachable and the tenth untestable.
/// </summary>
public abstract class LeagueRecordsQueryConformanceTests
{
    protected abstract ILeagueRecordsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNull_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId + 5_000, CancellationToken.None);

        // Assert
        data.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnWhetherTheLeagueIsFree()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the seeded league is free; the flag is passed through untouched either way.
        data!.IsFree.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnOnlyApprovedMembers()
    {
        // Arrange
        var world = await ArrangeAsync();
        var pendingUserId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, pendingUserId, LeagueMemberStatus.Pending);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the champion is chosen from these, so a pending member cannot win the league.
        data!.ApprovedMembers.Select(member => (member.FirstName, member.LastName))
            .Should().BeEquivalentTo([("Ada", "Lovelace")]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryRoundScoreUnaggregated()
    {
        // Arrange
        var world = await ArrangeAsync();
        var firstRoundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-2), RoundStatus.Completed);
        var secondRoundId = await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, firstRoundId, world.UserId, 9, 18, "DOUBLE");
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, secondRoundId, world.UserId, 7, 7, "NONE");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - every record is chosen from these, so nothing may be summed or ordered on the way out.
        data!.RoundScores.Select(row => row.BoostedPoints).Should().BeEquivalentTo([18, 7]);
        data.RoundScores.Should().OnlyContain(row => row.FirstName == "Ada" && row.LastName == "Lovelace");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachRoundScoresRoundAndStatus()
    {
        // Arrange
        var world = await ArrangeAsync();
        var startDateUtc = new DateTime(2026, 3, 4, 0, 0, 0, DateTimeKind.Utc);
        var roundId = await Seed.AddRoundAsync(
            world.SeasonId, 6, DateTime.UtcNow.AddDays(-1), RoundStatus.InProgress, startDateUtc);
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, roundId, world.UserId, 9, 18, "DOUBLE");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the wins counts need both: only completed rounds count, and a round belongs to the calendar
        // month it started in.
        var score = data!.RoundScores.Single();
        score.RoundId.Should().Be(roundId);
        score.RoundNumber.Should().Be(6);
        score.RoundStatus.Should().Be(RoundStatus.InProgress);
        score.RoundStartDateUtc.Should().BeCloseTo(startDateUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportWhetherThePlayerEnteredTheRound()
    {
        // Arrange - one round predicted, one not.
        var world = await ArrangeAsync();
        var predictedRoundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-2), RoundStatus.Completed);
        var matchId = await Seed.AddMatchAsync(predictedRoundId, world.HomeTeamId, world.AwayTeamId);
        await Seed.AddPredictionAsync(matchId, world.UserId);
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, predictedRoundId, world.UserId, 9, 18, "DOUBLE");

        var skippedRoundId = await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddMatchAsync(skippedRoundId, world.HomeTeamId, world.AwayTeamId);
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, skippedRoundId, world.UserId, 0, 0, "NONE");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the worst-round record excludes rounds nobody entered, and this is the fact it needs.
        data!.RoundScores.Single(row => row.RoundId == predictedRoundId).HasAnyPrediction.Should().BeTrue();
        data.RoundScores.Single(row => row.RoundId == skippedRoundId).HasAnyPrediction.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCountAPredictionInAnotherRound_AsEnteringThisOne()
    {
        // Arrange - a prediction exists, but for a fixture in a different round.
        var world = await ArrangeAsync();
        var scoredRoundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-2), RoundStatus.Completed);
        await Seed.AddLeagueRoundResultAsync(world.LeagueId, scoredRoundId, world.UserId, 0, 0, "NONE");

        var otherRoundId = await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        var otherMatchId = await Seed.AddMatchAsync(otherRoundId, world.HomeTeamId, world.AwayTeamId);
        await Seed.AddPredictionAsync(otherMatchId, world.UserId);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.RoundScores.Single(row => row.RoundId == scoredRoundId).HasAnyPrediction.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnRoundScoresForThisLeagueOnly()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        await Seed.AddLeagueMemberAsync(otherLeagueId, world.UserId);
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddLeagueRoundResultAsync(otherLeagueId, roundId, world.UserId, 40, 80, "DOUBLE");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.RoundScores.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnExactScoresForTheLeaguesApprovedMembersOnly()
    {
        // Arrange - exact scores are league-agnostic, so scoping them is this port's job.
        var world = await ArrangeAsync();
        var roundId = await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddRoundResultAsync(roundId, world.UserId, exactScoreCount: 4);

        var outsiderId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddRoundResultAsync(roundId, outsiderId, exactScoreCount: 9);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        var exact = data!.ExactScores.Single();
        exact.UserId.Should().Be(world.UserId);
        exact.ExactScoreCount.Should().Be(4);
        exact.RoundNumber.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoExactScores_FromAnotherSeason()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(world.CompetitionId, "2027/28");
        var otherRoundId = await Seed.AddRoundAsync(otherSeasonId, 1, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddRoundResultAsync(otherRoundId, world.UserId, exactScoreCount: 9);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.ExactScores.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachPrizeWithItsTypeAndParts()
    {
        // Arrange
        var world = await ArrangeAsync();
        var awardedDateUtc = new DateTime(2026, 4, 5, 12, 0, 0, DateTimeKind.Utc);
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Monthly, 25m);
        await Seed.AddWinningAsync(world.UserId, settingId, 25m, awardedDateUtc, month: 3);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - the prize type has to arrive as the enum. The SQL Server column holds "1" rather than
        // "Monthly", and an adapter that leaked that would leave the label rule unable to tell a monthly prize
        // from a round one.
        var winning = data!.Winnings.Single();
        winning.UserId.Should().Be(world.UserId);
        winning.Amount.Should().Be(25m);
        winning.PrizeType.Should().Be(PrizeType.Monthly);
        winning.Month.Should().Be(3);
        winning.AwardedDateUtc.Should().BeCloseTo(awardedDateUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAPrizesOwnWordingWhenItHasSome()
    {
        // Arrange
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(
            world.LeagueId, PrizeType.Overall, 100m, prizeDescription: "1st Place");
        await Seed.AddWinningAsync(world.UserId, settingId, 100m);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert - returned raw; whether it beats the derived label is the rule.
        data!.Winnings.Single().PrizeDescription.Should().Be("1st Place");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoWording_ForAPrizeWithNone()
    {
        // Arrange
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Round, 10m);
        await Seed.AddWinningAsync(world.UserId, settingId, 10m, roundNumber: 12);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        var winning = data!.Winnings.Single();
        winning.PrizeDescription.Should().BeNull();
        winning.PrizeType.Should().Be(PrizeType.Round);
        winning.RoundNumber.Should().Be(12);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoWinnings_FromAnotherLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.UserId, "Other League");
        var settingId = await Seed.AddLeaguePrizeSettingAsync(otherLeagueId, PrizeType.Overall, 500m);
        await Seed.AddWinningAsync(world.UserId, settingId, 500m);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, CancellationToken.None);

        // Assert
        data!.Winnings.Should().BeEmpty();
    }

    private async Task<RecordsWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new RecordsWorld(
            leagueId, backdrop.CompetitionId, backdrop.SeasonId, backdrop.UserId,
            backdrop.HomeTeamId, backdrop.AwayTeamId);
    }

    private sealed record RecordsWorld(
        int LeagueId,
        int CompetitionId,
        int SeasonId,
        string UserId,
        int HomeTeamId,
        int AwayTeamId);
}
