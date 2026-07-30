using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Predictions.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Predictions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Services;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Predictions.Commands;

public class SubmitPredictionsCommandHandlerTests
{
    private readonly IRoundRepository _roundRepository = Substitute.For<IRoundRepository>();
    private readonly IUserPredictionRepository _userPredictionRepository = Substitute.For<IUserPredictionRepository>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 4, 13, 10, 0, 0, DateTimeKind.Utc));
    private readonly PredictionDomainService _predictionDomainService;
    private readonly SubmitPredictionsCommandHandler _handler;

    public SubmitPredictionsCommandHandlerTests()
    {
        _predictionDomainService = new PredictionDomainService(_dateTimeProvider);
        _handler = new SubmitPredictionsCommandHandler(
            _roundRepository,
            _userPredictionRepository,
            _predictionDomainService);
    }

    private Round CreateRound(int id = 1, DateTime? deadlineUtc = null, IEnumerable<Match?>? matches = null)
    {
        return new Round(
            id: id,
            seasonId: 1,
            roundNumber: 1,
            displayName: "Round 1",
            startDateUtc: _dateTimeProvider.UtcNow.AddDays(-1),
            deadlineUtc: deadlineUtc ?? _dateTimeProvider.UtcNow.AddDays(1),
            status: RoundStatus.Published,
            apiRoundName: null,
            lastReminderSentUtc: null,
            matches: matches);
    }

    private Match CreateMatch(int id, int homeTeamId = 1, int awayTeamId = 2)
    {
        return new Match(
            id: id,
            roundId: 1,
            homeTeamId: homeTeamId,
            awayTeamId: awayTeamId,
            matchDateTimeUtc: _dateTimeProvider.UtcNow.AddDays(1),
            customLockTimeUtc: null,
            status: MatchStatus.Scheduled,
            actualHomeTeamScore: null,
            actualAwayTeamScore: null,
            externalId: null,
            matchNumber: null,
            placeholderHomeName: null,
            placeholderAwayName: null,
            apiRoundName: null);
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenRoundNotFound()
    {
        // Arrange
        var predictions = new List<PredictionSubmissionDto>
        {
            new(MatchId: 1, HomeScore: 2, AwayScore: 1)
        };
        var command = new SubmitPredictionsCommand("user-1", 999, predictions);

        _roundRepository.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((Round?)null);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldUpsertPredictions_WhenRoundExistsAndDeadlineNotPassed()
    {
        // Arrange
        var match = CreateMatch(id: 10);
        var round = CreateRound(id: 1, matches: new[] { match });
        var predictions = new List<PredictionSubmissionDto>
        {
            new(MatchId: 10, HomeScore: 2, AwayScore: 1)
        };
        var command = new SubmitPredictionsCommand("user-1", 1, predictions);

        _roundRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(round);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _userPredictionRepository.Received(1).UpsertBatchAsync(
            Arg.Is<IEnumerable<UserPrediction>>(p => p.Any()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldThrowBusinessRuleViolation_WhenDeadlineHasPassed()
    {
        // Arrange
        var match = CreateMatch(id: 10);
        var round = CreateRound(
            id: 1,
            deadlineUtc: _dateTimeProvider.UtcNow.AddDays(-1),
            matches: new[] { match });
        var predictions = new List<PredictionSubmissionDto>
        {
            new(MatchId: 10, HomeScore: 2, AwayScore: 1)
        };
        var command = new SubmitPredictionsCommand("user-1", 1, predictions);

        _roundRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(round);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<BusinessRuleViolationException>()
            .WithMessage("*deadline*");
    }

    [Fact]
    public async Task Handle_ShouldCallUpsertBatchWithCorrectPredictions_WhenMultiplePredictionsSubmitted()
    {
        // Arrange
        var match1 = CreateMatch(id: 10, homeTeamId: 1, awayTeamId: 2);
        var match2 = CreateMatch(id: 11, homeTeamId: 3, awayTeamId: 4);
        var round = CreateRound(id: 1, matches: new[] { match1, match2 });
        var predictions = new List<PredictionSubmissionDto>
        {
            new(MatchId: 10, HomeScore: 2, AwayScore: 1),
            new(MatchId: 11, HomeScore: 0, AwayScore: 0)
        };
        var command = new SubmitPredictionsCommand("user-1", 1, predictions);

        _roundRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(round);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _userPredictionRepository.Received(1).UpsertBatchAsync(
            Arg.Is<IEnumerable<UserPrediction>>(p => p.Count() == 2),
            Arg.Any<CancellationToken>());
    }
}
