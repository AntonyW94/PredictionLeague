using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

/// <summary>
/// Settles a finished round: prizes, badges, then the results digest and the prize emails.
/// </summary>
/// <remarks>
/// Deliberately not transactional. This step makes outbound HTTP calls - two rounds of email, one per
/// player - and it used to make them from inside the transaction that had just written the round's points,
/// which meant every reader of those rows waited for Brevo. Every step it runs is idempotent, and two of
/// them are already reachable from admin actions that run untransacted, so there is nothing here that
/// needed the transaction it was borrowing.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record CompleteRoundCommand(int RoundId) : IRequest;
