using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Services;

namespace ThePredictions.Infrastructure.Services;

/// <inheritdoc cref="IBackgroundCommandDispatcher"/>
public class BackgroundCommandDispatcher(IServiceScopeFactory scopeFactory, ILogger<BackgroundCommandDispatcher> logger) : IBackgroundCommandDispatcher
{
    public void Dispatch<TCommand>(TCommand command) where TCommand : IRequest =>
        // Task.Run rather than an un-awaited call, so not even the synchronous run-up to the command's first
        // await is charged to the request that is trying to return.
        _ = Task.Run(() => SendAsync(command), CancellationToken.None);

    private async Task SendAsync<TCommand>(TCommand command) where TCommand : IRequest
    {
        var commandName = typeof(TCommand).Name;

        try
        {
            // A scope of its own, because the request's one is disposed the moment its response is written and
            // IMediator, the read connection and every repository are scoped. Reusing it would mean resolving
            // the handler's dependencies from a container that has already let them go.
            using var scope = scopeFactory.CreateScope();

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            // CancellationToken.None, not the request's token: that token is cancelled when the response
            // completes, which is precisely the moment this work starts.
            await mediator.Send(command, CancellationToken.None);
        }
        catch (Exception exception)
        {
            // The only place a failure can surface. Nobody is waiting on this task, so an exception left to
            // escape would be an unobserved one - silently swallowed by the runtime with nothing written down.
            logger.LogError(exception, "Background command ({CommandName}) failed after its response was returned", commandName);
        }
    }
}
