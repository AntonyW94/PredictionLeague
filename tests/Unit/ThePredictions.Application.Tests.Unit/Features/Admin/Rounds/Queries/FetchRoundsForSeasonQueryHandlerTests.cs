using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Queries;

/// <summary>The administrator's list of a season's rounds, in the order the endpoint promises.</summary>
public class FetchRoundsForSeasonQueryHandlerTests
{
    private const int SeasonId = 7;

    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly IAdminSeasonRoundsQuery _seasonRoundsQuery = Substitute.For<IAdminSeasonRoundsQuery>();
    private readonly FetchRoundsForSeasonQueryHandler _handler;

    public FetchRoundsForSeasonQueryHandlerTests()
    {
        _handler = new FetchRoundsForSeasonQueryHandler(_seasonRoundsQuery);
    }

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenTheSeasonHasNoRounds()
    {
        // Arrange
        Given();

        // Act
        var rounds = await HandleAsync();

        // Assert
        rounds.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldListTheRoundsByRoundNumber()
    {
        // Arrange - deliberately out of order, as an unordered read may deliver them.
        Given(Round(3), Round(1), Round(2));

        // Act
        var rounds = await HandleAsync();

        // Assert
        rounds.Select(round => round.RoundNumber).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Handle_ShouldListByRoundNumberRatherThanDeadline()
    {
        // A round rescheduled to a later date keeps its place in the season.
        Given(
            Round(1) with { DeadlineUtc = Deadline.AddMonths(6) },
            Round(2));

        // Act
        var rounds = await HandleAsync();

        // Assert
        rounds.Select(round => round.RoundNumber).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Handle_ShouldReportEachRoundsDetails()
    {
        // Arrange
        Given(Round(4) with { ApiRoundName = "Regular Season - 4", Status = RoundStatus.Completed, MatchCount = 10 });

        // Act
        var round = (await HandleAsync()).Single();

        // Assert
        round.Id.Should().Be(104);
        round.SeasonId.Should().Be(SeasonId);
        round.RoundNumber.Should().Be(4);
        round.ApiRoundName.Should().Be("Regular Season - 4");
        round.StartDateUtc.Should().Be(Deadline.AddDays(-1));
        round.DeadlineUtc.Should().Be(Deadline);
        round.Status.Should().Be(RoundStatus.Completed);
        round.MatchCount.Should().Be(10);
    }

    [Fact]
    public async Task Handle_ShouldReportARoundWithNoApiName()
    {
        // A round created by hand rather than imported has no name from the fixtures feed.
        Given(Round(1) with { ApiRoundName = null });

        // Act
        var round = (await HandleAsync()).Single();

        // Assert
        round.ApiRoundName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldAskForTheSeasonRequested()
    {
        // Arrange
        Given();

        // Act
        await HandleAsync();

        // Assert
        await _seasonRoundsQuery.Received(1).ExecuteAsync(SeasonId, Arg.Any<CancellationToken>());
    }

    private void Given(params AdminRoundRow[] rounds) =>
        _seasonRoundsQuery.ExecuteAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(rounds);

    private static AdminRoundRow Round(int roundNumber) =>
        new(100 + roundNumber, SeasonId, roundNumber, "Regular Season - " + roundNumber,
            Deadline.AddDays(-1), Deadline, RoundStatus.Published, MatchCount: 10);

    private Task<IEnumerable<RoundDto>> HandleAsync() =>
        _handler.Handle(new FetchRoundsForSeasonQuery(SeasonId), CancellationToken.None);
}
