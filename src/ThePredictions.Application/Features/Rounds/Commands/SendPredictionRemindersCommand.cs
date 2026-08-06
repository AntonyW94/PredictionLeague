using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Rounds;

namespace ThePredictions.Application.Features.Rounds.Commands;

/// <summary>
/// Sends an ad-hoc "you are missing predictions" reminder to the given players for a round. When
/// <paramref name="LeagueId"/> is null the caller must be a site admin; when set, an admin or that
/// league's owner. Sends are deduped per (round, player): anyone reminded within the throttle window,
/// or who no longer has any missing fixtures, is skipped.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record SendPredictionRemindersCommand(
    int RoundId,
    int? LeagueId,
    IReadOnlyList<string> UserIds,
    string CurrentUserId,
    bool IsSiteAdmin) : IRequest<SendPredictionRemindersResultDto>;
