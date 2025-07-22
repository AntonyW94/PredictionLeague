using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Contracts.Predictions;

namespace PredictionLeague.Application.Features.Predictions.Queries;

public class GetPredictionPageDataQueryHandler : IRequestHandler<GetPredictionPageDataQuery, PredictionPageDto>
{
    private readonly IRoundRepository _roundRepository;
    private readonly ISeasonRepository _seasonRepository;
    private readonly IMatchRepository _matchRepository;
    private readonly IUserPredictionRepository _predictionRepository;

    public GetPredictionPageDataQueryHandler(
        IRoundRepository roundRepository,
        ISeasonRepository seasonRepository,
        IMatchRepository matchRepository,
        IUserPredictionRepository predictionRepository)
    {
        _roundRepository = roundRepository;
        _seasonRepository = seasonRepository;
        _matchRepository = matchRepository;
        _predictionRepository = predictionRepository;
    }

    public async Task<PredictionPageDto> Handle(GetPredictionPageDataQuery request, CancellationToken cancellationToken)
    {
        var round = await _roundRepository.GetByIdAsync(request.RoundId, cancellationToken) ?? throw new KeyNotFoundException("Round not found.");
        var season = await _seasonRepository.GetByIdAsync(round.SeasonId, cancellationToken) ?? throw new KeyNotFoundException("Season not found.");
        var matches = await _matchRepository.GetByRoundIdAsync(request.RoundId, cancellationToken);
        var userPredictions = await _predictionRepository.FetchByUserIdAndRoundIdAsync(request.UserId, request.RoundId, cancellationToken);

        var pageData = new PredictionPageDto
        {
            RoundId = round.Id,
            RoundNumber = round.RoundNumber,
            SeasonName = season.Name,
            Deadline = round.Deadline,
            IsPastDeadline = round.Deadline < DateTime.UtcNow,
            Matches = matches.Select(m =>
            {
                var prediction = userPredictions.FirstOrDefault(p => p.MatchId == m.Id);
                return new MatchPredictionDto
                {
                    MatchId = m.Id,
                    MatchDateTime = m.MatchDateTime,
                    HomeTeamName = m.HomeTeam!.Name,
                    HomeTeamLogoUrl = m.HomeTeam.LogoUrl,
                    AwayTeamName = m.AwayTeam!.Name,
                    AwayTeamLogoUrl = m.AwayTeam.LogoUrl,
                    PredictedHomeScore = prediction?.PredictedHomeScore,
                    PredictedAwayScore = prediction?.PredictedAwayScore
                };
            }).ToList()
        };

        return pageData;
    }
}