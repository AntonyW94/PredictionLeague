using FluentAssertions;
using ThePredictions.Application.Features.Admin.Seasons.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ISeasonsQuery"/> implementation must return: the seasons, their rounds, and their fixtures' teams,
/// with none of the counting done.
/// </summary>
public abstract class SeasonsQueryConformanceTests
{
    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    protected abstract ISeasonsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_WhenThereAreNoSeasons()
    {
        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert
        data.Seasons.Should().BeEmpty();
        data.Rounds.Should().BeEmpty();
        data.Fixtures.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachSeasonWithItsCompetition()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var season = (await Query.ExecuteAsync(CancellationToken.None)).Seasons.Single();

        // Assert - the competition type arrives typed rather than as the number stored in the column.
        season.Id.Should().Be(backdrop.SeasonId);
        season.Name.Should().Be("2026/27");
        season.CompetitionId.Should().Be(backdrop.CompetitionId);
        season.CompetitionName.Should().NotBeNullOrWhiteSpace();
        season.CompetitionType.Should().BeOneOf(CompetitionType.League, CompetitionType.Tournament);
        season.NumberOfRounds.Should().Be(38);
        season.IsActive.Should().BeTrue();
        season.StartDateUtc.Should().NotBe(default);
        season.EndDateUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEverySeasonOfEveryCompetition()
    {
        // Arrange - the single-season screen picks one out of this set, so all of them have to arrive.
        var backdrop = await Seed.AddBackdropAsync();
        var secondSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        var otherCompetitionId = await Seed.AddCompetitionAsync("CUP");
        var cupSeasonId = await Seed.AddSeasonAsync(otherCompetitionId, "Cup 2026");

        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert
        data.Seasons.Select(season => season.Id)
            .Should().BeEquivalentTo([backdrop.SeasonId, secondSeasonId, cupSeasonId]);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnASeasonThatIsNoLongerActive()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var retiredSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2024/25", isActive: false);

        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert - an administrator's list is where a finished season is looked back at.
        data.Seasons.Single(season => season.Id == retiredSeasonId).IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountTheSeasonsPassHolders()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);
        await Seed.AddSeasonPassAsync(otherUserId, backdrop.SeasonId);

        // Act
        var season = (await Query.ExecuteAsync(CancellationToken.None)).Seasons.Single();

        // Assert - a count of rows in a scoped set with no classification in it, which is why it stays in the read.
        season.PassHolderCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryRoundWithItsStatusAsAnEnum()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline, RoundStatus.Completed);
        await Seed.AddRoundAsync(backdrop.SeasonId, 2, Deadline.AddDays(7), RoundStatus.Draft);

        // Act
        var rounds = (await Query.ExecuteAsync(CancellationToken.None)).Rounds;

        // Assert - counting each state is a rule, and the statements this replaces wrote every state name in as a literal.
        rounds.Should().HaveCount(2);
        rounds.Single(round => round.RoundNumber == 1).Status.Should().Be(RoundStatus.Completed);
        rounds.Single(round => round.RoundNumber == 2).Status.Should().Be(RoundStatus.Draft);
        rounds.Should().OnlyContain(round => round.SeasonId == backdrop.SeasonId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachFixturesTwoTeamsWithItsRoundNumber()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 4, Deadline);

        await Seed.AddMatchAsync(roundId, backdrop.HomeTeamId, backdrop.AwayTeamId);

        // Act
        var fixture = (await Query.ExecuteAsync(CancellationToken.None)).Fixtures.Single();

        // Assert - which round a fixture is in comes back because "the season's first round" is a rule.
        fixture.SeasonId.Should().Be(backdrop.SeasonId);
        fixture.RoundNumber.Should().Be(4);
        fixture.HomeTeamId.Should().Be(backdrop.HomeTeamId);
        fixture.AwayTeamId.Should().Be(backdrop.AwayTeamId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAFixtureWhoseTeamsAreNotKnownYet()
    {
        // Arrange - a knockout tie before it is settled.
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);

        await Seed.AddMatchAsync(roundId, homeTeamId: null, awayTeamId: null);

        // Act
        var fixture = (await Query.ExecuteAsync(CancellationToken.None)).Fixtures.Single();

        // Assert - it arrives with nothing in it rather than being filtered out, because whether a placeholder counts
        // towards the season's team total is a rule.
        fixture.HomeTeamId.Should().BeNull();
        fixture.AwayTeamId.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnRoundsAndFixturesOfEverySeason()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        var firstRoundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, Deadline);
        var otherRoundId = await Seed.AddRoundAsync(otherSeasonId, 1, Deadline.AddYears(1));

        await Seed.AddMatchAsync(firstRoundId, backdrop.HomeTeamId, backdrop.AwayTeamId);
        await Seed.AddMatchAsync(otherRoundId, backdrop.AwayTeamId, backdrop.HomeTeamId);

        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert - the list shows every season's counts at once, so scoping to one season would not serve it.
        data.Rounds.Select(round => round.SeasonId).Should().BeEquivalentTo([backdrop.SeasonId, otherSeasonId]);
        data.Fixtures.Select(fixture => fixture.SeasonId).Should().BeEquivalentTo([backdrop.SeasonId, otherSeasonId]);
    }
}
