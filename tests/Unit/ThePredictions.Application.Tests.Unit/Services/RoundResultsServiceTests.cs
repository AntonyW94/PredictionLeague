using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Services;

/// <summary>
/// Rebuilding every player's stored tally for a round. This was one <c>MERGE</c> that both counted and stored; the
/// counting is <c>OutcomeTally</c> now and the storing is a plain upsert, so what is left here is the sequence between
/// them - which fixtures are read, and one row per player out the other side.
/// </summary>
public class RoundResultsServiceTests
{
    private static readonly DateTime Deadline = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly IUserPredictionRepository _predictions = Substitute.For<IUserPredictionRepository>();
    private readonly IRoundRepository _rounds = Substitute.For<IRoundRepository>();
    private readonly RoundResultsService _service;

    public RoundResultsServiceTests()
    {
        _service = new RoundResultsService(_predictions, _rounds);
    }

    [Fact]
    public async Task RecalculateAsync_ShouldStoreOneTallyPerPlayer()
    {
        // Arrange - two players across two fixtures of the same round.
        GivenPredictions(
            Prediction(1, "user-1", PredictionOutcome.ExactScore),
            Prediction(2, "user-1", PredictionOutcome.Incorrect),
            Prediction(1, "user-2", PredictionOutcome.CorrectResult));

        // Act
        await _service.RecalculateAsync(RoundWithTwoMatches(), CancellationToken.None);

        // Assert
        var tallies = CapturedTallies();
        tallies.Should().HaveCount(2);

        var mine = tallies.Single(tally => tally.UserId == "user-1");
        mine.Counts.ExactScoreCount.Should().Be(1);
        mine.Counts.IncorrectCount.Should().Be(1);

        tallies.Single(tally => tally.UserId == "user-2").Counts.CorrectResultCount.Should().Be(1);
    }

    [Fact]
    public async Task RecalculateAsync_ShouldStoreTheTalliesAgainstTheRound()
    {
        // Arrange
        GivenPredictions(Prediction(1, "user-1", PredictionOutcome.ExactScore));

        // Act
        await _service.RecalculateAsync(RoundWithTwoMatches(), CancellationToken.None);

        // Assert
        await _rounds.Received(1).UpdateRoundResultsAsync(
            7, Arg.Any<IEnumerable<RoundResultTally>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecalculateAsync_ShouldTallyEveryFixtureInTheRound()
    {
        // A tally is the whole round's answer for that player, so it is rebuilt from every fixture rather than from the
        // ones whose scores happen to have just changed.
        GivenPredictions(Prediction(1, "user-1", PredictionOutcome.ExactScore));

        // Act
        await _service.RecalculateAsync(RoundWithTwoMatches(), CancellationToken.None);

        // Assert
        await _predictions.Received(1).GetByMatchIdsAsync(
            Arg.Is<IEnumerable<int>>(ids => ids.OrderBy(id => id).SequenceEqual(new[] { 1, 2 })),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecalculateAsync_ShouldStoreNothing_WhenNobodyPredicted()
    {
        // Arrange
        GivenPredictions();

        // Act
        await _service.RecalculateAsync(RoundWithTwoMatches(), CancellationToken.None);

        // Assert
        CapturedTallies().Should().BeEmpty();
    }

    [Fact]
    public async Task RecalculateAsync_ShouldNotReadPredictions_ForARoundWithNoFixtures()
    {
        // Arrange - a draft round with nothing in it. The read takes an IN clause, so asking about no fixtures is a trip
        // for nothing.
        var round = Round([]);

        // Act
        await _service.RecalculateAsync(round, CancellationToken.None);

        // Assert
        await _predictions.DidNotReceiveWithAnyArgs().GetByMatchIdsAsync(default!, CancellationToken.None);
        await _rounds.DidNotReceiveWithAnyArgs().UpdateRoundResultsAsync(default, default!, CancellationToken.None);
    }

    private void GivenPredictions(params UserPrediction[] predictions) =>
        _predictions.GetByMatchIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>()).Returns(predictions);

    private List<RoundResultTally> CapturedTallies()
    {
        var calls = _rounds.ReceivedCalls()
            .Where(call => call.GetMethodInfo().Name == nameof(IRoundRepository.UpdateRoundResultsAsync))
            .ToList();

        calls.Should().ContainSingle();

        return ((IEnumerable<RoundResultTally>)calls[0].GetArguments()[1]!).ToList();
    }

    /// <summary>
    /// A prediction whose outcome is set the way the round-processing path sets it - through the domain rule, from a
    /// finished fixture - rather than assigned, so these tests cannot disagree with how outcomes are really decided.
    /// </summary>
    private static UserPrediction Prediction(int matchId, string userId, PredictionOutcome outcome)
    {
        var clock = new TestDateTimeProvider(Deadline);
        var prediction = UserPrediction.Create(userId, matchId, 2, 1, clock);

        var (homeScore, awayScore) = outcome switch
        {
            PredictionOutcome.ExactScore => (2, 1),
            PredictionOutcome.CorrectResult => (3, 1),
            _ => (0, 4)
        };

        prediction.SetOutcome(MatchStatus.Completed, homeScore, awayScore, clock);
        prediction.Outcome.Should().Be(outcome, "the fixture scores above are meant to produce this outcome");

        return prediction;
    }

    private static Round RoundWithTwoMatches() => Round([Match(1), Match(2)]);

    private static Round Round(List<Match> matches) =>
        new(id: 7, seasonId: 3, roundNumber: 5, displayName: "Gameweek 5",
            startDateUtc: Deadline.AddHours(1), deadlineUtc: Deadline, status: RoundStatus.InProgress,
            apiRoundName: null, lastReminderSentUtc: null, matches: matches);

    private static Match Match(int id) =>
        new(id: id, roundId: 7, homeTeamId: 1, awayTeamId: 2, matchDateTimeUtc: Deadline.AddHours(1),
            customLockTimeUtc: null, status: MatchStatus.Scheduled, actualHomeTeamScore: null,
            actualAwayTeamScore: null, externalId: null, matchNumber: null, placeholderHomeName: null,
            placeholderAwayName: null, apiRoundName: null);
}
