using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetMonthsForLeagueQueryHandler(
    IApplicationReadDbConnection dbConnection,
    ILeagueMembershipService membershipService) : IRequestHandler<GetMonthsForLeagueQuery, IEnumerable<MonthDto>>
{
    public async Task<IEnumerable<MonthDto>> Handle(GetMonthsForLeagueQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        const string sql = @"
            WITH SeasonInfo AS (
                SELECT
                    MONTH(MIN(r.[StartDateUtc])) AS [StartMonth]
                FROM [Rounds] r
                JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
                WHERE l.[Id] = @LeagueId
            ),

            MonthlyAggregates AS (
                SELECT 
                    MONTH(r.[StartDateUtc]) AS [Month],

                    SUM(CASE 
                        WHEN r.[Status] <> @CompletedStatus THEN 1 
                        ELSE 0 
                    END) AS [RoundsRemaining],

                    SUM(CASE 
                        WHEN r.[Status] = @CompletedStatus THEN 1 
                        ELSE 0 
                    END) AS [RoundsCompleted],

                    SUM(CASE 
                        WHEN r.[Status] <> @DraftStatus THEN 1 
                        ELSE 0 
                    END) AS [NonDraftCount]

                FROM [Rounds] r
                JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
                WHERE l.[Id] = @LeagueId
                GROUP BY MONTH(r.[StartDateUtc])
            )

           SELECT
                ma.[Month],
                ma.[RoundsRemaining],
                ma.[RoundsCompleted]
            FROM
                [MonthlyAggregates] ma,
                [SeasonInfo] si
            WHERE 
                ma.[NonDraftCount] > 0
            ORDER BY
                CASE
                    WHEN ma.[Month] >= si.[StartMonth] THEN 1
                    ELSE 2
                END,
                ma.[Month]";

        var months = await dbConnection.QueryAsync<MonthRow>(sql, cancellationToken, new { request.LeagueId, DraftStatus = nameof(RoundStatus.Draft), CompletedStatus = nameof(RoundStatus.Completed) });
       
        return months.Select(m => new MonthDto(m.Month, CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m.Month), m.RoundsRemaining, m.RoundsCompleted));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private sealed record MonthRow(int Month, int RoundsRemaining, int RoundsCompleted);
}
