using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Repositories;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class UserOwnsLeaguesQueryHandler(ILeagueRepository leagueRepository)
    : IRequestHandler<UserOwnsLeaguesQuery, bool>
{
    public async Task<bool> Handle(UserOwnsLeaguesQuery request, CancellationToken cancellationToken)
    {
        return (await leagueRepository.GetLeaguesByAdministratorIdAsync(request.UserId, cancellationToken)).Any();
    }
}
