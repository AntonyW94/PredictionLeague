using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetStageLeaderboardQueryHandler(
    IApplicationReadDbConnection dbConnection,
    ILeagueMembershipService membershipService) : IRequestHandler<GetStageLeaderboardQuery, IEnumerable<LeaderboardEntryDto>>
{
    public async Task<IEnumerable<LeaderboardEntryDto>> Handle(GetStageLeaderboardQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.CurrentUserId, cancellationToken);

        const string sql = @"
            WITH StageRounds AS (
                SELECT
                    r.[Id],
                    r.[Status]
                FROM
                    [Rounds] r
                JOIN [TournamentRoundMappings] trm ON trm.[SeasonId] = r.[SeasonId] AND trm.[RoundNumber] = r.[RoundNumber]
                WHERE
                    r.[SeasonId] = (SELECT [SeasonId] FROM [Leagues] WHERE [Id] = @LeagueId)
                    AND CASE WHEN trm.[Stages] LIKE '%Group%' THEN @GroupStage ELSE @KnockoutStage END = @Stage
            )

            SELECT
                RANK() OVER (ORDER BY COALESCE(SUM(lrr.[BoostedPoints]), 0) DESC) AS [Rank],
                u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                COALESCE(SUM(lrr.[BoostedPoints]), 0) AS [TotalPoints],
                u.[Id] AS [UserId],

                CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM [StageRounds]
                        WHERE [Status] = @InProgressStatus
                    )
                    AND (
                        SELECT COUNT(*)
                        FROM [StageRounds]
                        WHERE [Status] IN (@InProgressStatus, @CompletedStatus)
                    ) > 1
                    THEN RANK() OVER (
                        ORDER BY COALESCE(SUM(CASE WHEN sr.[Status] = @InProgressStatus THEN 0 ELSE lrr.[BoostedPoints] END), 0) DESC
                    )
                    ELSE NULL
                END AS [SnapshotRank],

                CAST(CASE WHEN EXISTS (
                    SELECT 1
                    FROM [Rounds] r
                    JOIN [Leagues] l ON r.[SeasonId] = l.[SeasonId]
                    WHERE l.[Id] = @LeagueId AND r.[Status] = @InProgressStatus
                ) THEN 1 ELSE 0 END AS bit) AS [IsRoundInProgress]
            FROM
                [LeagueMembers] lm
            JOIN
                [AspNetUsers] u ON lm.[UserId] = u.[Id]
            LEFT JOIN
                [LeagueRoundResults] lrr ON lm.[UserId] = lrr.[UserId] AND lrr.[LeagueId] = @LeagueId AND lrr.[RoundId] IN (SELECT [Id] FROM [StageRounds])
            LEFT JOIN
                [StageRounds] sr ON lrr.[RoundId] = sr.[Id]
            WHERE
                lm.[LeagueId] = @LeagueId
                AND lm.[Status] = @ApprovedStatus
            GROUP BY
                u.[FirstName],
                u.[LastName],
                u.[Id]
            ORDER BY
                [Rank] ASC,
                [PlayerName] ASC;";

        var entries = await dbConnection.QueryAsync<StageLeaderboardQueryResult>(
            sql,
            cancellationToken,
            new
            {
                request.LeagueId,
                Stage = request.Stage.ToString(),
                GroupStage = nameof(TournamentStageGroup.GroupStage),
                KnockoutStage = nameof(TournamentStageGroup.KnockoutStage),
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                InProgressStatus = nameof(RoundStatus.InProgress),
                CompletedStatus = nameof(RoundStatus.Completed)
            }
        );

        return entries.Select(e => new LeaderboardEntryDto
        {
            Rank = e.Rank,
            PlayerName = e.PlayerName,
            TotalPoints = e.TotalPoints,
            UserId = e.UserId,
            SnapshotRank = e.SnapshotRank,
            IsRoundInProgress = e.IsRoundInProgress
        });
    }

    // SnapshotRank is a RANK() window function here, not a stats column, so it arrives as a bigint and
    // stays long? - unlike the overall and monthly leaderboards, which read int snapshot columns.
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record StageLeaderboardQueryResult(
        long Rank,
        string PlayerName,
        int? TotalPoints,
        string UserId,
        long? SnapshotRank,
        bool IsRoundInProgress);
}
