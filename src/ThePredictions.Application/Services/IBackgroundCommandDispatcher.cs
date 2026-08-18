using MediatR;

namespace ThePredictions.Application.Services;

/// <summary>
/// Sends a command without waiting for it, so the caller's response is not held up by whatever the command
/// talks to.
/// </summary>
/// <remarks>
/// <para>
/// For work the caller has already reported as done: a notification email is the case this exists for. The
/// player has joined, the join is committed, and the HTTP response is theirs to have - it should not also wait
/// on Brevo, which is a third party over the network and has taken five seconds in the past.
/// </para>
/// <para>
/// <b>Fire and forget means exactly that.</b> Nothing is returned, nothing is retried, and a failure reaches
/// the log rather than the caller. It is therefore only correct for work whose loss is a disappointment rather
/// than a fault: an email that never arrives, not a row that never gets written. Anything that must not be lost
/// belongs in the request, or in a scheduled task with a record of what it has done - the pattern
/// <c>SendLeagueWelcomeEmailsCommandHandler</c> follows.
/// </para>
/// </remarks>
public interface IBackgroundCommandDispatcher
{
    /// <summary>
    /// Hands <paramref name="command"/> off to run after the caller has returned. Returns immediately.
    /// </summary>
    void Dispatch<TCommand>(TCommand command) where TCommand : IRequest;
}
