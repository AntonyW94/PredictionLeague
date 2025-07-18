using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Leaderboards;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetMonthlyLeaderboardQueryHandler : IRequestHandler<GetMonthlyLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    private readonly IUserPredictionRepository _predictionRepository;

    public GetMonthlyLeaderboardQueryHandler(IUserPredictionRepository predictionRepository)
    {
        _predictionRepository = predictionRepository;
    }

    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(GetMonthlyLeaderboardQuery request, CancellationToken cancellationToken)
    {
        return await _predictionRepository.GetMonthlyLeaderboardAsync(request.LeagueId, request.Month);
    }
}