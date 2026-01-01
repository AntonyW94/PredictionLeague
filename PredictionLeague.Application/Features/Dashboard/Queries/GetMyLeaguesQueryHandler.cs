using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class GetMyLeaguesQueryHandler : IRequestHandler<GetMyLeaguesQuery, IEnumerable<MyLeagueDto>>
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public GetMyLeaguesQueryHandler(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<IEnumerable<MyLeagueDto>> Handle(GetMyLeaguesQuery request, CancellationToken cancellationToken)
    {
        //const string sql = @"
        //    WITH LeagueMemberCounts AS (
        //        SELECT 
        //            [LeagueId],
        //            COUNT([UserId]) AS [MemberCount]
        //        FROM 
        //            [LeagueMembers]
        //        WHERE 
        //            [Status] = @ApprovedStatus
        //        GROUP BY 
        //            [LeagueId]
        //    ),

        //    LeagueUserPoints AS (
        //        SELECT
        //            lm.UserId,
        //            lm.LeagueId,
        //            COALESCE(SUM(lrr.[BoostedPoints]), 0) AS [TotalPoints]
        //        FROM 
        //      [LeagueMembers] lm
        //        LEFT JOIN 
        //      [LeagueRoundResults] lrr ON lm.[UserId] = lrr.[UserId] AND lrr.[LeagueId] = lm.[LeagueId]
        //        WHERE 
        //      lm.[Status] = @ApprovedStatus
        //        GROUP BY 
        //            lm.[UserId],
        //      lm.[LeagueId]
        //    ),

        //    RankedUsers AS (
        //        SELECT
        //            [UserId],
        //            [LeagueId],
        //            RANK() OVER (PARTITION BY [LeagueId] ORDER BY [TotalPoints] DESC) AS [OverallRank]
        //        FROM [LeagueUserPoints]
        //    )

        //    SELECT
        //        l.[Id],
        //        l.[Name],
        //        s.[Name] AS [SeasonName],
        //        lm.[Status],
        //        ru.[OverallRank] AS [Rank],
        //        COALESCE(lmc.[MemberCount], 0) AS [MemberCount]
        //    FROM 
        //        [Leagues] l
        //    JOIN 
        //        [Seasons] s ON l.[SeasonId] = s.[Id]
        //    JOIN 
        //        [LeagueMembers] lm ON l.[Id] = lm.[LeagueId]
        //    LEFT JOIN
        //        [LeagueMemberCounts] lmc ON l.[Id] = lmc.[LeagueId]
        //    LEFT JOIN
        //        [RankedUsers] ru ON ru.[UserId] = lm.[UserId] AND ru.[LeagueId] = l.[Id]
        //    WHERE 
        //        lm.[UserId] = @UserId
        //    ORDER BY 
        //        s.[StartDate] DESC, 
        //     l.[Price] DESC,
        //        COALESCE(lmc.[MemberCount], 0) DESC,
        //        l.[Name];";

        const string sql = @"
        WITH MyLeagues AS (
            SELECT 
                l.[Id] AS LeagueId,
                l.[Name] AS LeagueName,
                l.[Price],
                s.[Id] AS SeasonId,
                s.[Name] AS SeasonName,
                lm.[UserId],
                lm.[Status]
            FROM [LeagueMembers] lm
            JOIN [Leagues] l ON lm.[LeagueId] = l.[Id]
            JOIN [Seasons] s ON l.[SeasonId] = s.[Id]
            WHERE lm.[UserId] = @UserId AND lm.[Status] = @ApprovedStatus
        ),

        -- 1. Determine the 'Current Round' for every Season involved
        -- We prioritize 'In Progress' rounds (Oldest first), then 'Completed' rounds (Newest first)
        ActiveRounds AS (
            SELECT 
                r.[SeasonId],
                r.[Id] AS RoundId,
                r.[RoundNumber],
                r.[StartDate],
                ROW_NUMBER() OVER (
                    PARTITION BY r.[SeasonId] 
                    ORDER BY 
                        -- Priority: 0 = In Progress, 1 = Completed, 2 = Future
                        CASE 
                            WHEN r.[Status] <> @CompletedStatus AND r.[StartDate] <= GETDATE() THEN 0 
                            WHEN r.[Status] = @CompletedStatus THEN 1 
                            ELSE 2 
                        END ASC,
                        -- Tiebreaker 1 (In Progress): Oldest first
                        CASE WHEN r.[Status] <> @CompletedStatus AND r.[StartDate] <= GETDATE() THEN r.[StartDate] END ASC,
                        -- Tiebreaker 2 (Completed): Newest first
                        r.[StartDate] DESC
                ) as [PriorityRank]
            FROM [Rounds] r
            WHERE 
                r.[Status] <> @DraftStatus
                AND r.[SeasonId] IN (SELECT DISTINCT [SeasonId] FROM [MyLeagues])
        ),

        -- 2. Overall Rankings CTE
        OverallRanks AS (
            SELECT 
                lrr.[LeagueId],
                lrr.[UserId],
                RANK() OVER (PARTITION BY lrr.[LeagueId] ORDER BY SUM(lrr.[BoostedPoints]) DESC) as Rank
            FROM 
                [LeagueRoundResults] lrr
            WHERE 
                lrr.[LeagueId] IN (SELECT [LeagueId] FROM [MyLeagues])
            GROUP BY 
                lrr.[LeagueId], 
                lrr.[UserId]
        ),

        -- 3. Round Rankings CTE (Specific to the Active Round found above)
        RoundRanks AS (
            SELECT 
                lrr.[LeagueId],
                lrr.[UserId],
                lrr.[RoundId],
                RANK() OVER (PARTITION BY lrr.[LeagueId] ORDER BY lrr.[BoostedPoints] DESC) as Rank
            FROM 
                [LeagueRoundResults] lrr
            JOIN 
                [ActiveRounds] ar ON lrr.[RoundId] = ar.[RoundId]
            WHERE 
                lrr.[LeagueId] IN (SELECT [LeagueId] FROM [MyLeagues]) 
                AND ar.[PriorityRank] = 1 -- Only look at the single active round per season
        ),

        -- 4. Month Rankings CTE (Specific to the Month of the Active Round)
        MonthRanks AS (
            SELECT 
                lrr.[LeagueId],
                lrr.[UserId],
                RANK() OVER (PARTITION BY lrr.[LeagueId] ORDER BY SUM(lrr.[BoostedPoints]) DESC) as Rank
            FROM 
                [LeagueRoundResults] lrr
            JOIN 
                [Rounds] r ON lrr.[RoundId] = r.[Id]
            JOIN 
                [ActiveRounds] ar ON r.[SeasonId] = ar.[SeasonId] AND ar.[PriorityRank] = 1
            WHERE 
                lrr.[LeagueId] IN (SELECT [LeagueId] FROM [MyLeagues])
                AND MONTH(r.[StartDate]) = MONTH(ar.[StartDate])
                AND YEAR(r.[StartDate]) = YEAR(ar.[StartDate])
            GROUP BY 
                lrr.[LeagueId], 
                lrr.[UserId]
        ),

        -- 5. Simple Member Counts
        MemberCounts AS (
            SELECT 
                [LeagueId],
                COUNT(*) as Total
            FROM 
                [LeagueMembers]
            WHERE 
                [Status] = @ApprovedStatus 
            AND 
                [LeagueId] IN (SELECT [LeagueId] FROM [MyLeagues])
            GROUP BY 
                [LeagueId]
        )

        -- FINAL SELECT: Stitch it all together
        SELECT 
            l.[LeagueId] AS Id,
            l.[LeagueName] AS Name,
            l.[SeasonName],
            
            -- Rankings
            rr.[Rank] AS RoundRank,
            mr.[Rank] AS MonthRank,
            o.[Rank] AS Rank,
          
            -- Display Info
            CASE WHEN ar.[RoundId] IS NOT NULL THEN 'Round ' + CAST(ar.[RoundNumber] AS VARCHAR(10)) ELSE NULL END AS CurrentRound,
            CASE WHEN ar.[RoundId] IS NOT NULL THEN DATENAME(MONTH, ar.[StartDate]) ELSE NULL END AS CurrentMonth,
            ISNULL(mc.[Total], 0) AS MemberCount

        FROM [MyLeagues] l
        LEFT JOIN [ActiveRounds] ar ON l.[SeasonId] = ar.[SeasonId] AND ar.[PriorityRank] = 1
        LEFT JOIN [OverallRanks] o ON l.[LeagueId] = o.[LeagueId] AND l.[UserId] = o.[UserId]
        LEFT JOIN [RoundRanks] rr ON l.[LeagueId] = rr.[LeagueId] AND l.[UserId] = rr.[UserId]
        LEFT JOIN [MonthRanks] mr ON l.[LeagueId] = mr.[LeagueId] AND l.[UserId] = mr.[UserId]
        LEFT JOIN [MemberCounts] mc ON l.[LeagueId] = mc.[LeagueId]

        ORDER BY 
            ar.[StartDate] DESC,
            l.[Price] DESC,
            l.[LeagueName]";

        return await _dbConnection.QueryAsync<MyLeagueDto>(
            sql, 
            cancellationToken, 
            new
            {
                request.UserId, 
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                DraftStatus = nameof(RoundStatus.Draft),
                CompletedStatus = nameof(RoundStatus.Completed)
            });
    }
}