using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

/// <summary>
/// Builds the prize-won data for every user holding a (non-zero) <c>Winning</c> in any league of
/// the given round's season, grouped by user with one entry per prize they have won. Each prize
/// carries an <c>AlreadyNotified</c> flag (from the <c>PrizeNotifications</c> sent-log) so the send
/// command can skip prizes a winner has already been told about.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetPrizeWinnersForRoundQuery(int RoundId) : IRequest<IReadOnlyList<PrizeWinner>>;
