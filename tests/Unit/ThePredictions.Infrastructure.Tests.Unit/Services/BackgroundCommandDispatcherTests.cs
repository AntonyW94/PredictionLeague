using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Infrastructure.Services;
using Xunit;

namespace ThePredictions.Infrastructure.Tests.Unit.Services;

/// <summary>
/// The point of this class is what it does NOT do to its caller: a command that fails must not surface, and a
/// command that is slow must not be waited for. Both are asserted here, because both are the reason a player
/// joining a league no longer waits on Brevo.
/// </summary>
public class BackgroundCommandDispatcherTests
{
    private readonly IServiceScope _scope = Substitute.For<IServiceScope>();
    private readonly IServiceScopeFactory _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly IServiceProvider _serviceProvider = Substitute.For<IServiceProvider>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly RecordingLogger<BackgroundCommandDispatcher> _logger = new();
    private readonly BackgroundCommandDispatcher _dispatcher;

    public BackgroundCommandDispatcherTests()
    {
        _serviceProvider.GetService(typeof(IMediator)).Returns(_mediator);
        _scope.ServiceProvider.Returns(_serviceProvider);
        _scopeFactory.CreateScope().Returns(_scope);

        _dispatcher = new BackgroundCommandDispatcher(_scopeFactory, _logger);
    }

    [Fact]
    public async Task Dispatch_ShouldSendTheCommand_WhenDispatched()
    {
        var command = new TestCommand();

        _dispatcher.Dispatch(command);

        await WaitUntilAsync(() => _mediator.ReceivedCalls().Any());

        await _mediator.Received(1).Send(command, CancellationToken.None);
    }

    /// <summary>
    /// A scope of its own, disposed when it is finished with. The request's scope is gone by the time this
    /// runs, so resolving from it would hand the handler dependencies that have already been disposed.
    /// </summary>
    [Fact]
    public async Task Dispatch_ShouldResolveFromItsOwnScopeAndDisposeIt_WhenDispatched()
    {
        _dispatcher.Dispatch(new TestCommand());

        await WaitUntilAsync(() => _scope.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IDisposable.Dispose)));

        _scopeFactory.Received(1).CreateScope();
        _scope.Received(1).Dispose();
    }

    /// <summary>
    /// The caller's token is cancelled the moment its response completes, which is exactly when this work
    /// starts. Passing it through would cancel every notification the instant it was handed over.
    /// </summary>
    [Fact]
    public async Task Dispatch_ShouldNotSendACancellableToken_WhenDispatched()
    {
        _dispatcher.Dispatch(new TestCommand());

        await WaitUntilAsync(() => _mediator.ReceivedCalls().Any());

        var token = (CancellationToken)_mediator.ReceivedCalls().Single().GetArguments()[1]!;
        token.CanBeCanceled.Should().BeFalse();
    }

    [Fact]
    public async Task Dispatch_ShouldLogAnErrorAndNotThrow_WhenTheCommandFails()
    {
        var failure = new InvalidOperationException("Brevo API Key is not configured.");

        _mediator.Send(Arg.Any<IRequest>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(failure));

        // Not wrapped in an assertion that it does not throw: nobody is awaiting the dispatched work, so an
        // escaping exception could not reach this line anyway. The log IS the observable behaviour.
        _dispatcher.Dispatch(new TestCommand());

        await WaitUntilAsync(() => _logger.Entries.Count > 0);

        var entry = _logger.Entries.Single();
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(failure);
        entry.Message.Should().Contain(nameof(TestCommand));
    }

    /// <summary>
    /// A scope that cannot even be created is still the dispatcher's problem to swallow, not its caller's.
    /// </summary>
    [Fact]
    public async Task Dispatch_ShouldLogAnError_WhenTheScopeCannotBeCreated()
    {
        var failure = new ObjectDisposedException("Container");

        _scopeFactory.CreateScope().Returns(_ => throw failure);

        _dispatcher.Dispatch(new TestCommand());

        await WaitUntilAsync(() => _logger.Entries.Count > 0);

        _logger.Entries.Single().Exception.Should().BeSameAs(failure);
    }

    /// <summary>
    /// Polls rather than awaits, because Dispatch returns void by design - there is no task to hand back.
    /// Generous on the ceiling and short on the interval, so a pass is quick and a genuine failure is not a
    /// hang.
    /// </summary>
    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            if (condition())
                return;

            await Task.Delay(10, CancellationToken.None);
        }

        throw new TimeoutException("The dispatched command did not run within five seconds.");
    }

    private sealed record TestCommand : IRequest;
}
