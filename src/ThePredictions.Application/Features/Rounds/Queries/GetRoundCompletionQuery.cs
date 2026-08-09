using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Rounds;

namespace ThePredictions.Application.Features.Rounds.Queries;

/// <summary>
/// Prediction-completion overview for a round. When <paramref name="LeagueId"/> is null the view is
/// global (site admin only, every participant in the round's season); when set it is scoped to that
/// league's approved members. Any approved member may read the league view; only an admin or the
/// league owner may then send reminders (<see cref="Contracts.Rounds.RoundCompletionDto.CanSendReminders"/>).
/// </summary>
[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetRoundCompletionQuery(int RoundId, int? LeagueId, string CurrentUserId, bool IsSiteAdmin)
    : IRequest<RoundCompletionDto>;
