using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Leaderboards;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetRoundLeaderboardQueryHandler : IRequestHandler<GetRoundLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    private readonly IUserPredictionRepository _predictionRepository;

    public GetRoundLeaderboardQueryHandler(IUserPredictionRepository predictionRepository)
    {
        _predictionRepository = predictionRepository;
    }

    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(GetRoundLeaderboardQuery request, CancellationToken cancellationToken)
    {
        return await _predictionRepository.FetchRoundLeaderboardAsync(request.LeagueId, request.RoundId, cancellationToken);
    }
}