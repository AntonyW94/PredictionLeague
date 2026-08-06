using System.Diagnostics.CodeAnalysis;
using Ardalis.GuardClauses;
using MediatR;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Guards;

namespace ThePredictions.Application.Features.Prizes.Queries;

public class EvaluateSchemeQueryHandler(IApplicationReadDbConnection dbConnection, IPrizeEvaluator evaluator) : IRequestHandler<EvaluateSchemeQuery, PrizeBreakdownDto>
{
    public async Task<PrizeBreakdownDto> Handle(EvaluateSchemeQuery request, CancellationToken cancellationToken)
    {
        const string seasonSql = @"
            SELECT
                s.[NumberOfRounds],
                s.[StartDateUtc],
                s.[EndDateUtc]
            FROM
                [Seasons] s
            WHERE
                s.[Id] = @SeasonId;";

        var season = await dbConnection.QuerySingleOrDefaultAsync<SeasonRow>(seasonSql, cancellationToken, new { request.SeasonId });
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
    private sealed class SeasonRow
    {
        public int NumberOfRounds { get; init; }
        public DateTime StartDateUtc { get; init; }
        public DateTime EndDateUtc { get; init; }
    }
}
