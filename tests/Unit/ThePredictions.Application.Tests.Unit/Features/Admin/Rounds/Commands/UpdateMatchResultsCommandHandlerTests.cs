using MediatR;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Contracts.Admin.Matches;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

/// <summary>
/// The entry point both callers use, and nothing more than a sequence: score the results inside a
/// transaction, then - only if that finished the round, and only once the transaction has committed -
/// settle it. The settlement sends a season's worth of email, which is why it must not be inside.
/// </summary>
public class UpdateMatchResultsCommandHandlerTests
{
    private const int RoundId = 100;
    private const int MatchId = 500;

    private static readonly List<MatchResultDto> Results = [new(MatchId, 2, 1, MatchStatus.Completed)];

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly UpdateMatchResultsCommandHandler _handler;

    public UpdateMatchResultsCommandHandlerTests()
    {
        _handler = new UpdateMatchResultsCommandHandler(_mediator);
        GivenScoringReports(roundFinished: false);
    }

    private void GivenScoringReports(bool roundFinished) =>
        _mediator.Send(Arg.Any<ScoreMatchResultsCommand>(), Arg.Any<CancellationToken>())
            .Returns(new MatchResultsOutcome(roundFinished));

    private Task HandleAsync() =>
        _handler.Handle(new UpdateMatchResultsCommand(RoundId, Results), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldScoreTheResults()
    {
        await HandleAsync();

        await _mediator.Received(1).Send(
            Arg.Is<ScoreMatchResultsCommand>(c => c.RoundId == RoundId && c.Matches == Results),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSettleTheRound_WhenScoringFinishedIt()
    {
        GivenScoringReports(roundFinished: true);

        await HandleAsync();

        await _mediator.Received(1).Send(
            Arg.Is<CompleteRoundCommand>(c => c.RoundId == RoundId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSettleTheRoundOnlyAfterScoringIt()
    {
        // Scoring is the transactional half. Settling before it returned would put the email sends back
        // inside the transaction holding write locks on the rows the whole site is reading.
        GivenScoringReports(roundFinished: true);

        await HandleAsync();

        Received.InOrder(() =>
        {
            _mediator.Send(Arg.Any<ScoreMatchResultsCommand>(), Arg.Any<CancellationToken>());
            _mediator.Send(Arg.Any<CompleteRoundCommand>(), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_ShouldNotSettleTheRound_WhenItIsStillInPlay()
    {
        GivenScoringReports(roundFinished: false);

        await HandleAsync();

        await _mediator.DidNotReceive().Send(Arg.Any<CompleteRoundCommand>(), Arg.Any<CancellationToken>());
    }
}
