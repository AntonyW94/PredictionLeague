using ThePredictions.Domain.Common.Exceptions;
using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "Query handler: the body is a SQL string plus a mapping. A unit test would mock IApplicationReadDbConnection and verify neither. Covered by tools/ThePredictions.SchemaCheck and E2E.")]
public class GetSeasonRecapQueryHandler(
    IApplicationReadDbConnection dbConnection,
    ILeagueMembershipService membershipService) : IRequestHandler<GetSeasonRecapQuery, SeasonRecapDto>
{
    public async Task<SeasonRecapDto> Handle(GetSeasonRecapQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.UserId, cancellationToken);

        // Query 1: All flat stats + leaderboard-based round/month wins
        const string statsSql = @"
            DECLARE @SeasonId int = (SELECT [SeasonId] FROM [Leagues] WHERE [Id] = @LeagueId);

            SELECT
                l.[IsFree],
                l.[Price] AS LeaguePrice,

                ISNULL(winnings.[TotalWinnings], 0) AS TotalWinnings,
                ISNULL(winnings.[TotalWinnings], 0) - l.[Price] AS ProfitLoss,

                ISNULL(ranking.[FinalPosition], 0) AS FinalPosition,
                ISNULL(member_count.[TotalMembers], 0) AS TotalMembers,

                ISNULL(performance.[AveragePointsPerRound], 0) AS AveragePointsPerRound,
                ISNULL(performance.[BestRoundPoints], 0) AS BestRoundPoints,
                performance.[BestRoundNumber] AS BestRoundNumber,
                ISNULL(performance.[WorstRoundPoints], 0) AS WorstRoundPoints,
                performance.[WorstRoundNumber] AS WorstRoundNumber,
                ISNULL(performance.[TotalExactScores], 0) AS TotalExactScores,

                ISNULL(round_wins.[RoundsWon], 0) AS RoundsWon,
                ISNULL(month_wins.[MonthsWon], 0) AS MonthsWon
            FROM
                [Leagues] l

                OUTER APPLY (
                    SELECT
                        ISNULL(SUM(w.[Amount]), 0) AS TotalWinnings
                    FROM [Winnings] w
                    INNER JOIN [LeaguePrizeSettings] lps ON lps.[Id] = w.[LeaguePrizeSettingId]
                    WHERE lps.[LeagueId] = l.[Id] AND w.[UserId] = @UserId
                ) winnings

                OUTER APPLY (
                    SELECT COUNT(*) AS TotalMembers
                    FROM [LeagueMembers]
                    WHERE [LeagueId] = l.[Id] AND [Status] = @ApprovedStatus
                ) member_count

                OUTER APPLY (
                    SELECT [FinalPosition]
                    FROM (
                        SELECT
                            lm.[UserId],
                            CAST(RANK() OVER (ORDER BY ISNULL(SUM(lrr.[BoostedPoints]), 0) DESC) AS INT) AS FinalPosition
                        FROM [LeagueMembers] lm
                        LEFT JOIN [LeagueRoundResults] lrr ON lrr.[UserId] = lm.[UserId] AND lrr.[LeagueId] = lm.[LeagueId]
                        WHERE lm.[LeagueId] = l.[Id] AND lm.[Status] = @ApprovedStatus
                        GROUP BY lm.[UserId]
                    ) AS r
                    WHERE r.[UserId] = @UserId
                ) ranking

                OUTER APPLY (
                    SELECT
                        AVG(CAST(lrr.[BoostedPoints] AS DECIMAL(10, 2))) AS AveragePointsPerRound,
                        MAX(lrr.[BoostedPoints]) AS BestRoundPoints,
                        (
                            SELECT TOP 1 r.[RoundNumber]
                            FROM [LeagueRoundResults] inner_lrr
                            INNER JOIN [Rounds] r ON r.[Id] = inner_lrr.[RoundId]
                            WHERE inner_lrr.[LeagueId] = l.[Id] AND inner_lrr.[UserId] = @UserId
                            ORDER BY inner_lrr.[BoostedPoints] DESC, r.[RoundNumber] ASC
                        ) AS BestRoundNumber,
                        MIN(lrr.[BoostedPoints]) AS WorstRoundPoints,
                        (
                            SELECT TOP 1 r.[RoundNumber]
                            FROM [LeagueRoundResults] inner_lrr
                            INNER JOIN [Rounds] r ON r.[Id] = inner_lrr.[RoundId]
                            WHERE inner_lrr.[LeagueId] = l.[Id] AND inner_lrr.[UserId] = @UserId
                            ORDER BY inner_lrr.[BoostedPoints] ASC, r.[RoundNumber] ASC
                        ) AS WorstRoundNumber,
                        (
                            SELECT ISNULL(SUM(rr.[ExactScoreCount]), 0)
                            FROM [RoundResults] rr
                            INNER JOIN [Rounds] r ON r.[Id] = rr.[RoundId]
                            WHERE r.[SeasonId] = @SeasonId AND rr.[UserId] = @UserId
                        ) AS TotalExactScores
                    FROM [LeagueRoundResults] lrr
                    WHERE lrr.[LeagueId] = l.[Id] AND lrr.[UserId] = @UserId
                ) performance

                OUTER APPLY (
                    SELECT COUNT(*) AS RoundsWon
                    FROM (
                        SELECT lrr.[UserId], lrr.[BoostedPoints],
                            RANK() OVER (PARTITION BY lrr.[RoundId] ORDER BY lrr.[BoostedPoints] DESC) AS Rnk
                        FROM [LeagueRoundResults] lrr
                        INNER JOIN [Rounds] r ON r.[Id] = lrr.[RoundId]
                        WHERE lrr.[LeagueId] = l.[Id] AND r.[Status] = @CompletedStatus
                    ) ranked
                    WHERE ranked.[UserId] = @UserId AND ranked.[Rnk] = 1 AND ranked.[BoostedPoints] > 0
                ) round_wins

                OUTER APPLY (
                    SELECT COUNT(*) AS MonthsWon
                    FROM (
                        SELECT
                            lrr.[UserId],
                            SUM(lrr.[BoostedPoints]) AS MonthPoints,
                            RANK() OVER (
                                PARTITION BY MONTH(r.[StartDateUtc]), YEAR(r.[StartDateUtc])
                                ORDER BY SUM(lrr.[BoostedPoints]) DESC
                            ) AS Rnk
                        FROM [LeagueRoundResults] lrr
                        INNER JOIN [Rounds] r ON r.[Id] = lrr.[RoundId]
                        WHERE lrr.[LeagueId] = l.[Id] AND r.[Status] = @CompletedStatus
                        GROUP BY MONTH(r.[StartDateUtc]), YEAR(r.[StartDateUtc]), lrr.[UserId]
                    ) ranked_months
                    WHERE ranked_months.[UserId] = @UserId AND ranked_months.[Rnk] = 1 AND ranked_months.[MonthPoints] > 0
                ) month_wins
            WHERE
                l.[Id] = @LeagueId;";

        var stats = await dbConnection.QuerySingleOrDefaultAsync<SeasonRecapQueryResult>(
            statsSql,
            cancellationToken,
            new
            {
                request.LeagueId,
                request.UserId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                CompletedStatus = nameof(RoundStatus.Completed)
            });

        if (stats == null)
            throw new EntityNotFoundException("League", request.LeagueId);

        // Query 2: Highest position held during the season (cumulative rank trajectory)
        const string positionSql = @"
            DECLARE @SeasonId int = (SELECT [SeasonId] FROM [Leagues] WHERE [Id] = @LeagueId);

            WITH CumulativePoints AS (
                SELECT
                    ar.[RoundNumber],
                    am.[UserId],
                    SUM(ISNULL(lrr.[BoostedPoints], 0)) OVER (
                        PARTITION BY am.[UserId]
                        ORDER BY ar.[RoundNumber]
                        ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW
                    ) AS Total
                FROM (
                    SELECT [Id], [RoundNumber]
                    FROM [Rounds]
                    WHERE [SeasonId] = @SeasonId AND [Status] = @CompletedStatus
                ) ar
                CROSS JOIN (
                    SELECT [UserId]
                    FROM [LeagueMembers]
                    WHERE [LeagueId] = @LeagueId AND [Status] = @ApprovedStatus
                ) am
                LEFT JOIN [LeagueRoundResults] lrr
                    ON lrr.[RoundId] = ar.[Id]
                    AND lrr.[UserId] = am.[UserId]
                    AND lrr.[LeagueId] = @LeagueId
            ),
            RanksPerRound AS (
                SELECT
                    [RoundNumber],
                    [UserId],
                    [Total],
                    CAST(RANK() OVER (PARTITION BY [RoundNumber] ORDER BY [Total] DESC) AS INT) AS Rnk
                FROM CumulativePoints
            ),
            UserBest AS (
                SELECT MIN(Rnk) AS BestRank
                FROM RanksPerRound
                WHERE [UserId] = @UserId AND [Total] > 0
            )
            SELECT
                ISNULL(ub.BestRank, 0) AS HighestPosition,
                ISNULL((SELECT COUNT(*) FROM RanksPerRound WHERE [UserId] = @UserId AND Rnk = ub.BestRank), 0) AS RoundsAtHighestPosition
            FROM UserBest ub;";

        var position = await dbConnection.QuerySingleOrDefaultAsync<PositionResult>(
            positionSql,
            cancellationToken,
            new
            {
                request.LeagueId,
                request.UserId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                CompletedStatus = nameof(RoundStatus.Completed)
            });

        return new SeasonRecapDto
        {
            IsFree = stats.IsFree,
            LeaguePrice = stats.LeaguePrice,
            FinalPosition = stats.FinalPosition,
            TotalMembers = stats.TotalMembers,
            TotalWinnings = stats.TotalWinnings,
            ProfitLoss = stats.ProfitLoss,
            AveragePointsPerRound = stats.AveragePointsPerRound,
            BestRoundPoints = stats.BestRoundPoints,
            BestRoundNumber = stats.BestRoundNumber,
            WorstRoundPoints = stats.WorstRoundPoints,
            WorstRoundNumber = stats.WorstRoundNumber,
            TotalExactScores = stats.TotalExactScores,
            RoundsWon = stats.RoundsWon,
            MonthsWon = stats.MonthsWon,
            HighestPosition = position?.HighestPosition ?? 0,
            RoundsAtHighestPosition = position?.RoundsAtHighestPosition ?? 0
        };
    }

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record SeasonRecapQueryResult(
        bool IsFree,
        decimal LeaguePrice,
        decimal TotalWinnings,
        decimal ProfitLoss,
        int FinalPosition,
        int TotalMembers,
        decimal AveragePointsPerRound,
        int BestRoundPoints,
        int? BestRoundNumber,
        int WorstRoundPoints,
        int? WorstRoundNumber,
        int TotalExactScores,
        int RoundsWon,
        int MonthsWon
    );

    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Local")]
    private record PositionResult(int HighestPosition, int RoundsAtHighestPosition);
}
