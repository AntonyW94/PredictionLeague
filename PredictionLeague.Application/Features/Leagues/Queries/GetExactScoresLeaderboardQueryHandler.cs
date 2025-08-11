using MediatR;
using PredictionLeague.Application.Data;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Domain.Common.Enumerations;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class GetExactScoresLeaderboardQueryHandler : IRequestHandler<GetExactScoresLeaderboardQuery, ExactScoresLeaderboardDto>
{
    private readonly IApplicationReadDbConnection _connection;

    public GetExactScoresLeaderboardQueryHandler(IApplicationReadDbConnection connection)
    {
        _connection = connection;
    }

    public async Task<ExactScoresLeaderboardDto> Handle(GetExactScoresLeaderboardQuery request, CancellationToken cancellationToken)
    {
        const string entriesSql = @"
                                    SELECT
                                        u.[FirstName] + ' ' + u.[LastName] AS PlayerName,
                                        COUNT(CASE WHEN up.[PointsAwarded] = 5 THEN 1 END) AS ExactScoresCount
                                    FROM
                                        [LeagueMembers] lm
                                    JOIN 
                                        [AspNetUsers] u ON lm.[UserId] = u.[Id]
                                    JOIN 
                                        [Leagues] AS l ON lm.[LeagueId] = l.[Id]
                                    JOIN 
                                        [Seasons] AS s ON l.[SeasonId] = s.[Id]
                                    JOIN 
                                        [Rounds] AS r ON s.[Id] = r.[SeasonId]
                                    JOIN 
                                        [Matches] AS m ON r.[Id] = m.[RoundId]
                                    LEFT JOIN 
                                        [UserPredictions] AS up ON m.[Id] = up.[MatchId] AND lm.[UserId] = up.[UserId]
                                    WHERE
                                        lm.[LeagueId] = @LeagueId 
                                        AND lm.[Status] = @ApprovedStatus
                                    GROUP BY 
                                        u.[FirstName],
                                        u.[LastName]
                                  ORDER BY
                                        ExactScoresCount DESC, 
                                        PlayerName";

        var leaderboardEntries = await _connection.QueryAsync<ExactScoresLeaderboardEntryDto>(entriesSql, cancellationToken, new { request.LeagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        const string leagueInfoSql = @"
                                        SELECT
                                            l.[Name] AS LeagueName,
                                            s.[Name] AS SeasonName
                                        FROM 
                                            [Leagues] l
                                        JOIN
                                            [Seasons] s ON l.[SeasonId] = s.[Id]
                                        WHERE 
                                            l.[Id] = @LeagueId";

        var leagueInfo = await _connection.QuerySingleOrDefaultAsync<(string LeagueName, string SeasonName)>(leagueInfoSql, cancellationToken, new { request.LeagueId });

        var leaderboard = new ExactScoresLeaderboardDto
        {
            LeagueName = leagueInfo.LeagueName,
            SeasonName = leagueInfo.SeasonName,
            Entries = leaderboardEntries.ToList()
        };

        var rank = 1;
        for (var i = 0; i < leaderboard.Entries.Count; i++)
        {
            if (i > 0 && leaderboard.Entries[i].ExactScoresCount < leaderboard.Entries[i - 1].ExactScoresCount)
            {
                rank = i + 1;
            }
            leaderboard.Entries[i].Rank = rank;
        }

        return leaderboard;
    }
}
