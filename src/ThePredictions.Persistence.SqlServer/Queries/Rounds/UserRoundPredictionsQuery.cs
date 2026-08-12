using System.Diagnostics.CodeAnalysis;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Rounds.Queries;

namespace ThePredictions.Persistence.SqlServer.Queries.Rounds;

/// <summary>
/// The SQL Server read behind <see cref="IUserRoundPredictionsQuery"/>.
///
/// Every prediction this player has in the round, joined to nothing. The two statements this replaces reached these rows
/// through a join to the fixtures - one a left join to keep the unpredicted ones and one an inner join to drop them - which is
/// a rule about what each screen is for rather than a fact about the data.
/// </summary>
[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public sealed class UserRoundPredictionsQuery(IApplicationReadDbConnection dbConnection) : IUserRoundPredictionsQuery
{
    public async Task<IReadOnlyList<UserRoundPredictionRow>> ExecuteAsync(
        string userId,
        int roundId,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                up.[MatchId],
                up.[PredictedHomeScore],
                up.[PredictedAwayScore],
                up.[Outcome]
            FROM
                [UserPredictions] up
            INNER JOIN
                [Matches] m ON m.[Id] = up.[MatchId]
            WHERE
                m.[RoundId] = @RoundId
                AND up.[UserId] = @UserId;";

        return (await dbConnection.QueryAsync<UserRoundPredictionRow>(
            sql, cancellationToken, new { UserId = userId, RoundId = roundId })).ToList();
    }
}
