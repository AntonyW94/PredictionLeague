using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Seasons;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

public class GetSeasonByIdQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetSeasonByIdQuery, SeasonDto?>
{
    public async Task<SeasonDto?> Handle(GetSeasonByIdQuery request, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                s.[Id],
                s.[Name],
                s.[StartDateUtc],
                s.[EndDateUtc],
                s.[IsActive],
                s.[NumberOfRounds],
                s.[CompetitionId],
                c.[Name] AS CompetitionName,
                c.[Type] AS CompetitionType,
                c.[ApiLeagueId],
                (SELECT COUNT(*) FROM [Rounds] r WHERE r.[SeasonId] = s.[Id]) AS RoundCount,
                (SELECT COUNT(*) FROM [Rounds] r WHERE r.[SeasonId] = s.[Id] AND r.[Status] = 'Draft') AS DraftCount,
                (SELECT COUNT(*) FROM [Rounds] r WHERE r.[SeasonId] = s.[Id] AND r.[Status] = 'Published') AS PublishedCount,
                (SELECT COUNT(*) FROM [Rounds] r WHERE r.[SeasonId] = s.[Id] AND r.[Status] = 'InProgress') AS InProgressCount,
                (SELECT COUNT(*) FROM [Rounds] r WHERE r.[SeasonId] = s.[Id] AND r.[Status] = 'Completed') AS CompletedCount,
                (
                    SELECT COUNT(*)
                    FROM (
                        SELECT m.[HomeTeamId] AS TeamId
                        FROM [Matches] m
                        JOIN [Rounds] r ON m.[RoundId] = r.[Id]
                        WHERE r.[SeasonId] = s.[Id]
                            AND r.[RoundNumber] = (SELECT MIN(r2.[RoundNumber]) FROM [Rounds] r2 WHERE r2.[SeasonId] = s.[Id])
                            AND m.[HomeTeamId] IS NOT NULL
                        UNION
                        SELECT m.[AwayTeamId]
                        FROM [Matches] m
                        JOIN [Rounds] r ON m.[RoundId] = r.[Id]
                        WHERE r.[SeasonId] = s.[Id]
                            AND r.[RoundNumber] = (SELECT MIN(r2.[RoundNumber]) FROM [Rounds] r2 WHERE r2.[SeasonId] = s.[Id])
                            AND m.[AwayTeamId] IS NOT NULL
                    ) firstRoundTeams
                ) AS TeamCount,
                s.[PassStandardPrice],
                s.[PassPremiumPrice]
            FROM
                [Seasons] s
            JOIN
                [Competitions] c ON s.[CompetitionId] = c.[Id]
            WHERE
                s.[Id] = @Id";

        return await dbConnection.QuerySingleOrDefaultAsync<SeasonDto>(sql, cancellationToken, new { request.Id });
    }
}