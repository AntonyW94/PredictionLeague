using MediatR;
using NSubstitute;
using ThePredictions.Application.Features.Badges.Commands;
using ThePredictions.Application.Repositories;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Badges.Commands;

/// <summary>
/// The admin rebuild that awards badges for rounds that finished before a badge existed. It replays
/// every completed round through the normal evaluation rather than having its own scoring rules.
/// </summary>
public class BackfillBadgesCommandHandlerTests
{
    private readonly IBadgeEvaluationRepository _repository = Substitute.For<IBadgeEvaluationRepository>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private readonly BackfillBadgesCommandHandler _handler;

    public BackfillBadgesCommandHandlerTests()
    {
        _handler = new BackfillBadgesCommandHandler(_repository, _mediator);
        _repository.GetCompletedRoundIdsAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    private Task HandleAsync() => _handler.Handle(new BackfillBadgesCommand(), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldDoNothing_WhenNoRoundHasFinished()
    {
        await HandleAsync();

        await _mediator.DidNotReceive().Send(Arg.Any<EvaluateBadgesForRoundCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReplayEveryCompletedRound()
    {
        _repository.GetCompletedRoundIdsAsync(Arg.Any<CancellationToken>()).Returns([3, 1, 2]);

        await HandleAsync();

        await _mediator.Received(1).Send(Arg.Is<EvaluateBadgesForRoundCommand>(c => c.RoundId == 1), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(Arg.Is<EvaluateBadgesForRoundCommand>(c => c.RoundId == 2), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(Arg.Is<EvaluateBadgesForRoundCommand>(c => c.RoundId == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReplayRoundsInTheOrderTheyAreGiven()
    {
        // Some badges depend on a running streak, so replaying out of order would award the wrong
        // ones - the repository returns them in round order and that order is preserved.
        var order = new List<int>();
        _repository.GetCompletedRoundIdsAsync(Arg.Any<CancellationToken>()).Returns([1, 2, 3]);
        await _mediator.Send(Arg.Do<EvaluateBadgesForRoundCommand>(c => order.Add(c.RoundId)), Arg.Any<CancellationToken>());
        order.Clear();

        await HandleAsync();

        Assert.Equal([1, 2, 3], order);
    }
}
