using MediatR;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Application.Services.Boosts;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Seasons.Commands;

public class RecalculateSeasonStatsCommandHandler(
    IRoundRepository roundRepository,
    ILeagueRepository leagueRepository,
    ILeagueStatsRepository leagueStatsRepository,
    IBoostService boostService,
    IRoundResultsService roundResultsService,
    IMediator mediator) : IRequestHandler<RecalculateSeasonStatsCommand>
{
    public async Task Handle(RecalculateSeasonStatsCommand request, CancellationToken cancellationToken)
    {
        var rounds = (await roundRepository.GetAllForSeasonAsync(request.SeasonId, cancellationToken)).Values.ToList();

        var completedRounds = rounds
            .Where(r => r.Status == RoundStatus.Completed)
            .OrderBy(r => r.StartDateUtc)
            .ToList();

        foreach (var round in completedRounds)
        {
            await roundResultsService.RecalculateAsync(round, cancellationToken);
            await leagueRepository.UpdateLeagueRoundResultsAsync(round.Id, cancellationToken);
            await boostService.ApplyRoundBoostsAsync(round.Id, cancellationToken);

            var leagueIds = await leagueRepository.GetLeagueIdsForSeasonAsync(round.SeasonId, cancellationToken);

            foreach (var leagueId in leagueIds)
            {
                var processPrizesCommand = new ProcessPrizesCommand
                {
                    RoundId = round.Id,
                    LeagueId = leagueId
                };

                await mediator.Send(processPrizesCommand, cancellationToken);
            }
        }

        // Rebuild the cached My Leagues ranks from the points this has just recalculated. This is also
        // the backfill route for a season the per-minute job never visits, because that job only walks
        // seasons still flagged active - a finished season's leagues are still on the dashboard.
        await leagueStatsRepository.RefreshSeasonAsync(request.SeasonId, cancellationToken);
    }
}