using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Admin.Seasons.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Admin.Seasons;

/// <summary>
/// The SQL Server reads behind <see cref="ISeasonsQuery"/>.
///
/// Three reads where there were two copies of the same twenty-column statement. What is gone: four correlated counts that
/// each hardcoded a round status as a text literal, a nested <c>UNION</c> inside a correlated <c>COUNT</c> that worked out
/// how many teams are in a season, and the <c>ORDER BY</c>.
/// </summary>
/// <remarks>
/// The pass-holder count stays here. It is a count of rows in a scoped set with no classification in it, which is
/// fetching rather than a rule - unlike the round counts, which each had to know what a status means.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class SeasonsQuery(IApplicationReadDbConnection dbConnection) : ISeasonsQuery
{
    public async Task<SeasonsData> ExecuteAsync(CancellationToken cancellationToken)
    {
        var seasons = await GetSeasonsAsync(cancellationToken);

        if (seasons.Count == 0)
            return new SeasonsData(seasons, [], []);

        var rounds = await GetRoundsAsync(cancellationToken);
        var fixtures = await GetFixturesAsync(cancellationToken);

        return new SeasonsData(seasons, rounds, fixtures);
    }

    private async Task<IReadOnlyList<AdminSeasonRow>> GetSeasonsAsync(CancellationToken cancellationToken)
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
                c.[Name] AS [CompetitionName],
                c.[Type] AS [CompetitionType],
                c.[ApiLeagueId],
                s.[PassStandardPrice],
                s.[PassPremiumPrice],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [SeasonPasses] sp
                    WHERE
                        sp.[SeasonId] = s.[Id]
                ) AS [PassHolderCount]
            FROM
                [Seasons] s
            INNER JOIN
                [Competitions] c ON c.[Id] = s.[CompetitionId];";

        return (await dbConnection.QueryAsync<AdminSeasonRow>(sql, cancellationToken)).ToList();
    }

    private async Task<IReadOnlyList<SeasonRoundStatusRow>> GetRoundsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[SeasonId],
                r.[RoundNumber],
                r.[Status]
            FROM
                [Rounds] r;";

        return (await dbConnection.QueryAsync<SeasonRoundStatusRow>(sql, cancellationToken)).ToList();
    }

    private async Task<IReadOnlyList<SeasonFixtureTeamsRow>> GetFixturesAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                r.[SeasonId],
                r.[RoundNumber],
                m.[HomeTeamId],
                m.[AwayTeamId]
            FROM
                [Matches] m
            INNER JOIN
                [Rounds] r ON r.[Id] = m.[RoundId];";

        return (await dbConnection.QueryAsync<SeasonFixtureTeamsRow>(sql, cancellationToken)).ToList();
    }
}
