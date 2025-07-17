using MediatR;

namespace PredictionLeague.Application.Features.Leagues.Commands;

public class ApproveLeagueMemberCommand : IRequest
{
    public int LeagueId { get; }
    public string MemberId { get; }
    public string ApprovingUserId { get; }

    public ApproveLeagueMemberCommand(int leagueId, string memberId, string approvingUserId)
    {
        LeagueId = leagueId;
        MemberId = memberId;
        ApprovingUserId = approvingUserId;
    }
}