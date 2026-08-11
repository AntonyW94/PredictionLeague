using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Teams.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Teams;

/// <summary>
/// The SQL Server read behind <see cref="ISeasonTeamsQuery"/>.
///
/// One statement where there were two: an <c>INNER JOIN ... SELECT DISTINCT</c> on the administrator's screen and an
/// <c>EXISTS</c> on the season-pass page, asking the same question two ways. The <c>EXISTS</c> form is the one kept,
/// because it cannot produce a duplicate in the first place - so the <c>DISTINCT</c> the other needed was a repair for
/// its own join rather than a rule about teams.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class SeasonTeamsQuery(IApplicationReadDbConnection dbConnection) : ISeasonTeamsQuery
{
    public async Task<IReadOnlyList<TeamRow>> ExecuteAsync(int seasonId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                t.[Id],
                t.[Name],
                t.[ShortName],
                t.[LogoUrl],
                t.[Abbreviation],
                t.[ApiTeamId]
            FROM
                [Teams] t
            WHERE
                EXISTS (
                    SELECT
                        1
                    FROM
                        [Matches] m
                    INNER JOIN
                        [Rounds] r ON r.[Id] = m.[RoundId]
                    WHERE
                        r.[SeasonId] = @SeasonId
                        AND (m.[HomeTeamId] = t.[Id] OR m.[AwayTeamId] = t.[Id])
                );";

        return (await dbConnection.QueryAsync<TeamRow>(sql, cancellationToken, new { SeasonId = seasonId })).ToList();
    }
}
