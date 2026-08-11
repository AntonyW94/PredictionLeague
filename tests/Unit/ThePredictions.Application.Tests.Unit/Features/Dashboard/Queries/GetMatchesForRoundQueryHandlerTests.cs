using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Dashboard.Queries;

/// <summary>
/// The fixtures of one round as a player sees them: a called-off fixture is left out, which is the one thing that
/// differs from the administrator's view of the same round.
/// </summary>
public class GetMatchesForRoundQueryHandlerTests
{
    private const int RoundId = 42;

    private static readonly DateTime KickOff = new(2026, 8, 1, 15, 0, 0, DateTimeKind.Utc);

    private readonly IRoundMatchesQuery _roundMatchesQuery = Substitute.For<IRoundMatchesQuery>();
    private readonly GetMatchesForRoundQueryHandler _handler;

    public GetMatchesForRoundQueryHandlerTests()
    {
        _handler = new GetMatchesForRoundQueryHandler(_roundMatchesQuery);
    }

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenTheRoundHasNoFixtures()
    {
        // Arrange
        Given();

        // Act
        var matches = await HandleAsync();

        // Assert
        matches.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutAFixtureThatHasBeenCalledOff()
    {
        // A player cannot predict it, so it would sit in the list as an unpredicted, unscored row.
        Given(
            Match(1, KickOff),
            Match(2, KickOff.AddHours(1)) with { Status = MatchStatus.Postponed });

        // Act
        var matches = await HandleAsync();

        // Assert
        matches.Select(match => match.Id).Should().Equal(1);
    }

    [Theory]
    [InlineData(MatchStatus.Scheduled)]
    [InlineData(MatchStatus.InProgress)]
    [InlineData(MatchStatus.Completed)]
    public async Task Handle_ShouldIncludeEveryFixtureThatHasNotBeenCalledOff(MatchStatus status)
    {
        // The statement this replaces named these three rather than the one it meant, so a status added later would
        // have vanished from the list without anybody noticing.
        Given(Match(1, KickOff) with { Status = status });

        // Act
        var matches = await HandleAsync();

        // Assert
        matches.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ShouldListTheFixturesInKickOffOrder()
    {
        // Arrange
        Given(
            Match(3, KickOff.AddHours(2)),
            Match(1, KickOff),
            Match(2, KickOff.AddHours(1)));

        // Act
        var matches = await HandleAsync();

        // Assert
        matches.Select(match => match.Id).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Handle_ShouldOrderFixturesKickingOffTogetherByHomeTeam()
    {
        // Arrange
        Given(
            Match(1, KickOff) with { HomeTeamName = "Wolves" },
            Match(2, KickOff) with { HomeTeamName = "Arsenal" });

        // Act
        var matches = await HandleAsync();

        // Assert
        matches.Select(match => match.Id).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Handle_ShouldReportEachFixturesDetails()
    {
        // Arrange
        Given(Match(1, KickOff) with { CustomLockTimeUtc = KickOff.AddHours(-1) });

        // Act
        var match = (await HandleAsync()).Single();

        // Assert
        match.MatchDateTimeUtc.Should().Be(KickOff);
        match.HomeTeamName.Should().Be("Arsenal");
        match.AwayTeamName.Should().Be("Chelsea");
        match.Status.Should().Be(MatchStatus.Scheduled);
        match.CustomLockTimeUtc.Should().Be(KickOff.AddHours(-1));
    }

    [Fact]
    public async Task Handle_ShouldAskForTheRoundRequested()
    {
        // Arrange
        Given();

        // Act
        await HandleAsync();

        // Assert
        await _roundMatchesQuery.Received(1).ExecuteAsync(RoundId, Arg.Any<CancellationToken>());
    }

    private void Given(params RoundMatchRow[] matches) =>
        _roundMatchesQuery.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(matches);

    private static RoundMatchRow Match(int id, DateTime kickOffUtc) =>
        new(id, kickOffUtc, MatchNumber: id,
            HomeTeamId: 10, HomeTeamName: "Arsenal", HomeTeamShortName: "Arsenal", HomeTeamAbbreviation: "ARS", HomeTeamLogoUrl: "arsenal.png",
            AwayTeamId: 20, AwayTeamName: "Chelsea", AwayTeamShortName: "Chelsea", AwayTeamAbbreviation: "CHE", AwayTeamLogoUrl: "chelsea.png",
            ActualHomeTeamScore: null, ActualAwayTeamScore: null, MatchStatus.Scheduled,
            PlaceholderHomeName: null, PlaceholderAwayName: null, CustomLockTimeUtc: null);

    private Task<IEnumerable<MatchInRoundDto>> HandleAsync() =>
        _handler.Handle(new GetMatchesForRoundQuery(RoundId), CancellationToken.None);
}
