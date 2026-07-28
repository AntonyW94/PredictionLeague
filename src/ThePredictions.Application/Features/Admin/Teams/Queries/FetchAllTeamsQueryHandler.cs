using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Contracts.Admin.Teams;

namespace ThePredictions.Application.Features.Admin.Teams.Queries;

public class FetchAllTeamsQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<FetchAllTeamsQuery, IEnumerable<TeamDto>>
{
    public async Task<IEnumerable<TeamDto>> Handle(FetchAllTeamsQuery request, CancellationToken cancellationToken)
    {
        if (request.SeasonId.HasValue)
        {
            const string sql = @"
                SELECT DISTINCT
                    t.[Id],
                    t.[Name],
                    t.[ShortName],
                    t.[LogoUrl],
                    t.[Abbreviation],
                    t.[ApiTeamId]
                FROM
                    [Teams] t
                INNER JOIN
                    [Matches] m ON t.[Id] = m.[HomeTeamId] OR t.[Id] = m.[AwayTeamId]
                INNER JOIN
                    [Rounds] r ON m.[RoundId] = r.[Id]
                WHERE
                    r.[SeasonId] = @SeasonId
                ORDER BY
                    t.[Name] ASC";

            var seasonTeams = await dbConnection.QueryAsync<TeamQueryResult>(sql, cancellationToken, new { request.SeasonId });

            return seasonTeams.Select(ToDto);
        }

        const string allTeamsSql = @"
            SELECT
                [Id],
                [Name],
                [ShortName],
                [LogoUrl],
                [Abbreviation],
                [ApiTeamId]
            FROM [Teams]
            ORDER BY [Name] ASC";

        var allTeams = await dbConnection.QueryAsync<TeamQueryResult>(allTeamsSql, cancellationToken);

        return allTeams.Select(ToDto);
    }

    private static TeamDto ToDto(TeamQueryResult team)
    {
        return new TeamDto(
            team.Id,
            team.Name,
            team.ShortName,
            team.LogoUrl,
            team.Abbreviation,
            team.ApiTeamId);
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record TeamQueryResult(
        int Id,
        string Name,
        string ShortName,
        string LogoUrl,
        string Abbreviation,
        int? ApiTeamId);
}
