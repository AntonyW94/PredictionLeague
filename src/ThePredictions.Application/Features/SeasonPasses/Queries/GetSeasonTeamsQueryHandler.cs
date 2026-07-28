using System.Diagnostics.CodeAnalysis;
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

        var teams = await dbConnection.QueryAsync<SeasonTeamQueryResult>(sql, cancellationToken, new { request.SeasonId });

        return teams.Select(t => new SeasonTeamDto(
            t.Name,
            t.LogoUrl));
    }

    // NOTE: Dapper matches a record's constructor to the result columns POSITIONALLY -
    // parameter N must line up with SELECT column N (by name and type). Keep the order of
    // these parameters identical to the SELECT column order above, or materialisation throws
    // at runtime ("A parameterless default constructor or one matching signature ... is required").
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record SeasonTeamQueryResult(
        string Name,
        string? LogoUrl);
}
