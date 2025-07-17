using MediatR;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Contracts.Admin.Rounds;

namespace PredictionLeague.Application.Features.Admin.Rounds.Queries;

public class GetRoundsForSeasonQueryHandler : IRequestHandler<FetchRoundsForSeasonQuery, IEnumerable<RoundDto>>
{
    private readonly IRoundRepository _roundRepository;
    private readonly IMatchRepository _matchRepository;

    public GetRoundsForSeasonQueryHandler(IRoundRepository roundRepository, IMatchRepository matchRepository)
    {
        _roundRepository = roundRepository;
        _matchRepository = matchRepository;
    }

    public async Task<IEnumerable<RoundDto>> Handle(FetchRoundsForSeasonQuery request, CancellationToken cancellationToken)
    {
        var rounds = await _roundRepository.GetBySeasonIdAsync(request.SeasonId);
        var roundsToReturn = new List<RoundDto>();

        foreach (var round in rounds)
        {
            var matches = await _matchRepository.GetByRoundIdAsync(round.Id);
            
            roundsToReturn.Add(new RoundDto
            {
                Id = round.Id,
                RoundNumber = round.RoundNumber,
                StartDate = round.StartDate,
                Deadline = round.Deadline,
                MatchCount = matches.Count()
            });
        }

        return roundsToReturn;
    }
}