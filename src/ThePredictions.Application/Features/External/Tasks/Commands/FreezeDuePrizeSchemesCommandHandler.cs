using MediatR;
using Microsoft.Extensions.Logging;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;

namespace ThePredictions.Application.Features.External.Tasks.Commands;

public class FreezeDuePrizeSchemesCommandHandler(
    ILeagueRepository leagueRepository,
    IPrizeSchemeFreezeService prizeSchemeFreezeService,
    IDateTimeProvider dateTimeProvider,
    ILogger<FreezeDuePrizeSchemesCommandHandler> logger) : IRequestHandler<FreezeDuePrizeSchemesCommand, FreezeDuePrizeSchemesResult>
{
    public async Task<FreezeDuePrizeSchemesResult> Handle(FreezeDuePrizeSchemesCommand request, CancellationToken cancellationToken)
    {
        var dueLeagueIds = (await leagueRepository.GetLeagueIdsDueForPrizeFreezeAsync(dateTimeProvider.UtcNow, cancellationToken)).ToList();
        if (dueLeagueIds.Count == 0)
            return new FreezeDuePrizeSchemesResult(LeaguesDue: 0, LeaguesFrozen: 0);

        var frozenCount = 0;

        foreach (var leagueId in dueLeagueIds)
        {
            var league = await leagueRepository.GetByIdWithAllDataAsync(leagueId, cancellationToken);
            if (league == null)
                continue;

            if (await prizeSchemeFreezeService.TryFreezeAsync(league, cancellationToken))
                frozenCount++;
        }

        logger.LogInformation("Scheduled prize freeze processed {LeaguesDue} due leagues and froze {LeaguesFrozen}", dueLeagueIds.Count, frozenCount);

        return new FreezeDuePrizeSchemesResult(LeaguesDue: dueLeagueIds.Count, LeaguesFrozen: frozenCount);
    }
}
