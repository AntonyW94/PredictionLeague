using MediatR;
using ThePredictions.Application.Repositories;

namespace ThePredictions.Application.Features.Admin.Users.Queries;

public class UserOwnsLeaguesQueryHandler(ILeagueRepository leagueRepository)
    : IRequestHandler<UserOwnsLeaguesQuery, bool>
{
    public async Task<bool> Handle(UserOwnsLeaguesQuery request, CancellationToken cancellationToken)
    {
        return (await leagueRepository.GetLeaguesByAdministratorIdAsync(request.UserId, cancellationToken)).Any();
    }
}
