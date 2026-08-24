using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Common.Interfaces;
using ThePredictions.Contracts.Admin.Matches;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

/// <summary>
/// Applies match scores and everything that follows from them arithmetically: prediction outcomes, round
/// tallies, per-league points, boosts, the round's own status and the cached ranks.
/// </summary>
/// <remarks>
/// The transactional half of <see cref="UpdateMatchResultsCommand"/>, and deliberately only that half.
/// Everything in here is a database write over rows the site is reading constantly, so the transaction is
/// held open for as long as they take and no longer - the round-completion work that used to run inside it
/// is a separate, untransacted command now. See <see cref="CompleteRoundCommand"/>.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record ScoreMatchResultsCommand(
    int RoundId,
    List<MatchResultDto> Matches) : IRequest<MatchResultsOutcome>, ITransactionalRequest;
