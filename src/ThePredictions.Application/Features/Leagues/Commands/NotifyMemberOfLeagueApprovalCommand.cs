using MediatR;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record NotifyMemberOfLeagueApprovalCommand(
    string MemberUserId,
    string LeagueName,
    int SeasonId) : IRequest;
