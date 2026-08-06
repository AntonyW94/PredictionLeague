using System.Diagnostics.CodeAnalysis;
using MediatR;

namespace ThePredictions.Application.Features.Leagues.Commands;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record NotifyLeagueAdminOfJoinRequestCommand(string AdministratorUserId,
    string LeagueName,
    int SeasonId,
    string NewMemberFirstName,
    string NewMemberLastName) : IRequest;
