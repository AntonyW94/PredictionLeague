using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Common.Behaviours;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Application.Data;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Behaviours;

/// <summary>
/// Wraps any command marked as transactional so its writes either all land or none do. There is no
/// rollback call to make - the transaction is abandoned uncommitted and unwinds when the scope is
/// disposed - so what this has to get right is never committing after a failure.
/// </summary>
public class TransactionBehaviourTests
{
    public record SampleCommand : IRequest<string>, ITransactionalRequest;

    private readonly IDbTransactionContext _transactionContext = Substitute.For<IDbTransactionContext>();
    private readonly ILogger<TransactionBehaviour<SampleCommand, string>> _logger =
        Substitute.For<ILogger<TransactionBehaviour<SampleCommand, string>>>();

    private readonly TransactionBehaviour<SampleCommand, string> _behaviour;

    public TransactionBehaviourTests()
    {
        _behaviour = new TransactionBehaviour<SampleCommand, string>(_transactionContext, _logger);
    }

    private Task<string> HandleAsync(RequestHandlerDelegate<string>? next = null) =>
        _behaviour.Handle(new SampleCommand(), next ?? (_ => Task.FromResult("done")), CancellationToken.None);

    [Fact]
    public async Task Handle_ShouldPassTheHandlersAnswerStraightBack()
    {
        var result = await HandleAsync();

        result.Should().Be("done");
    }

    [Fact]
    public async Task Handle_ShouldOpenATransactionBeforeRunningTheCommand()
    {
        // Opening it afterwards would leave the handler's first writes outside the transaction.
        var openedBeforeHandler = false;

        await HandleAsync(_ =>
        {
            openedBeforeHandler = _transactionContext.ReceivedCalls()
                .Any(c => c.GetMethodInfo().Name == nameof(IDbTransactionContext.BeginAsync));
            return Task.FromResult("done");
        });

        openedBeforeHandler.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldCommitOnlyAfterTheCommandHasFinished()
    {
        var committedDuringHandler = false;

        await HandleAsync(_ =>
        {
            committedDuringHandler = _transactionContext.ReceivedCalls()
                .Any(c => c.GetMethodInfo().Name == nameof(IDbTransactionContext.CommitAsync));
            return Task.FromResult("done");
        });

        committedDuringHandler.Should().BeFalse();
        await _transactionContext.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNeverCommit_WhenTheCommandFails()
    {
        // This is the whole point: a half-finished command must not be made permanent.
        var act = () => HandleAsync(_ => throw new InvalidOperationException("nope"));

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _transactionContext.DidNotReceiveWithAnyArgs().CommitAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldLetTheFailureThroughUnchanged()
    {
        // The exception has to reach the API layer intact so it maps to the right status code.
        var act = () => HandleAsync(_ => throw new InvalidOperationException("nope"));

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("nope");
    }

    [Fact]
    public async Task Handle_ShouldRecordTheFailure()
    {
        var act = () => HandleAsync(_ => throw new InvalidOperationException("nope"));

        await act.Should().ThrowAsync<InvalidOperationException>();
        _logger.ReceivedWithAnyArgs().Log(default, default, default!, default, default!);
    }
}
