using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Rounds.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Rounds;

/// <summary>
/// The SQL Server read behind <see cref="IRoundMatchesQuery"/>.
///
/// One statement where there were two. What is gone: a status whitelist that named every status except the one it
/// meant, and an <c>ORDER BY</c> that only one of the two copies had.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class RoundMatchesQuery(IApplicationReadDbConnection dbConnection) : IRoundMatchesQuery
{
    public async Task<IReadOnlyList<RoundMatchRow>> ExecuteAsync(int roundId, CancellationToken cancellationToken)
    {
        // The team joins are left joins because a tournament fixture can be scheduled before its teams are known, in
        // which case it carries a placeholder name instead. That is why every team column is nullable.
        const string sql = @"
            SELECT
                m.[Id],
                m.[MatchDateTimeUtc],
                m.[MatchNumber],
                m.[HomeTeamId],
                ht.[Name] AS [HomeTeamName],
                ht.[ShortName] AS [HomeTeamShortName],
                ht.[Abbreviation] AS [HomeTeamAbbreviation],
                ht.[LogoUrl] AS [HomeTeamLogoUrl],
                m.[AwayTeamId],
                at.[Name] AS [AwayTeamName],
                at.[ShortName] AS [AwayTeamShortName],
                at.[Abbreviation] AS [AwayTeamAbbreviation],
                at.[LogoUrl] AS [AwayTeamLogoUrl],
                m.[ActualHomeTeamScore],
                m.[ActualAwayTeamScore],
                m.[Status],
                m.[PlaceholderHomeName],
                m.[PlaceholderAwayName],
                m.[CustomLockTimeUtc]
            FROM
                [Matches] m
            LEFT JOIN
                [Teams] ht ON ht.[Id] = m.[HomeTeamId]
            LEFT JOIN
                [Teams] at ON at.[Id] = m.[AwayTeamId]
            WHERE
                m.[RoundId] = @RoundId;";

        return (await dbConnection.QueryAsync<RoundMatchRow>(sql, cancellationToken, new { RoundId = roundId })).ToList();
    }
}
