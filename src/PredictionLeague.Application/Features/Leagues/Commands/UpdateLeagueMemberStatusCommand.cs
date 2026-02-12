using MediatR;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public record UpdateLeagueMemberStatusCommand(
    int LeagueId,
    string MemberId,
    string UpdatingUserId,
    LeagueMemberStatus NewStatus
) : IRequest;