using MediatR;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record NotifyMemberOfLeagueApprovalCommand(
    string MemberUserId,
    int LeagueId,
    string LeagueName,
    int SeasonId,
    string? LeagueUrlBase) : IRequest;
