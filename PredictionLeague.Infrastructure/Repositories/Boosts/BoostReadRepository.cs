using PredictionLeague.Application.Data;
using PredictionLeague.Application.Repositories;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;
using PredictionLeague.Domain.Services.Boosts;
using System.Diagnostics.CodeAnalysis;

namespace PredictionLeague.Infrastructure.Repositories.Boosts;

public sealed class BoostReadRepository : IBoostReadRepository
{
    private readonly IApplicationReadDbConnection _dbConnection;

    public BoostReadRepository(IApplicationReadDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task<(int SeasonId, int RoundNumber)> GetRoundInfoAsync(int roundId, CancellationToken cancellationToken)
    {
        const string sql = @"
                SELECT r.[SeasonId], r.[RoundNumber]
                FROM [Rounds] r
                WHERE r.[Id] = @RoundId;";

        var rows = await _dbConnection.QueryAsync<RoundInfoRow>(
            sql,
            cancellationToken,
            new { RoundId = roundId });

        var row = rows.Single();

        return (row.SeasonId, row.RoundNumber);
    }

    public async Task<int?> GetLeagueSeasonIdAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
                SELECT l.[SeasonId]
                FROM [Leagues] l
                WHERE l.[Id] = @LeagueId;";

        var rows = (await _dbConnection.QueryAsync<int>(sql, cancellationToken, new { LeagueId = leagueId })).ToList();
        if (rows.Count == 0)
            return null;

        return rows.Single();
    }

    public async Task<IEnumerable<BoostDefinition>> GetBoostDefinitionsForLeagueAsync(int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                bd.[Id] AS BoostDefinitionId,
                bd.[Code] AS BoostCode,
                bd.[Name],
                bd.[Tooltip],
                bd.[Description],
                bd.[ImageUrl],
                bd.[SelectedImageUrl],
                bd.[DisabledImageUrl]
            FROM 
                [BoostDefinitions] bd
            INNER JOIN 
                [LeagueBoostRules] lbr ON lbr.[BoostDefinitionId] = bd.[Id]
            WHERE 
                lbr.[LeagueId] = @LeagueId
                AND lbr.[IsEnabled] = 1
            ORDER BY 
                lbr.[Id]";

        return await _dbConnection.QueryAsync<BoostDefinition>(sql, cancellationToken, new { LeagueId = leagueId });
    }

    public async Task<bool> IsUserMemberOfLeagueAsync(string userId, int leagueId, CancellationToken cancellationToken)
    {
        const string sql = @"
                SELECT 1
                FROM [LeagueMembers] lm
                WHERE lm.[LeagueId] = @LeagueId
                  AND lm.[UserId] = @UserId
                  AND lm.[Status] = @ApprovedStatus;";

        var rows = await _dbConnection.QueryAsync<int>(sql, cancellationToken, new { LeagueId = leagueId, UserId = userId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        return rows.Any();
    }

    public async Task<LeagueBoostRuleSnapshot?> GetLeagueBoostRuleAsync(int leagueId, string boostCode, CancellationToken cancellationToken)
    {
        const string ruleSql = @"
                SELECT
                    lbr.[IsEnabled],
                    lbr.[TotalUsesPerSeason],
                    lbr.[Id] AS LeagueBoostRuleId
                FROM [BoostDefinitions] bd
                INNER JOIN [LeagueBoostRules] lbr
                    ON lbr.[BoostDefinitionId] = bd.[Id]
                   AND lbr.[LeagueId] = @LeagueId
                WHERE bd.[Code] = @BoostCode;";

        var ruleRows = await _dbConnection.QueryAsync<LeagueBoostRuleRow>(ruleSql, cancellationToken, new { LeagueId = leagueId, BoostCode = boostCode });

        var ruleRow = ruleRows.SingleOrDefault();
        if (ruleRow == null)
            return null;

        const string windowsSql = @"
                SELECT
                    [StartRoundNumber],
                    [EndRoundNumber],
                    [MaxUsesInWindow]
                FROM [LeagueBoostWindows]
                WHERE [LeagueBoostRuleId] = @LeagueBoostRuleId
                ORDER BY [StartRoundNumber];";

        var windowRows = await _dbConnection.QueryAsync<LeagueBoostWindowRow>(
            windowsSql,
            cancellationToken,
            new { ruleRow.LeagueBoostRuleId });

        var windows = windowRows.Select(w => new BoostWindowSnapshot
        {
            StartRoundNumber = w.StartRoundNumber,
            EndRoundNumber = w.EndRoundNumber,
            MaxUsesInWindow = w.MaxUsesInWindow
        }).ToList();

        return new LeagueBoostRuleSnapshot
        {
            IsEnabled = ruleRow.IsEnabled,
            TotalUsesPerSeason = ruleRow.TotalUsesPerSeason,
            Windows = windows
        };
    }

    public async Task<BoostUsageSnapshot> GetUserBoostUsageSnapshotAsync(string userId, int leagueId, int seasonId, int roundId, string boostCode, CancellationToken cancellationToken)
    {
        var (_, roundNumber) = await GetRoundInfoAsync(roundId, cancellationToken);

        const string seasonUsesSql = @"
                SELECT COUNT(*) AS Count
                FROM [UserBoostUsages] ubu
                INNER JOIN [BoostDefinitions] bd
                    ON ubu.[BoostDefinitionId] = bd.[Id]
                WHERE ubu.[UserId] = @UserId
                  AND ubu.[LeagueId] = @LeagueId
                  AND ubu.[SeasonId] = @SeasonId
                  AND bd.[Code] = @BoostCode;";

        var seasonRows = await _dbConnection.QueryAsync<CountRow>(
            seasonUsesSql,
            cancellationToken,
            new { UserId = userId, LeagueId = leagueId, SeasonId = seasonId, BoostCode = boostCode });

        var seasonUses = seasonRows.SingleOrDefault()?.Count ?? 0;

        const string usedThisRoundSql = @"
                SELECT COUNT(*) AS Count
                FROM [UserBoostUsages] ubu
                INNER JOIN [BoostDefinitions] bd
                    ON ubu.[BoostDefinitionId] = bd.[Id]
                WHERE ubu.[UserId] = @UserId
                  AND ubu.[LeagueId] = @LeagueId
                  AND ubu.[SeasonId] = @SeasonId
                  AND ubu.[RoundId] = @RoundId
                  AND bd.[Code] = @BoostCode;";

        var usedRows = await _dbConnection.QueryAsync<CountRow>(
            usedThisRoundSql,
            cancellationToken,
            new { UserId = userId, LeagueId = leagueId, SeasonId = seasonId, RoundId = roundId, BoostCode = boostCode });

        var usedThisRound = (usedRows.SingleOrDefault()?.Count ?? 0) > 0;

        const string activeWindowsSql = @"
                SELECT lbw.[StartRoundNumber], lbw.[EndRoundNumber], lbw.[MaxUsesInWindow]
                FROM [LeagueBoostWindows] lbw
                INNER JOIN [LeagueBoostRules] lbr ON lbw.[LeagueBoostRuleId] = lbr.[Id]
                INNER JOIN [BoostDefinitions] bd ON lbr.[BoostDefinitionId] = bd.[Id]
                WHERE lbr.[LeagueId] = @LeagueId
                  AND bd.[Code] = @BoostCode
                  AND @RoundNumber BETWEEN lbw.[StartRoundNumber] AND lbw.[EndRoundNumber]
                ORDER BY lbw.[StartRoundNumber];";

        var activeWindowRows = (await _dbConnection.QueryAsync<LeagueBoostWindowRow>(
            activeWindowsSql,
            cancellationToken,
            new { LeagueId = leagueId, BoostCode = boostCode, RoundNumber = roundNumber })).ToList();

        var windowUses = 0;

        if (activeWindowRows.Any())
        {
            var windowRows = activeWindowRows.ToList();

            const string windowCountSql = @"
                    SELECT COUNT(*) AS CountInWindow
                    FROM [UserBoostUsages] ubu
                    INNER JOIN [BoostDefinitions] bd ON ubu.[BoostDefinitionId] = bd.[Id]
                    INNER JOIN [Rounds] r ON ubu.[RoundId] = r.[Id]
                    WHERE ubu.[UserId] = @UserId
                      AND ubu.[LeagueId] = @LeagueId
                      AND ubu.[SeasonId] = @SeasonId
                      AND bd.[Code] = @BoostCode
                      AND r.[RoundNumber] BETWEEN @StartRound AND @EndRound;";

            var maxWindowUses = 0;

            foreach (var w in windowRows)
            {
                var rows = await _dbConnection.QueryAsync<CountRow>(
                    windowCountSql,
                    cancellationToken,
                    new { UserId = userId, LeagueId = leagueId, SeasonId = seasonId, BoostCode = boostCode, StartRound = w.StartRoundNumber, EndRound = w.EndRoundNumber });

                var count = rows.SingleOrDefault()?.Count ?? 0;
                if (count > maxWindowUses)
                    maxWindowUses = count;
            }

            windowUses = maxWindowUses;
        }

        return new BoostUsageSnapshot
        {
            SeasonUses = seasonUses,
            WindowUses = windowUses,
            HasUsedThisRound = usedThisRound
        };
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class RoundInfoRow
    {
        public int SeasonId { get; init; }
        public int RoundNumber { get; init; }
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class LeagueBoostRuleRow
    {
        public bool IsEnabled { get; init; }
        public int TotalUsesPerSeason { get; init; }
        public int LeagueBoostRuleId { get; init; }
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class LeagueBoostWindowRow
    {
        public int StartRoundNumber { get; init; }
        public int EndRoundNumber { get; init; }
        public int MaxUsesInWindow { get; init; }
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Local")]
    private sealed class CountRow
    {
        public int Count { get; init; }
    }
}