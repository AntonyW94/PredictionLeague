using MediatR;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Features.Admin.Rounds.Strategies;
using ThePredictions.Application.Repositories;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

public class ProcessPrizesCommandHandler(
    IEnumerable<IPrizeStrategy> prizeStrategies,
    ILeagueRepository leagueRepository,
    IPrizeSchemeFreezeService prizeSchemeFreezeService) : IRequestHandler<ProcessPrizesCommand, Unit>
{
    public async Task<Unit> Handle(ProcessPrizesCommand request, CancellationToken cancellationToken)
    {
        var league = await leagueRepository.GetByIdWithAllDataAsync(request.LeagueId, cancellationToken);
        if (league == null)
            return Unit.Value;

        // Safety net: the scheduled freeze-prizes task normally freezes the scheme shortly after
        // the entry deadline, but if it hasn't run yet the first prize processing does it lazily.
        if (!league.PrizeSettings.Any())
            await prizeSchemeFreezeService.TryFreezeAsync(league, cancellationToken);

        if (!league.PrizeSettings.Any())
            return Unit.Value;

        foreach (var prizeSetting in league.PrizeSettings)
        {
            var strategy = prizeStrategies.FirstOrDefault(s => s.PrizeType == prizeSetting.PrizeType);
            if (strategy != null)
                await strategy.AwardPrizes(request, cancellationToken);
        }

        return Unit.Value;
    }
}
