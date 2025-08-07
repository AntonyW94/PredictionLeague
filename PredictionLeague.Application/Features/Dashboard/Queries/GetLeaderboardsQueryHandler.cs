using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class GetLeaderboardsQueryHandler : IRequestHandler<GetLeaderboardsQuery, IEnumerable<LeagueLeaderboardDto>>
{
    private readonly IApplicationReadDbConnection _connection;

    public GetLeaderboardsQueryHandler(IApplicationReadDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<IEnumerable<LeagueLeaderboardDto>> Handle(GetLeaderboardsQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            WITH AllLeagueRanks AS (
                SELECT
                    l.[Id] AS [LeagueId],
                    l.[Name] AS [LeagueName],
                    s.[Name] AS [SeasonName],
                    u.[Id] AS [UserId],
                    u.[FirstName] + ' ' + u.[LastName] AS [PlayerName],
                    SUM(ISNULL(up.[PointsAwarded], 0)) AS [TotalPoints],
                    RANK() OVER (PARTITION BY l.[Id] ORDER BY SUM(ISNULL(up.[PointsAwarded], 0)) DESC) AS [Rank]
                FROM 
                    [AspNetUsers] u
                JOIN 
                    [LeagueMembers] lm ON u.[Id] = lm.[UserId]
                JOIN 
                    [Leagues] l ON lm.[LeagueId] = l.[Id]
                JOIN 
                    [Seasons] s ON l.[SeasonId] = s.[Id]
                LEFT JOIN 
                    [Rounds] r ON s.[Id] = r.[SeasonId]
                LEFT JOIN 
                    [Matches] m ON r.[Id] = m.[RoundId]
                LEFT JOIN 
                    [UserPredictions] up ON m.[Id] = up.[MatchId] AND u.[Id] = up.[UserId]
                WHERE 
                    lm.[Status] = @ApprovedStatus
                GROUP BY 
                    l.[Id], l.[Name], s.[Name], u.[Id], u.[FirstName], u.[LastName]
            )
            SELECT
                alr.[LeagueId],
                alr.[LeagueName],
                alr.[SeasonName],
                alr.[Rank],
                alr.[PlayerName],
                alr.[TotalPoints]
            FROM 
                AllLeagueRanks alr
            WHERE 
                alr.[LeagueId] IN (
                    SELECT [LeagueId] FROM [LeagueMembers] WHERE [UserId] = @UserId AND [Status] = @ApprovedStatus
                )
            ORDER BY 
                alr.[LeagueName], alr.[Rank];";
        var flatResults = await _connection.QueryAsync<FlatLeaderboardEntry>(
            sql,
            cancellationToken,
            new { request.UserId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) }
        );

        var result = flatResults
            .GroupBy(x => new { x.LeagueId, x.LeagueName, x.SeasonName })
            .Select(g => new LeagueLeaderboardDto
            {
                LeagueId = g.Key.LeagueId,
                LeagueName = g.Key.LeagueName,
                SeasonName = g.Key.SeasonName,
                Entries = g.Select(entry => new LeaderboardEntryDto
                {
                    Rank = entry.Rank,
                    PlayerName = entry.PlayerName,
                    TotalPoints = entry.TotalPoints
                }).ToList()
            });

        return result;
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    [SuppressMessage("ReSharper", "AutoPropertyCanBeMadeGetOnly.Local")]
    private record FlatLeaderboardEntry
    {
        public int LeagueId { get; init; }
        public string LeagueName { get; init; } = null!;
        public string SeasonName { get; init; } = null!;
        public long Rank { get; init; }
        public string PlayerName { get; init; } = null!;
        public int TotalPoints { get; init; }
    }
}