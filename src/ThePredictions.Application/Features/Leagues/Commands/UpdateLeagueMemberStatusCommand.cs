using MediatR;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record UpdateLeagueMemberStatusCommand(
    int LeagueId,
    string MemberId,
    string UpdatingUserId,
    LeagueMemberStatus NewStatus
) : IRequest;