using MediatR;
using PredictionLeague.Application.Repositories;

namespace PredictionLeague.Application.Features.Admin.Users.Queries;

public class UserOwnsLeaguesQueryHandler : IRequestHandler<UserOwnsLeaguesQuery, bool>
{
    private readonly ILeagueRepository _leagueRepository;

    public UserOwnsLeaguesQueryHandler(ILeagueRepository leagueRepository)
    {
        _leagueRepository = leagueRepository;
    }

    public async Task<bool> Handle(UserOwnsLeaguesQuery request, CancellationToken cancellationToken)
    {
        return (await _leagueRepository.GetLeaguesByAdministratorIdAsync(request.UserId, cancellationToken)).Any();
    }
}