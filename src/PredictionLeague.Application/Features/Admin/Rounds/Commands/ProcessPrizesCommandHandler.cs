using MediatR;
using PredictionLeague.Application.Features.Admin.Rounds.Strategies;
using PredictionLeague.Application.Repositories;

namespace PredictionLeague.Application.Features.Admin.Rounds.Commands;

public class ProcessPrizesCommandHandler : IRequestHandler<ProcessPrizesCommand, Unit>
{
    private readonly IEnumerable<IPrizeStrategy> _prizeStrategies;
    private readonly ILeagueRepository _leagueRepository;
  
    public ProcessPrizesCommandHandler(IEnumerable<IPrizeStrategy> prizeStrategies, ILeagueRepository leagueRepository)
    {
        _prizeStrategies = prizeStrategies;
        _leagueRepository = leagueRepository;
    }

    public async Task<Unit> Handle(ProcessPrizesCommand request, CancellationToken cancellationToken)
    {
        var league = await _leagueRepository.GetByIdWithAllDataAsync(request.LeagueId, cancellationToken);
        if (league == null || !league.PrizeSettings.Any())
            return Unit.Value;
        
        foreach (var prizeSetting in league.PrizeSettings)
        {
            var strategy = _prizeStrategies.FirstOrDefault(s => s.PrizeType == prizeSetting.PrizeType);
            if (strategy != null)
                await strategy.AwardPrizes(request, cancellationToken);
        }
        
        return Unit.Value;
    }
}