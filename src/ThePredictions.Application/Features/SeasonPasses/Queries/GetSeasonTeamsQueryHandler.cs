using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

public class GetSeasonTeamsQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetSeasonTeamsQuery, IEnumerable<SeasonTeamDto>>
{
    public async Task<IEnumerable<SeasonTeamDto>> Handle(GetSeasonTeamsQuery request, CancellationToken cancellationToken)
    {
        // Distinct teams that appear in the season's matches (knockout placeholders have no team yet).
        const string sql = @"
            SELECT DISTINCT
                t.[Name],
                t.[LogoUrl]
            FROM
                [Teams] t
            WHERE EXISTS (
                SELECT 1
                FROM [Matches] m
                INNER JOIN [Rounds] r ON r.[Id] = m.[RoundId]
                WHERE r.[SeasonId] = @SeasonId
                    AND (m.[HomeTeamId] = t.[Id] OR m.[AwayTeamId] = t.[Id])
            )
            ORDER BY
                t.[Name];";

        return await dbConnection.QueryAsync<SeasonTeamDto>(sql, cancellationToken, new { request.SeasonId });
    }
}
