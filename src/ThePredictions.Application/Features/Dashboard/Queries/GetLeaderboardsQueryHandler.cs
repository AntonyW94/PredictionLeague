using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Dashboard.Queries;

public class GetLeaderboardsQueryHandler(IApplicationReadDbConnection connection)
    : IRequestHandler<GetLeaderboardsQuery, IEnumerable<LeagueLeaderboardDto>>
{
    public async Task<IEnumerable<LeagueLeaderboardDto>> Handle(GetLeaderboardsQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH AllLeagueRanks AS (
                SELECT
                    l.[Id] AS [LeagueId],
                    l.[Name] AS [LeagueName],
                    l.[Price] AS [LeaguePrice],
                    s.[Name] AS [SeasonName],
                    s.[StartDateUtc] AS [SeasonStartDateUtc],
                    CAST(CASE
                        WHEN (SELECT COUNT(*) FROM [Rounds] r2 WHERE r2.[SeasonId] = l.[SeasonId] AND r2.[Status] = @CompletedStatus) >= s.[NumberOfRounds]
                        THEN 1
                        ELSE 0
                    END AS bit) AS [IsFinished],
                    u.[Id] AS [UserId],
                    u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS [PlayerName],
                    SUM(ISNULL(lrr.[BoostedPoints], 0)) AS [TotalPoints],
                    RANK() OVER (PARTITION BY l.[Id] ORDER BY SUM(ISNULL(lrr.[BoostedPoints], 0)) DESC) AS [Rank],
                    CASE WHEN EXISTS (
                        SELECT 1
                        FROM [Rounds] r
                        WHERE r.[SeasonId] = l.[SeasonId] AND r.[Status] = @CompletedStatus
                    ) THEN stats.[SnapshotOverallRank] ELSE NULL END AS [SnapshotRank],
                    ar.[IsInProgress] AS [IsRoundInProgress]
                FROM
                    [LeagueMembers] lm
                JOIN
                    [AspNetUsers] u ON lm.[UserId] = u.[Id]
	            JOIN
                    [Leagues] l ON lm.[LeagueId] = l.[Id]
                JOIN
                    [Seasons] s ON l.[SeasonId] = s.[Id]
                CROSS APPLY (
                    SELECT CASE WHEN EXISTS (
                        SELECT 1
                        FROM [Rounds] r
                        WHERE r.[SeasonId] = l.[SeasonId] AND r.[Status] = @InProgressStatus
                    ) THEN 1 ELSE 0 END AS IsInProgress
                ) ar
                LEFT JOIN
                    [LeagueRoundResults] lrr ON lm.[UserId] = lrr.[UserId] AND lrr.[LeagueId] = l.[Id]
                LEFT JOIN
                    [LeagueMemberStats] stats ON lm.[LeagueId] = stats.[LeagueId] AND lm.[UserId] = stats.[UserId]
                WHERE
                    lm.[Status] = @ApprovedStatus
                GROUP BY
                    l.[Id],
                    l.[Name],
                    l.[Price],
                    s.[Name],
                    s.[StartDateUtc],
                    s.[NumberOfRounds],
                    l.[SeasonId],
                    u.[Id],
                    u.[FirstName],
                    u.[LastName],
                    stats.[SnapshotOverallRank],
                    ar.[IsInProgress]
            )
            SELECT
                alr.[LeagueId],
                alr.[LeagueName],
                alr.[LeaguePrice],
                alr.[SeasonName],
                alr.[SeasonStartDateUtc],
                alr.[IsFinished],
                alr.[Rank],
                alr.[PlayerName],
                alr.[TotalPoints],
                alr.[UserId],
                alr.[SnapshotRank],
                alr.[IsRoundInProgress],
                mylm.[IsArchivedByUser]
            FROM
                [AllLeagueRanks] alr
            JOIN
                [LeagueMembers] mylm ON mylm.[LeagueId] = alr.[LeagueId] AND mylm.[UserId] = @UserId AND mylm.[Status] = @ApprovedStatus
            ORDER BY
                CASE WHEN alr.[IsRoundInProgress] = 1 THEN 0 ELSE 1 END ASC,
                alr.[SeasonStartDateUtc] ASC,
                alr.[LeaguePrice] DESC,
                alr.[LeagueName],
                alr.[Rank],
                alr.[PlayerName];";

        var flatResults = await connection.QueryAsync<FlatLeaderboardEntry>(
            sql,
            cancellationToken,
            new
            {
                request.UserId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                InProgressStatus = nameof(RoundStatus.InProgress),
                CompletedStatus = nameof(RoundStatus.Completed)
            }
        );

        var result = flatResults
            .GroupBy(x => new { x.LeagueId, x.LeagueName, x.LeaguePrice, x.SeasonName, x.SeasonStartDateUtc, x.IsFinished, x.IsRoundInProgress, x.IsArchivedByUser })
            .Select(g => new
            {
                g.Key.LeaguePrice,
                g.Key.SeasonStartDateUtc,
                g.Key.IsRoundInProgress,
                Dto = new LeagueLeaderboardDto
                {
                    LeagueId = g.Key.LeagueId,
                    LeagueName = g.Key.LeagueName,
                    SeasonName = g.Key.SeasonName,
                    IsFinished = g.Key.IsFinished,
                    IsArchivedByUser = g.Key.IsArchivedByUser,
                    Entries = g.Select(entry => new LeaderboardEntryDto
                    {
                        Rank = entry.Rank,
                        PlayerName = entry.PlayerName,
                        TotalPoints = entry.TotalPoints,
                        UserId = entry.UserId,
                        SnapshotRank = entry.SnapshotRank,
                        IsRoundInProgress = entry.IsRoundInProgress == 1
                    }).ToList()
                }
            })
            .OrderBy(x => x.IsRoundInProgress == 1 ? 0 : 1)
            .ThenBy(x => x.SeasonStartDateUtc)
            .ThenByDescending(x => x.LeaguePrice)
            .ThenBy(x => x.Dto.LeagueName)
            .Select(x => x.Dto);

        return result;
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local")]
    private record FlatLeaderboardEntry
    {
        public int LeagueId { get; init; }
        public string LeagueName { get; init; } = null!;
        public decimal LeaguePrice { get; init; }
        public string SeasonName { get; init; } = null!;
        public DateTime SeasonStartDateUtc { get; init; }
        public bool IsFinished { get; init; }
        public long Rank { get; init; }
        public string PlayerName { get; init; } = null!;
        public int TotalPoints { get; init; }
        public string UserId { get; init; } = null!;
        public long? SnapshotRank { get; init; }
        public int IsRoundInProgress { get; init; }
        public bool IsArchivedByUser { get; init; }
    }
}