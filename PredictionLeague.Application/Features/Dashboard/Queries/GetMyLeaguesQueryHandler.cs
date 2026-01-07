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
        const string sql = @"
        WITH MyLeagues AS (
            SELECT 
                l.[Id] AS LeagueId,
                l.[Name] AS LeagueName,
                l.[Price],
		        l.[PrizeFundOverride],
                s.[Id] AS SeasonId,
                s.[Name] AS SeasonName,
                lm.[UserId],
                lm.[Status]
            FROM [LeagueMembers] lm
            JOIN [Leagues] l ON lm.[LeagueId] = l.[Id]
            JOIN [Seasons] s ON l.[SeasonId] = s.[Id]
            WHERE lm.[UserId] = @UserId AND lm.[Status] = @ApprovedStatus
        ),

        ActiveRounds AS (
            SELECT 
                r.[SeasonId],
                r.[Id] AS RoundId,
                r.[RoundNumber],
                r.[StartDate],
                r.[Status],
                (SELECT COUNT(*) FROM [Matches] WHERE [RoundId] = r.[Id] AND [Status] = @InProgressStatus) AS InProgressCount,
                (SELECT COUNT(*) FROM [Matches] WHERE [RoundId] = r.[Id] AND [Status] = @CompletedStatus) AS CompletedCount,
                ROW_NUMBER() OVER (
                    PARTITION BY r.[SeasonId] 
                        ORDER BY 
                           CASE 
                            WHEN r.[Status] = @InProgressStatus THEN 0 
                            WHEN r.[Status] = @CompletedStatus AND r.[CompletedDate] > DATEADD(HOUR, -48, GETUTCDATE()) THEN 1
                            WHEN r.[Status] = @PublishedStatus THEN 2
                            ELSE 3 
                        END ASC,
                        r.[StartDate] ASC
                ) as [PriorityRank]
            FROM [Rounds] r
            WHERE 
                r.[Status] <> @DraftStatus
                AND r.[SeasonId] IN (SELECT DISTINCT [SeasonId] FROM [MyLeagues])
        ),
        
        LeagueContext AS (
            SELECT 
                l.[Id] AS LeagueId,
                (SELECT COUNT(*) FROM [LeagueMembers] WHERE [LeagueId] = l.[Id] AND [Status] = @ApprovedStatus) AS MemberCount,
                (SELECT ISNULL(SUM([Amount]), 0) FROM [Winnings] w JOIN [LeaguePrizeSettings] s ON w.[LeaguePrizeSettingId] = s.[Id] WHERE s.[LeagueId] = l.[Id]) AS TotalPaidOut,
                (SELECT ISNULL(SUM([Amount]), 0) FROM [Winnings] w JOIN [LeaguePrizeSettings] s ON w.[LeaguePrizeSettingId] = s.[Id] WHERE s.[LeagueId] = l.[Id] AND [UserId] = @UserId) AS UserWinnings
            FROM [Leagues] l 
            WHERE l.[Id] IN (SELECT [LeagueId] FROM [MyLeagues])
        )
       
        SELECT 
            l.[LeagueId] AS Id,
            l.[LeagueName] AS Name,
            l.[SeasonName],
            
            CASE WHEN ar.[RoundId] IS NOT NULL THEN 'Round ' + CAST(ar.[RoundNumber] AS VARCHAR(10)) ELSE NULL END AS CurrentRound,
            CASE WHEN ar.[RoundId] IS NOT NULL THEN DATENAME(MONTH, ar.[StartDate]) ELSE NULL END AS CurrentMonth,
            ISNULL(lc.[MemberCount], 0) AS MemberCount,

            stats.[OverallRank] AS Rank,
            stats.[MonthRank] AS MonthRank,
            CASE 
                WHEN ar.[Status] = @PublishedStatus THEN 1                    
                ELSE stats.[LiveRoundRank]
            END AS RoundRank,

            stats.[SnapshotOverallRank] AS PreRoundOverallRank,
            stats.[SnapshotMonthRank] AS PreRoundMonthRank,
            CASE 
                WHEN ar.[Status] = @PublishedStatus THEN 1                    
                ELSE stats.[StableRoundRank]
            END AS StableRoundRank,

            ar.[Status] AS RoundStatus,
            ISNULL(ar.[InProgressCount], 0) AS InProgressCount,
            ISNULL(ar.[CompletedCount], 0) AS CompletedCount,

            lc.[UserWinnings] AS PrizeMoneyWon,
            (COALESCE(l.[PrizeFundOverride], l.[Price] * lc.[MemberCount]) - lc.[TotalPaidOut]) AS PrizeMoneyRemaining,
            COALESCE(l.[PrizeFundOverride], l.[Price] * lc.[MemberCount]) AS TotalPrizeFund,
            l.[Price] AS EntryFee 

        FROM [MyLeagues] l
        LEFT JOIN [LeagueMemberStats] stats ON l.[LeagueId] = stats.[LeagueId] AND l.[UserId] = stats.[UserId]  
        LEFT JOIN [ActiveRounds] ar ON l.[SeasonId] = ar.[SeasonId] AND ar.[PriorityRank] = 1
        LEFT JOIN [LeagueContext] lc ON l.[LeagueId] = lc.[LeagueId]

        ORDER BY 
            CASE WHEN ar.[Status] = @InProgressStatus THEN 0 ELSE 1 END ASC,
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
                PublishedStatus = nameof(RoundStatus.Published),
                InProgressStatus = nameof(RoundStatus.InProgress),
                CompletedStatus = nameof(RoundStatus.Completed)
            });
    }
}