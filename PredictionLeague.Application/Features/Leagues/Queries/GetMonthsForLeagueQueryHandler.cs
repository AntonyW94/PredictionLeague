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

            MonthlyStats AS (
                SELECT 
                    MONTH(r.[StartDate]) AS [Month],
                    SUM (
                            CASE 
                                WHEN r.[Status] <> @CompletedStatus THEN 1 
                                ELSE 0 
                            END
                        ) AS [RoundsRemaining]            
                FROM [Rounds] r
                JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
                WHERE l.[Id] = @LeagueId
                AND r.[Status] <> @DraftStatus
                GROUP BY MONTH(r.[StartDate])
            )

            SELECT
                ms.[Month],
                ms.[RoundsRemaining]
            FROM
                [MonthlyStats] ms,
                [SeasonInfo] si
            ORDER BY
                CASE
                    WHEN ms.[Month] >= si.[StartMonth] THEN 1
                    ELSE 2
                END,
                ms.[Month]";

        var months = await _dbConnection.QueryAsync<MonthRow>(sql, cancellationToken, new { request.LeagueId, DraftStatus = nameof(RoundStatus.Draft), CompletedStatus = nameof(RoundStatus.Completed)
        });
        return months.Select(m => new MonthDto(m.Month, CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m.Month), m.RoundsRemaining));
    }

    private sealed record MonthRow(int Month, int RoundsRemaining);
}