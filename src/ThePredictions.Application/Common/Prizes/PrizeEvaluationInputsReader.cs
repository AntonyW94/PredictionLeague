using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Common.Prizes;

/// <summary>
/// Loads the live prize-evaluation inputs for a league from the read side (CQRS query path).
/// Reads the pot context, season event counts, headline facts and the scheme entries.
/// </summary>
public class PrizeEvaluationInputsReader(IApplicationReadDbConnection dbConnection) : IPrizeEvaluationInputsReader
{
    // Shared projection; callers append the WHERE predicate (by id or by entry code).
    private const string LeagueSelectSql = @"
            SELECT
                l.[Id] AS LeagueId,
                l.[Name] AS LeagueName,
                l.[AdministratorUserId],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS AdministratorName,
                l.[EntryCode],
                l.[Price] AS EntryCost,
                l.[PrizeFundOverride],
                l.[EntryDeadlineUtc],
                s.[Name] AS SeasonName,
                s.[StartDateUtc] AS SeasonStartDateUtc,
                s.[EndDateUtc] AS SeasonEndDateUtc,
                s.[NumberOfRounds],
                (SELECT COUNT(*) FROM [LeagueMembers] lm WHERE lm.[LeagueId] = l.[Id] AND lm.[Status] = @ApprovedStatus) AS EntrantCount
            FROM
                [Leagues] l
            JOIN
                [Seasons] s ON l.[SeasonId] = s.[Id]
            JOIN
                [AspNetUsers] u ON l.[AdministratorUserId] = u.[Id]
            WHERE
                ";

    public async Task<PrizeEvaluationInputs?> LoadAsync(int leagueId, CancellationToken cancellationToken)
    {
        var sql = LeagueSelectSql + "l.[Id] = @LeagueId;";
        var row = await dbConnection.QuerySingleOrDefaultAsync<LeagueRow>(sql, cancellationToken, new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
        return await BuildAsync(row, cancellationToken);
    }

    public async Task<PrizeEvaluationInputs?> LoadByEntryCodeAsync(string entryCode, CancellationToken cancellationToken)
    {
        var sql = LeagueSelectSql + "l.[EntryCode] = @EntryCode;";
        var row = await dbConnection.QuerySingleOrDefaultAsync<LeagueRow>(sql, cancellationToken, new { EntryCode = entryCode, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
        return await BuildAsync(row, cancellationToken);
    }

    private async Task<PrizeEvaluationInputs?> BuildAsync(LeagueRow? row, CancellationToken cancellationToken)
    {
        if (row is null)
            return null;

        const string schemeSql = @"
            SELECT
                lps.[Id]
            FROM
                [LeaguePrizeScheme] lps
            WHERE
                lps.[LeagueId] = @LeagueId;";

        var scheme = await dbConnection.QuerySingleOrDefaultAsync<SchemeRow>(schemeSql, cancellationToken, new { LeagueId = row.LeagueId });

        var categories = new List<PrizeSchemeCategoryInput>();
        if (scheme is not null)
        {
            const string entriesSql = @"
                SELECT
                    lpse.[Category],
                    lpse.[PerEntryPounds],
                    lpse.[RankTableJson]
                FROM
                    [LeaguePrizeSchemeEntries] lpse
                INNER JOIN [LeaguePrizeScheme] lps ON lps.[Id] = lpse.[LeaguePrizeSchemeId]
                WHERE
                    lps.[LeagueId] = @LeagueId;";

            var entryRows = await dbConnection.QueryAsync<EntryRow>(entriesSql, cancellationToken, new { LeagueId = row.LeagueId });
            categories = entryRows
                .Select(e => new PrizeSchemeCategoryInput { Category = e.Category, PerEntryPounds = e.PerEntryPounds, RankTableJson = e.RankTableJson })
                .ToList();
        }

        return new PrizeEvaluationInputs
        {
            LeagueId = row.LeagueId,
            LeagueName = row.LeagueName,
            SeasonName = row.SeasonName,
            AdministratorName = row.AdministratorName,
            AdministratorUserId = row.AdministratorUserId,
            EntryCode = row.EntryCode,
            EntryCost = row.EntryCost,
            EntrantCount = row.EntrantCount,
            EntryDeadlineUtc = row.EntryDeadlineUtc,
            NumberOfRounds = row.NumberOfRounds,
            NumberOfMonths = CountMonths(row.SeasonStartDateUtc, row.SeasonEndDateUtc),
            HasScheme = scheme is not null,
            AdminTopUpPounds = (int)decimal.Truncate(row.PrizeFundOverride ?? 0m),
            Categories = categories
        };
    }

    private static int CountMonths(DateTime startDateUtc, DateTime endDateUtc)
    {
        var months = 0;
        for (var date = startDateUtc; date <= endDateUtc; date = date.AddMonths(1))
            months++;

        return months;
    }

    // The row types are internal so a test can supply rows to the build below; InternalsVisibleTo
    // already exposes this assembly to ThePredictions.Application.Tests.Unit.
    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal sealed class LeagueRow
    {
        public int LeagueId { get; init; }
        public string LeagueName { get; init; } = string.Empty;
        public string SeasonName { get; init; } = string.Empty;
        public string AdministratorUserId { get; init; } = string.Empty;
        public string AdministratorName { get; init; } = string.Empty;
        public string? EntryCode { get; init; }
        public decimal EntryCost { get; init; }
        public decimal? PrizeFundOverride { get; init; }
        public DateTime EntryDeadlineUtc { get; init; }
        public DateTime SeasonStartDateUtc { get; init; }
        public DateTime SeasonEndDateUtc { get; init; }
        public int NumberOfRounds { get; init; }
        public int EntrantCount { get; init; }
    }

    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal sealed class SchemeRow
    {
        public int Id { get; init; }
    }

    [ExcludeFromCodeCoverage(Justification = "Dapper row type: properties only, no logic to test.")]
    internal sealed class EntryRow
    {
        public PrizeType Category { get; init; }
        public int PerEntryPounds { get; init; }
        public string? RankTableJson { get; init; }
    }
}
