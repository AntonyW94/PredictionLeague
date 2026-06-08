using MediatR;

namespace ThePredictions.Application.Features.Leagues.Commands;

public record NotifyLeagueAdminOfJoinRequestCommand(string AdministratorUserId,
    string LeagueName,
    int SeasonId,
    string NewMemberFirstName,
    string NewMemberLastName,
    string? LeagueUrlBase = null) : IRequest;