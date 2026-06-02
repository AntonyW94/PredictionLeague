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
    public async Task<PrizeEvaluationInputs?> LoadAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string leagueSql = @"
            SELECT
                l.[Name] AS LeagueName,
                l.[AdministratorUserId],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS AdministratorName,
                l.[EntryCode],
                l.[Price] AS EntryCost,
                l.[EntryDeadlineUtc],
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
                l.[Id] = @LeagueId;";

        var row = await dbConnection.QuerySingleOrDefaultAsync<LeagueRow>(leagueSql, cancellationToken, new { LeagueId = leagueId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });
        if (row is null)
            return null;

        const string schemeSql = @"
            SELECT
                lps.[AdminTopUpPounds],
                lps.[OverallFivePoundThreshold]
            FROM
                [LeaguePrizeScheme] lps
            WHERE
                lps.[LeagueId] = @LeagueId;";

        var scheme = await dbConnection.QuerySingleOrDefaultAsync<SchemeRow>(schemeSql, cancellationToken, new { LeagueId = leagueId });

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

            var entryRows = await dbConnection.QueryAsync<EntryRow>(entriesSql, cancellationToken, new { LeagueId = leagueId });
            categories = entryRows
                .Select(e => new PrizeSchemeCategoryInput { Category = e.Category, PerEntryPounds = e.PerEntryPounds, RankTableJson = e.RankTableJson })
                .ToList();
        }

        return new PrizeEvaluationInputs
        {
            LeagueName = row.LeagueName,
            AdministratorName = row.AdministratorName,
            AdministratorUserId = row.AdministratorUserId,
            EntryCode = row.EntryCode,
            EntryCost = row.EntryCost,
            EntrantCount = row.EntrantCount,
            EntryDeadlineUtc = row.EntryDeadlineUtc,
            NumberOfRounds = row.NumberOfRounds,
            NumberOfMonths = CountMonths(row.SeasonStartDateUtc, row.SeasonEndDateUtc),
            HasScheme = scheme is not null,
            AdminTopUpPounds = scheme?.AdminTopUpPounds ?? 0,
            OverallFivePoundThreshold = scheme?.OverallFivePoundThreshold ?? 0,
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

    [ExcludeFromCodeCoverage]
    private sealed class LeagueRow
    {
        public string LeagueName { get; init; } = string.Empty;
        public string AdministratorUserId { get; init; } = string.Empty;
        public string AdministratorName { get; init; } = string.Empty;
        public string? EntryCode { get; init; }
        public decimal EntryCost { get; init; }
        public DateTime EntryDeadlineUtc { get; init; }
        public DateTime SeasonStartDateUtc { get; init; }
        public DateTime SeasonEndDateUtc { get; init; }
        public int NumberOfRounds { get; init; }
        public int EntrantCount { get; init; }
    }

    [ExcludeFromCodeCoverage]
    private sealed class SchemeRow
    {
        public int AdminTopUpPounds { get; init; }
        public int OverallFivePoundThreshold { get; init; }
    }

    [ExcludeFromCodeCoverage]
    private sealed class EntryRow
    {
        public PrizeType Category { get; init; }
        public int PerEntryPounds { get; init; }
        public string? RankTableJson { get; init; }
    }
}
