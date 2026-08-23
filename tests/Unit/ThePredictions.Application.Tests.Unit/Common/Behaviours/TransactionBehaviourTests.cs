using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using ThePredictions.Application.Common.Behaviours;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Application.Configuration;
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

    // High enough that a test transaction never trips the slow-transaction warning by accident, so
    // the tests that count log calls are not at the mercy of how busy the build agent is.
    private const int NeverSlow = 60_000;

    private TransactionBehaviour<SampleCommand, string> BuildBehaviour(int slowTransactionThresholdMilliseconds = NeverSlow) =>
        new(_transactionContext,
            Options.Create(new QueryMonitoringSettings { SlowTransactionThresholdMilliseconds = slowTransactionThresholdMilliseconds }),
            _logger);

    private Task<string> HandleAsync(RequestHandlerDelegate<string>? next = null, int slowTransactionThresholdMilliseconds = NeverSlow) =>
        BuildBehaviour(slowTransactionThresholdMilliseconds)
            .Handle(new SampleCommand(), next ?? (_ => Task.FromResult("done")), CancellationToken.None);

    private int WarningCount() => _logger.ReceivedCalls()
        .Count(c => c.GetMethodInfo().Name == nameof(ILogger.Log)
                    && (LogLevel)c.GetArguments()[0]! == LogLevel.Warning);

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

    [Fact]
    public async Task Handle_ShouldWarn_WhenTheTransactionIsHeldOpenForTheThreshold()
    {
        // Threshold zero makes every transaction count as slow, which is the only way to exercise the
        // warning deterministically.
        await HandleAsync(slowTransactionThresholdMilliseconds: 0);

        WarningCount().Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldNotWarn_WhenTheTransactionIsComfortablyUnderTheThreshold()
    {
        await HandleAsync();

        WarningCount().Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldStillWarn_WhenTheCommandFailedAfterHoldingTheTransactionOpen()
    {
        // A transaction that was held open and then rolled back blocked readers for just as long as
        // one that committed, so the duration has to be reported either way.
        var act = () => HandleAsync(_ => throw new InvalidOperationException("nope"), slowTransactionThresholdMilliseconds: 0);

        await act.Should().ThrowAsync<InvalidOperationException>();
        WarningCount().Should().Be(1);
    }
}
