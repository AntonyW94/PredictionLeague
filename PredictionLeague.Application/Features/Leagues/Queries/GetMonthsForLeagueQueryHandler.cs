using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;
using System.Globalization;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetMonthsForLeagueQueryHandler : IRequestHandler<GetMonthsForLeagueQuery, IEnumerable<MonthDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetMonthsForLeagueQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<MonthDto>> Handle(GetMonthsForLeagueQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH SeasonInfo AS (
                SELECT
                    MONTH(MIN(r.[StartDate])) AS [StartMonth]
                FROM [Rounds] r
                JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
                WHERE l.[Id] = @LeagueId
            ),

            DistinctMonths AS (
                SELECT DISTINCT
                    MONTH(r.[StartDate]) AS [Month]
                FROM [Rounds] r
                JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
                WHERE l.[Id] = @LeagueId
                AND r.[Status] <> @DraftStatus
            )

            SELECT
                dm.[Month]
            FROM
                [DistinctMonths] dm,
                [SeasonInfo] si
            ORDER BY
                CASE
                    WHEN dm.[Month] >= si.[StartMonth] THEN 1
                    ELSE 2
                END,
                dm.[Month]";

        var months = await _dbConnection.QueryAsync<int>(sql, cancellationToken, new { request.LeagueId, DraftStatus = nameof(RoundStatus.Draft) });
        return months.Select(m => new MonthDto(m, CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m)));
    }
}