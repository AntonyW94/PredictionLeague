using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Predictions.Queries;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Persistence.SqlServer.Queries.Predictions;

/// <summary>
/// The SQL Server reads behind <see cref="IPredictionLeaguesQuery"/>.
///
/// Three reads where there were two statements. What is gone: a <c>CASE WHEN EXISTS</c> that decided whether a league runs
/// boosts, a second one with a <c>NOT EXISTS</c> nested inside it that decided whether the player had one left this season,
/// and the ordering.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class PredictionLeaguesQuery(IApplicationReadDbConnection dbConnection) : IPredictionLeaguesQuery
{
    public async Task<PredictionLeaguesData> ExecuteAsync(string userId, int seasonId, CancellationToken cancellationToken)
    {
        var leagues = await GetLeaguesAsync(userId, seasonId, cancellationToken);

        if (leagues.Count == 0)
            return new PredictionLeaguesData(leagues, [], []);

        var boostRules = await GetBoostRulesAsync(userId, seasonId, cancellationToken);
        var boostUsages = await GetBoostUsagesAsync(userId, seasonId, cancellationToken);

        return new PredictionLeaguesData(leagues, boostRules, boostUsages);
    }

    private async Task<IReadOnlyList<PredictionLeagueRow>> GetLeaguesAsync(
        string userId,
        int seasonId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                l.[Id] AS [LeagueId],
                l.[Name]
            FROM
                [Leagues] l
            INNER JOIN
                [LeagueMembers] lm ON lm.[LeagueId] = l.[Id]
            WHERE
                l.[SeasonId] = @SeasonId
                AND lm.[UserId] = @UserId
                AND lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<PredictionLeagueRow>(
            sql, cancellationToken,
            new { UserId = userId, SeasonId = seasonId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    /// <summary>
    /// The boost rules of every league the player is in, switched on or off. Whether a league counts as running boosts is a
    /// rule, and a league whose rules are all switched off is not the same as one with no rules at all.
    /// </summary>
    private async Task<IReadOnlyList<PredictionBoostRuleRow>> GetBoostRulesAsync(
        string userId,
        int seasonId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                lbr.[LeagueId],
                lbr.[BoostDefinitionId],
                lbr.[IsEnabled],
                lbr.[TotalUsesPerSeason]
            FROM
                [LeagueBoostRules] lbr
            INNER JOIN
                [Leagues] l ON l.[Id] = lbr.[LeagueId]
            INNER JOIN
                [LeagueMembers] lm ON lm.[LeagueId] = l.[Id]
            WHERE
                l.[SeasonId] = @SeasonId
                AND lm.[UserId] = @UserId
                AND lm.[Status] = @ApprovedStatus;";

        return (await dbConnection.QueryAsync<PredictionBoostRuleRow>(
            sql, cancellationToken,
            new { UserId = userId, SeasonId = seasonId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) })).ToList();
    }

    /// <summary>
    /// Every boost the player has used in the season, with the round it was used in - which is what lets one set of rows
    /// answer both "is there one left" and "which is picked for this round".
    /// </summary>
    private async Task<IReadOnlyList<PredictionBoostUsageRow>> GetBoostUsagesAsync(
        string userId,
        int seasonId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                ubu.[LeagueId],
                ubu.[BoostDefinitionId],
                ubu.[RoundId],
                bd.[Code] AS [BoostCode]
            FROM
                [UserBoostUsages] ubu
            INNER JOIN
                [BoostDefinitions] bd ON bd.[Id] = ubu.[BoostDefinitionId]
            WHERE
                ubu.[UserId] = @UserId
                AND ubu.[SeasonId] = @SeasonId;";

        return (await dbConnection.QueryAsync<PredictionBoostUsageRow>(
            sql, cancellationToken, new { UserId = userId, SeasonId = seasonId })).ToList();
    }
}
