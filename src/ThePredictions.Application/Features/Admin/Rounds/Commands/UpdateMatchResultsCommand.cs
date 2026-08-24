using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Admin.Matches;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

/// <summary>
/// Applies a set of match results and, if that finished the round, settles it.
/// </summary>
/// <remarks>
/// The entry point for both callers - the admin Enter Results screen and the per-minute live-scores job -
/// and no longer transactional itself. It sequences two commands that are: <see cref="ScoreMatchResultsCommand"/>,
/// whose writes are the transaction, and then <see cref="CompleteRoundCommand"/>, which sends email and
/// therefore must not be inside one. Sequencing them here rather than at the two call sites keeps "if the
/// round finished, settle it" stated once.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record UpdateMatchResultsCommand(
    int RoundId,
    List<MatchResultDto> Matches) : IRequest;
