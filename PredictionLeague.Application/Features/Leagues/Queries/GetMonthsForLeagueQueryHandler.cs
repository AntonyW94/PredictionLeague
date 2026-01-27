using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Application.Services;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetMonthsForLeagueQueryHandler : IRequestHandler<GetMonthsForLeagueQuery, IEnumerable<MonthDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;
    private readonly ILeagueMembershipService _membershipService;

    public GetMonthsForLeagueQueryHandler(
        IApplicationReadDbConnection dbConnection,
        ILeagueMembershipService membershipService)
    {
        _dbConnection = dbConnection;
        _membershipService = membershipService;
    }

    public async Task<IEnumerable<MonthDto>> Handle(GetMonthsForLeagueQuery request, CancellationToken cancellationToken)
    {
        await _membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

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

        var months = await _dbConnection.QueryAsync<MonthRow>(sql, cancellationToken, new { request.LeagueId, DraftStatus = nameof(RoundStatus.Draft), CompletedStatus = nameof(RoundStatus.Completed) });
       
        return months.Select(m => new MonthDto(m.Month, CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m.Month), m.RoundsRemaining, m.RoundsCompleted));
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private sealed record MonthRow(int Month, int RoundsRemaining, int RoundsCompleted);
}