using MediatR;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

/// <summary>
/// Sends the round-results digest email to every user who predicted in the round.
/// Fired automatically when a round completes; idempotent via Round.ResultsDigestSentUtc.
/// Set <see cref="Force"/> to re-send (admin "resend digest" action).
/// </summary>
public record SendRoundDigestEmailsCommand(int RoundId, bool Force = false) : IRequest;
