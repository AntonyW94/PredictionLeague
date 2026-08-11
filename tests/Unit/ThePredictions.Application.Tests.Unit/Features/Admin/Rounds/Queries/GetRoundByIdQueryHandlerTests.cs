using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Queries;

/// <summary>
/// One round and its fixtures, for the administrator's editor - which shows every fixture, including any that have been
/// called off, because putting a postponed fixture back is what an administrator opens this screen to do.
/// </summary>
public class GetRoundByIdQueryHandlerTests
{
    private const int RoundId = 42;

    private static readonly DateTime KickOff = new(2026, 8, 1, 15, 0, 0, DateTimeKind.Utc);

    private readonly IAdminRoundQuery _roundQuery = Substitute.For<IAdminRoundQuery>();
    private readonly IRoundMatchesQuery _roundMatchesQuery = Substitute.For<IRoundMatchesQuery>();
    private readonly GetRoundByIdQueryHandler _handler;

    public GetRoundByIdQueryHandlerTests()
    {
        _handler = new GetRoundByIdQueryHandler(_roundQuery, _roundMatchesQuery);
    }

    [Fact]
    public async Task Handle_ShouldReportTheRoundIsNotFound_WhenThereIsNoSuchRound()
    {
        // Arrange
        GivenRound(null);

        // Act
        var act = () => HandleAsync();

        // Assert - a client asking for a round that does not exist is a 404, not a server fault.
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldNotEvenReadTheFixtures_WhenThereIsNoSuchRound()
    {
        // Arrange
        GivenRound(null);

        // Act
        await Assert.ThrowsAsync<EntityNotFoundException>(HandleAsync);

        // Assert
        await _roundMatchesQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReportTheRoundsDetails()
    {
        // Arrange
        GivenRound(Round());
        GivenMatches();

        // Act
        var details = await HandleAsync();

        // Assert
        details.Round.Id.Should().Be(RoundId);
        details.Round.RoundNumber.Should().Be(4);
        details.Round.Status.Should().Be(RoundStatus.Published);
        details.Round.MatchCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldReturnARoundWithNoFixturesYet()
    {
        // Arrange - a round created before its fixtures are loaded. This used to arrive as a single row of nulls that
        // the mapping had to recognise and throw away.
        GivenRound(Round() with { MatchCount = 0 });
        GivenMatches();

        // Act
        var details = await HandleAsync();

        // Assert
        details.Matches.Should().BeEmpty();
        details.Round.MatchCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldListTheFixturesInKickOffOrder()
    {
        // Arrange
        GivenRound(Round());
        GivenMatches(
            Match(3, KickOff.AddHours(2)),
            Match(1, KickOff),
            Match(2, KickOff.AddHours(1)));

        // Act
        var details = await HandleAsync();

        // Assert
        details.Matches.Select(match => match.Id).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Handle_ShouldOrderFixturesKickingOffTogetherByHomeTeam()
    {
        // A Saturday afternoon is mostly simultaneous kick-offs, so without this the list could reshuffle itself
        // between visits.
        GivenRound(Round());
        GivenMatches(
            Match(1, KickOff) with { HomeTeamName = "Wolves" },
            Match(2, KickOff) with { HomeTeamName = "Arsenal" });

        // Act
        var details = await HandleAsync();

        // Assert
        details.Matches.Select(match => match.Id).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Handle_ShouldStillIncludeAFixtureThatHasBeenCalledOff()
    {
        // The players' view of the same round leaves it out. This screen is where it gets put back.
        GivenRound(Round());
        GivenMatches(
            Match(1, KickOff),
            Match(2, KickOff.AddHours(1)) with { Status = MatchStatus.Postponed });

        // Act
        var details = await HandleAsync();

        // Assert
        details.Matches.Select(match => match.Id).Should().Equal(1, 2);
        details.Matches.Last().Status.Should().Be(MatchStatus.Postponed);
    }

    [Fact]
    public async Task Handle_ShouldReportAFixtureWhoseTeamsAreNotKnownYet()
    {
        // Arrange - a tournament fixture with a placeholder instead of two teams, so every joined team column is null.
        GivenRound(Round());
        GivenMatches(new RoundMatchRow(
            Id: 9, KickOff, MatchNumber: 3,
            HomeTeamId: null, HomeTeamName: null, HomeTeamShortName: null, HomeTeamAbbreviation: null, HomeTeamLogoUrl: null,
            AwayTeamId: null, AwayTeamName: null, AwayTeamShortName: null, AwayTeamAbbreviation: null, AwayTeamLogoUrl: null,
            ActualHomeTeamScore: null, ActualAwayTeamScore: null, MatchStatus.Scheduled,
            PlaceholderHomeName: "Winner of QF1", PlaceholderAwayName: "Winner of QF2", CustomLockTimeUtc: null));

        // Act
        var match = (await HandleAsync()).Matches.Single();

        // Assert
        match.HomeTeamName.Should().BeNull();
        match.PlaceholderHomeName.Should().Be("Winner of QF1");
        match.PlaceholderAwayName.Should().Be("Winner of QF2");
    }

    [Fact]
    public async Task Handle_ShouldReportEachFixturesTeamsScoresAndLockTime()
    {
        // Arrange
        GivenRound(Round());
        GivenMatches(Match(1, KickOff) with
        {
            ActualHomeTeamScore = 2,
            ActualAwayTeamScore = 1,
            Status = MatchStatus.Completed,
            CustomLockTimeUtc = KickOff.AddHours(-1)
        });

        // Act
        var match = (await HandleAsync()).Matches.Single();

        // Assert
        match.MatchDateTimeUtc.Should().Be(KickOff);
        match.MatchNumber.Should().Be(1);
        match.HomeTeamId.Should().Be(10);
        match.HomeTeamName.Should().Be("Arsenal");
        match.HomeTeamShortName.Should().Be("Arsenal");
        match.HomeTeamAbbreviation.Should().Be("ARS");
        match.HomeTeamLogoUrl.Should().Be("arsenal.png");
        match.AwayTeamId.Should().Be(20);
        match.AwayTeamName.Should().Be("Chelsea");
        match.AwayTeamShortName.Should().Be("Chelsea");
        match.AwayTeamAbbreviation.Should().Be("CHE");
        match.AwayTeamLogoUrl.Should().Be("chelsea.png");
        match.ActualHomeTeamScore.Should().Be(2);
        match.ActualAwayTeamScore.Should().Be(1);
        match.Status.Should().Be(MatchStatus.Completed);
        match.CustomLockTimeUtc.Should().Be(KickOff.AddHours(-1));
    }

    [Fact]
    public async Task Handle_ShouldAskForTheRoundRequested()
    {
        // Arrange
        GivenRound(Round());
        GivenMatches();

        // Act
        await HandleAsync();

        // Assert
        await _roundQuery.Received(1).ExecuteAsync(RoundId, Arg.Any<CancellationToken>());
        await _roundMatchesQuery.Received(1).ExecuteAsync(RoundId, Arg.Any<CancellationToken>());
    }

    private void GivenRound(AdminRoundRow? round) =>
        _roundQuery.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(round);

    private void GivenMatches(params RoundMatchRow[] matches) =>
        _roundMatchesQuery.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(matches);

    private static AdminRoundRow Round() =>
        new(RoundId, SeasonId: 7, RoundNumber: 4, "Regular Season - 4",
            KickOff.AddDays(-1), KickOff.AddHours(-2), RoundStatus.Published, MatchCount: 2);

    private static RoundMatchRow Match(int id, DateTime kickOffUtc) =>
        new(id, kickOffUtc, MatchNumber: id,
            HomeTeamId: 10, HomeTeamName: "Arsenal", HomeTeamShortName: "Arsenal", HomeTeamAbbreviation: "ARS", HomeTeamLogoUrl: "arsenal.png",
            AwayTeamId: 20, AwayTeamName: "Chelsea", AwayTeamShortName: "Chelsea", AwayTeamAbbreviation: "CHE", AwayTeamLogoUrl: "chelsea.png",
            ActualHomeTeamScore: null, ActualAwayTeamScore: null, MatchStatus.Scheduled,
            PlaceholderHomeName: null, PlaceholderAwayName: null, CustomLockTimeUtc: null);

    private Task<RoundDetailsDto> HandleAsync() =>
        _handler.Handle(new GetRoundByIdQuery(RoundId), CancellationToken.None);
}
