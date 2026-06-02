using MediatR;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Features.Admin.Rounds.Strategies;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Features.Admin.Rounds.Commands;

public class ProcessPrizesCommandHandler(
    IEnumerable<IPrizeStrategy> prizeStrategies,
    ILeagueRepository leagueRepository,
    ISeasonRepository seasonRepository,
    IPrizeEvaluator prizeEvaluator,
    IDateTimeProvider dateTimeProvider) : IRequestHandler<ProcessPrizesCommand, Unit>
{
    public async Task<Unit> Handle(ProcessPrizesCommand request, CancellationToken cancellationToken)
    {
        var league = await leagueRepository.GetByIdWithAllDataAsync(request.LeagueId, cancellationToken);
        if (league == null)
            return Unit.Value;

        if (!league.PrizeSettings.Any())
            await TryFreezeSchemeAsync(league, cancellationToken);

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

    /// <summary>
    /// Lazily freezes the prize scheme into concrete settings the first time prizes are processed
    /// after the entry deadline. Idempotent: once settings exist this is skipped.
    /// </summary>
    private async Task TryFreezeSchemeAsync(League league, CancellationToken cancellationToken)
    {
        if (league.PrizeScheme is null || league.EntryDeadlineUtc > dateTimeProvider.UtcNow)
            return;

        var season = await seasonRepository.GetByIdAsync(league.SeasonId, cancellationToken);
        if (season is null)
            return;

        var stakePounds = (int)decimal.Truncate(league.Price);
        var entrantCount = league.Members.Count;
        var numberOfMonths = CountMonths(season.StartDateUtc, season.EndDateUtc);

        var evaluationRequest = PrizeSchemeEvaluationRequest.FromScheme(league.PrizeScheme, stakePounds, entrantCount, season.NumberOfRounds, numberOfMonths);
        var breakdown = prizeEvaluator.Evaluate(evaluationRequest);

        var settings = PrizeFreezeMapper.ToPrizeSettings(breakdown, league.Id);
        if (settings.Count == 0)
            return;

        league.DefinePrizes(settings);
        await leagueRepository.UpdateAsync(league, cancellationToken);
    }

    private static int CountMonths(DateTime startDateUtc, DateTime endDateUtc)
    {
        var months = 0;
        for (var date = startDateUtc; date <= endDateUtc; date = date.AddMonths(1))
            months++;

        return months;
    }
}
