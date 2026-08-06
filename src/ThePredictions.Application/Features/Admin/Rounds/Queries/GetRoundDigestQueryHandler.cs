using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Admin.Rounds.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetRoundDigestQueryHandler(IApplicationReadDbConnection dbConnection)
    : IRequestHandler<GetRoundDigestQuery, IReadOnlyList<UserRoundDigest>>
{
    public async Task<IReadOnlyList<UserRoundDigest>> Handle(GetRoundDigestQuery request, CancellationToken cancellationToken)
    {
        // Column order must match the RoundDigestRow constructor (Dapper maps positionally).
        const string sql = @"
            WITH TopScorers AS
            (
                SELECT
                    lrr.[LeagueId],
                    u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS TopScorerName,
                    lrr.[BoostedPoints] AS TopScorerPoints,
                    ROW_NUMBER() OVER (PARTITION BY lrr.[LeagueId] ORDER BY lrr.[BoostedPoints] DESC, u.[FirstName]) AS Rn
                FROM
                    [LeagueRoundResults] lrr
                JOIN
                    [AspNetUsers] u ON u.[Id] = lrr.[UserId]
                WHERE
                    lrr.[RoundId] = @RoundId
            ),
            NextRound AS
            (
                SELECT TOP 1
                    nr.[DisplayName] AS NextRoundName,
                    nr.[DeadlineUtc] AS NextRoundDeadlineUtc
                FROM
                    [Rounds] nr
                JOIN
                    [Rounds] cur ON cur.[Id] = @RoundId
                WHERE
                    nr.[SeasonId] = cur.[SeasonId]
                    AND nr.[RoundNumber] > cur.[RoundNumber]
                ORDER BY
                    nr.[RoundNumber]
            )
            SELECT
                u.[Id] AS UserId,
                u.[Email],
                u.[FirstName],
                r.[DisplayName] AS RoundName,
                rr.[ExactScoreCount],
                rr.[CorrectResultCount],
                l.[Id] AS LeagueId,
                l.[Name] AS LeagueName,
                lrr.[BoostedPoints] AS LeaguePoints,
                lms.[OverallRank] AS Position,
                CASE
                    WHEN lms.[OverallRank] IS NOT NULL AND lms.[SnapshotOverallRank] IS NOT NULL
                    THEN lms.[SnapshotOverallRank] - lms.[OverallRank]
                    ELSE NULL
                END AS PositionDelta,
                ts.[TopScorerName],
                ts.[TopScorerPoints],
                nextR.[NextRoundName],
                nextR.[NextRoundDeadlineUtc]
            FROM
                [Rounds] r
            JOIN
                [RoundResults] rr ON rr.[RoundId] = r.[Id]
            JOIN
                [AspNetUsers] u ON u.[Id] = rr.[UserId]
            JOIN
                [LeagueMembers] lm ON lm.[UserId] = u.[Id]
                AND lm.[Status] = @ApprovedStatus
            JOIN
                [Leagues] l ON l.[Id] = lm.[LeagueId]
                AND l.[SeasonId] = r.[SeasonId]
            JOIN
                [LeagueRoundResults] lrr ON lrr.[LeagueId] = l.[Id]
                AND lrr.[RoundId] = r.[Id]
                AND lrr.[UserId] = u.[Id]
            LEFT JOIN
                [LeagueMemberStats] lms ON lms.[LeagueId] = l.[Id]
                AND lms.[UserId] = u.[Id]
            LEFT JOIN
                TopScorers ts ON ts.[LeagueId] = l.[Id]
                AND ts.[Rn] = 1
            LEFT JOIN
                NextRound nextR ON 1 = 1
            WHERE
                r.[Id] = @RoundId
                AND EXISTS
                (
                    SELECT 1
                    FROM [UserPredictions] up
                    JOIN [Matches] m ON m.[Id] = up.[MatchId]
                    WHERE m.[RoundId] = r.[Id]
                        AND up.[UserId] = u.[Id]
                )
            ORDER BY
                u.[Id],
                l.[Name]";

        var rows = await dbConnection.QueryAsync<RoundDigestRow>(
            sql,
            cancellationToken,
            new { RoundId = request.RoundId, ApprovedStatus = nameof(LeagueMemberStatus.Approved) });

        return rows
            .GroupBy(row => row.UserId)
            .Select(group =>
            {
                var first = group.First();
                var leagues = group
                    .Select(row => new LeagueRoundDigest(
                        row.LeagueId,
                        row.LeagueName,
                        row.LeaguePoints,
                        row.Position,
                        row.PositionDelta,
                        row.TopScorerName,
                        row.TopScorerPoints))
                    .ToList();

                return new UserRoundDigest(
                    first.UserId,
                    first.Email,
                    first.FirstName,
                    first.RoundName,
                    first.ExactScoreCount,
                    first.CorrectResultCount,
                    first.NextRoundName,
                    first.NextRoundDeadlineUtc,
                    leagues);
            })
            .ToList();
    }
}
