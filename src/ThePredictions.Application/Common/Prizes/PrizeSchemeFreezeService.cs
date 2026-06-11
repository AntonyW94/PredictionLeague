using Microsoft.Extensions.Logging;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Common.Prizes;

public class PrizeSchemeFreezeService(
    ILeagueRepository leagueRepository,
    ISeasonRepository seasonRepository,
    IPrizeEvaluator prizeEvaluator,
    IDateTimeProvider dateTimeProvider,
    ILogger<PrizeSchemeFreezeService> logger) : IPrizeSchemeFreezeService
{
    public async Task<bool> TryFreezeAsync(League league, CancellationToken cancellationToken)
    {
        if (league.PrizeSettings.Any())
            return false;

        if (league.PrizeScheme is null || league.EntryDeadlineUtc > dateTimeProvider.UtcNow)
            return false;

        var season = await seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        if (season is null)
            return false;

        var stakePounds = (int)decimal.Truncate(league.Price);
        var adminTopUpPounds = (int)decimal.Truncate(league.PrizeFundOverride ?? 0m);
        var entrantCount = league.Members.Count;
        var numberOfMonths = CountMonths(season.StartDateUtc, season.EndDateUtc);

        var evaluationRequest = PrizeSchemeEvaluationRequest.FromScheme(league.PrizeScheme, stakePounds, adminTopUpPounds, entrantCount, season.NumberOfRounds, numberOfMonths);
        var breakdown = prizeEvaluator.Evaluate(evaluationRequest);

        var settings = PrizeFreezeMapper.ToPrizeSettings(breakdown, league.Id);
        if (settings.Count == 0)
            return false;

        league.DefinePrizes(settings);
        await leagueRepository.UpdateAsync(league, cancellationToken);

        logger.LogInformation("League (ID: {LeagueId}) prize scheme frozen into {PrizeSettingCount} prize settings", league.Id, settings.Count);

        return true;
    }

    private static int CountMonths(DateTime startDateUtc, DateTime endDateUtc)
    {
        var months = 0;
        for (var date = startDateUtc; date <= endDateUtc; date = date.AddMonths(1))
            months++;

        return months;
    }
}
