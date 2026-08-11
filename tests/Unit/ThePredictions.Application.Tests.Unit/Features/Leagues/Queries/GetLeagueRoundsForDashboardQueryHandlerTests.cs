using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// The rounds a member can pick from on a league's dashboard.
///
/// Shares its read with the league dashboard itself, which lists every round - so the interesting tests here are the
/// ones about which rounds this caller drops.
/// </summary>
public class GetLeagueRoundsForDashboardQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string UserId = "user-me";

    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILeagueRoundsQuery _roundsQuery = Substitute.For<ILeagueRoundsQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetLeagueRoundsForDashboardQueryHandler _handler;

    public GetLeagueRoundsForDashboardQueryHandlerTests()
    {
        _handler = new GetLeagueRoundsForDashboardQueryHandler(_roundsQuery, _membershipService);
    }

    [Fact]
    public async Task Handle_ShouldCheckMembership_BeforeReadingAnything()
    {
        // Arrange
        _membershipService
            .EnsureApprovedMemberAsync(LeagueId, UserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _roundsQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldOfferPublishedAndCompletedRounds()
    {
        // Arrange
        Given(Round(1, RoundStatus.Completed), Round(2, RoundStatus.Published));

        // Act
        var rounds = (await HandleAsync()).ToList();

        // Assert
        rounds.Select(round => round.RoundNumber).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Handle_ShouldNotOfferADraftRound()
    {
        // Arrange
        Given(Round(1, RoundStatus.Published), Round(2, RoundStatus.Draft));

        // Act
        var rounds = (await HandleAsync()).ToList();

        // Assert - a draft is not yet something players can see.
        rounds.Select(round => round.RoundNumber).Should().Equal(1);
    }

    [Fact]
    public async Task Handle_ShouldNotOfferARoundInProgress()
    {
        // Arrange
        Given(Round(1, RoundStatus.Completed), Round(2, RoundStatus.InProgress));

        // Act
        var rounds = (await HandleAsync()).ToList();

        // Assert - preserved from the old Status IN (@Published, @Completed), and flagged in the plan as a question:
        // a round in play is arguably the one most worth looking at.
        rounds.Select(round => round.RoundNumber).Should().Equal(1);
    }

    [Fact]
    public async Task Handle_ShouldListRoundsNewestFirst()
    {
        // Arrange
        Given(Round(1, RoundStatus.Completed), Round(3, RoundStatus.Published), Round(2, RoundStatus.Completed));

        // Act
        var rounds = (await HandleAsync()).ToList();

        // Assert
        rounds.Select(round => round.RoundNumber).Should().Equal(3, 2, 1);
    }

    [Fact]
    public async Task Handle_ShouldCarryEachRoundsDetails()
    {
        // Arrange
        Given(Round(4, RoundStatus.Completed, matchCount: 10, apiRoundName: "Matchday 4"));

        // Act
        var round = (await HandleAsync()).Single();

        // Assert
        round.MatchCount.Should().Be(10);
        round.ApiRoundName.Should().Be("Matchday 4");
        round.Status.Should().Be(RoundStatus.Completed);
    }

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenNoRoundCanBePicked()
    {
        // Arrange
        Given(Round(1, RoundStatus.Draft));

        // Act
        var rounds = await HandleAsync();

        // Assert
        rounds.Should().BeEmpty();
    }

    private void Given(params LeagueRoundRow[] rounds)
    {
        _roundsQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(rounds);
    }

    private async Task<IEnumerable<RoundDto>> HandleAsync() =>
        await _handler.Handle(new GetLeagueRoundsForDashboardQuery(LeagueId, UserId), CancellationToken.None);

    private static LeagueRoundRow Round(
        int roundNumber,
        RoundStatus status,
        int matchCount = 0,
        string? apiRoundName = null) =>
        new(
            roundNumber,
            1,
            roundNumber,
            apiRoundName,
            SeasonStart.AddDays(roundNumber),
            SeasonStart.AddDays(roundNumber),
            status,
            matchCount);
}
