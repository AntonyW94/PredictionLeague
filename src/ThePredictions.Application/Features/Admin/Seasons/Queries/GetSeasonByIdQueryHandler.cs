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