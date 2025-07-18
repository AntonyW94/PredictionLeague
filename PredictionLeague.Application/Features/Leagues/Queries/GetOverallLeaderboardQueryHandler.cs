using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Leaderboards;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetOverallLeaderboardQueryHandler : IRequestHandler<GetOverallLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    private readonly IUserPredictionRepository _predictionRepository;

    public GetOverallLeaderboardQueryHandler(IUserPredictionRepository predictionRepository)
    {
        _predictionRepository = predictionRepository;
    }

    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(GetOverallLeaderboardQuery request, CancellationToken cancellationToken)
    {
        return await _predictionRepository.GetOverallLeaderboardAsync(request.LeagueId);
    }
}