using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Data;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Prizes;

/// <summary>
/// The SQL Server reads behind <see cref="IPrizeEvaluationInputsQuery"/>.
///
/// The two entry points now have a statement each, written out in full. They used to share one projection with the predicate
/// concatenated onto the end of it at run time - which is the one thing <c>ThePredictions.SchemaCheck</c> cannot verify, because
/// it can only describe a statement that exists as a constant. Neither predicate was ever attacker-controlled, so this is about
/// what the tooling can see rather than about injection.
/// </summary>
/// <remarks>
/// Also gone: the name abbreviation, and the conditional second trip that only read the scheme entries when a scheme existed.
/// With no scheme the join returns nothing, so reading them unconditionally gives the same answer with one less decision.
/// </remarks>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class PrizeEvaluationInputsQuery(IApplicationReadDbConnection dbConnection) : IPrizeEvaluationInputsQuery
{
    public async Task<PrizeEvaluationInputsData?> GetByLeagueIdAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                l.[AdministratorUserId],
                u.[FirstName] AS [AdministratorFirstName],
                u.[LastName] AS [AdministratorLastName],
                l.[EntryCode],
                l.[Price] AS [EntryCost],
                l.[PrizeFundOverride],
                l.[EntryDeadlineUtc],
                s.[Name] AS [SeasonName],
                s.[StartDateUtc] AS [SeasonStartDateUtc],
                s.[EndDateUtc] AS [SeasonEndDateUtc],
                s.[NumberOfRounds],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS [EntrantCount]
            FROM
                [Leagues] l
            INNER JOIN
                [Seasons] s ON s.[Id] = l.[SeasonId]
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = l.[AdministratorUserId]
            WHERE
                l.[Id] = @LeagueId;";

        var league = await dbConnection.QuerySingleOrDefaultAsync<PrizeLeagueRow>(
            sql, cancellationToken,
            new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        return await WithSchemeAsync(league, cancellationToken);
    }

    public async Task<PrizeEvaluationInputsData?> GetByEntryCodeAsync(string entryCode, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id] AS [LeagueId],
                l.[Name] AS [LeagueName],
                l.[AdministratorUserId],
                u.[FirstName] AS [AdministratorFirstName],
                u.[LastName] AS [AdministratorLastName],
                l.[EntryCode],
                l.[Price] AS [EntryCost],
                l.[PrizeFundOverride],
                l.[EntryDeadlineUtc],
                s.[Name] AS [SeasonName],
                s.[StartDateUtc] AS [SeasonStartDateUtc],
                s.[EndDateUtc] AS [SeasonEndDateUtc],
                s.[NumberOfRounds],
                (
                    SELECT
                        COUNT(*)
                    FROM
                        [LeagueMembers] lm
                    WHERE
                        lm.[LeagueId] = l.[Id]
                        AND lm.[Status] = @ApprovedStatus
                ) AS [EntrantCount]
            FROM
                [Leagues] l
            INNER JOIN
                [Seasons] s ON s.[Id] = l.[SeasonId]
            INNER JOIN
                [AspNetUsers] u ON u.[Id] = l.[AdministratorUserId]
            WHERE
                l.[EntryCode] = @EntryCode;";

        var league = await dbConnection.QuerySingleOrDefaultAsync<PrizeLeagueRow>(
            sql, cancellationToken,
            new { EntryCode = entryCode, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        return await WithSchemeAsync(league, cancellationToken);
    }

    private async Task<PrizeEvaluationInputsData?> WithSchemeAsync(PrizeLeagueRow? league, CancellationToken cancellationToken)
    {
        if (league is null)
            return null;

        var schemes = await GetSchemesAsync(league.LeagueId, cancellationToken);
        var entries = await GetSchemeEntriesAsync(league.LeagueId, cancellationToken);

        return new PrizeEvaluationInputsData(league, schemes, entries);
    }

    private async Task<IReadOnlyList<PrizeSchemeRow>> GetSchemesAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lps.[Id]
            FROM
                [LeaguePrizeScheme] lps
            WHERE
                lps.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<PrizeSchemeRow>(sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }

    private async Task<IReadOnlyList<PrizeSchemeEntryRow>> GetSchemeEntriesAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lpse.[Category],
                lpse.[PerEntryPounds],
                lpse.[RankTableJson]
            FROM
                [LeaguePrizeSchemeEntries] lpse
            INNER JOIN
                [LeaguePrizeScheme] lps ON lps.[Id] = lpse.[LeaguePrizeSchemeId]
            WHERE
                lps.[LeagueId] = @LeagueId;";

        return (await dbConnection.QueryAsync<PrizeSchemeEntryRow>(sql, cancellationToken, new { LeagueId = leagueId })).ToList();
    }
}
