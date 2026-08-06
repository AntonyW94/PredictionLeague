using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

/// <summary>
/// Sends the celebratory "Prize Won" email to every winner in the round's season, one grouped email
/// per winner covering all the prizes they have won. Fired automatically when a round completes,
/// after the results digest. Idempotent via the <c>PrizeNotifications</c> sent-log, so re-running
/// prize processing never double-notifies. Set <see cref="Force"/> to re-send to every current
/// winner regardless of the log (admin "resend prize emails" action).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record SendPrizeNotificationsCommand(int RoundId, bool Force = false) : IRequest;
