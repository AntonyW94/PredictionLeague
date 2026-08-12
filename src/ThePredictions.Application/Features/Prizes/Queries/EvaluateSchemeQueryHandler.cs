using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Prizes.Queries;

public class EvaluateSchemeQueryHandler(IPrizeSchemeSeasonQuery prizeSchemeSeasonQuery, IPrizeEvaluator evaluator) : IRequestHandler<EvaluateSchemeQuery, PrizeBreakdownDto>
{
    public async Task<PrizeBreakdownDto> Handle(EvaluateSchemeQuery request, CancellationToken cancellationToken)
    {
        var season = await prizeSchemeSeasonQuery.ExecuteAsync(request.SeasonId, cancellationToken);
        Guard.Against.EntityNotFound(request.SeasonId, season, "Season");

        var evaluationRequest = new PrizeSchemeEvaluationRequest
        {
            StakePounds = (int)decimal.Truncate(request.Price),
            AdminTopUpPounds = (int)decimal.Truncate(request.PrizeFundOverride ?? 0m),
            EntrantCount = request.EntrantCount,
            NumberOfRounds = season.NumberOfRounds,
            NumberOfMonths = CountMonths(season.StartDateUtc, season.EndDateUtc),
            Categories = request.Scheme.Categories
                .Select(c => new PrizeSchemeCategoryInput { Category = c.Category, PerEntryPounds = c.PerEntryPounds, RankTableJson = c.RankTableJson })
                .ToList()
        };

        return evaluator.Evaluate(evaluationRequest);
    }

    private static int CountMonths(DateTime startDateUtc, DateTime endDateUtc)
    {
        var months = 0;
        for (var date = startDateUtc; date <= endDateUtc; date = date.AddMonths(1))
            months++;

        return months;
    }

    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    // internal so a test can supply a row; InternalsVisibleTo already exposes this assembly to
    // ThePredictions.Application.Tests.Unit.
    internal sealed class SeasonRow
    {
        public int NumberOfRounds { get; init; }
        public DateTime StartDateUtc { get; init; }
        public DateTime EndDateUtc { get; init; }
    }
}
