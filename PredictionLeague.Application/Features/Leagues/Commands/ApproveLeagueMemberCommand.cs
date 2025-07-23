using MediatR;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public record ApproveLeagueMemberCommand(
    int LeagueId,
    string MemberId,
    string ApprovingUserId
) : IRequest;