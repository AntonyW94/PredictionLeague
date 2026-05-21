using MediatR;
using ThePredictions.Application.Data;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using System.Diagnostics.CodeAnalysis;

namespace ThePredictions.Application.Features.Leagues.Queries;

public class GetLeagueRecordsQueryHandler(
    IApplicationReadDbConnection dbConnection,
    ILeagueMembershipService membershipService) : IRequestHandler<GetLeagueRecordsQuery, LeagueRecordsDto?>
{
    public async Task<LeagueRecordsDto?> Handle(GetLeagueRecordsQuery request, CancellationToken cancellationToken)
    {
        await membershipService.EnsureApprovedMemberAsync(request.LeagueId, request.UserId, cancellationToken);

        const string sql = @"
            DECLARE @SeasonId int = (SELECT [SeasonId] FROM [Leagues] WHERE [Id] = @LeagueId);

            SELECT
                l.[IsFree],

                top_round.[PlayerName] AS TopRoundPlayerName,
                ISNULL(top_round.[Points], 0) AS TopRoundPoints,
                top_round.[RoundNumber] AS TopRoundNumber,

                low_round.[PlayerName] AS LowestRoundPlayerName,
                ISNULL(low_round.[Points], 0) AS LowestRoundPoints,
                low_round.[RoundNumber] AS LowestRoundNumber,

                most_exact.[PlayerName] AS MostExactInRoundPlayerName,
                ISNULL(most_exact.[ExactCount], 0) AS MostExactInRoundCount,
                most_exact.[RoundNumber] AS MostExactInRoundNumber,

                champion.[PlayerName] AS ChampionName,
                ISNULL(champion.[Points], 0) AS ChampionPoints,

                top_earner.[PlayerName] AS TopEarnerName,
                ISNULL(top_earner.[Amount], 0) AS TopEarnerAmount,

                most_rounds.[PlayerName] AS MostRoundsWonPlayerName,
                ISNULL(most_rounds.[WinCount], 0) AS MostRoundsWonCount,

                most_months.[PlayerName] AS MostMonthsWonPlayerName,
                ISNULL(most_months.[WinCount], 0) AS MostMonthsWonCount,

                ISNULL(total_exact.[Total], 0) AS TotalExactScores,

                biggest_prize.[PlayerName] AS BiggestPrizePlayerName,
                ISNULL(biggest_prize.[Amount], 0) AS BiggestPrizeAmount,
                biggest_prize.[Description] AS BiggestPrizeDescription,

                top_gameweek.[RoundNumber] AS HighestGameweekRoundNumber,
                ISNULL(top_gameweek.[TotalPoints], 0) AS HighestGameweekPoints
            FROM
                [Leagues] l

                OUTER APPLY (
                    SELECT TOP 1
                        u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                        lrr.[BoostedPoints] AS Points,
                        r.[RoundNumber]
                    FROM [LeagueRoundResults] lrr
                    INNER JOIN [Rounds] r ON r.[Id] = lrr.[RoundId]
                    INNER JOIN [AspNetUsers] u ON u.[Id] = lrr.[UserId]
                    WHERE lrr.[LeagueId] = l.[Id]
                    ORDER BY lrr.[BoostedPoints] DESC, r.[RoundNumber] ASC
                ) top_round

                OUTER APPLY (
                    SELECT TOP 1
                        u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                        lrr.[BoostedPoints] AS Points,
                        r.[RoundNumber]
                    FROM [LeagueRoundResults] lrr
                    INNER JOIN [Rounds] r ON r.[Id] = lrr.[RoundId]
                    INNER JOIN [AspNetUsers] u ON u.[Id] = lrr.[UserId]
                    WHERE lrr.[LeagueId] = l.[Id]
                        AND EXISTS (
                            SELECT 1
                            FROM [UserPredictions] up
                            INNER JOIN [Matches] m ON m.[Id] = up.[MatchId]
                            WHERE m.[RoundId] = lrr.[RoundId] AND up.[UserId] = lrr.[UserId]
                        )
                    ORDER BY lrr.[BoostedPoints] ASC, r.[RoundNumber] ASC
                ) low_round

                OUTER APPLY (
                    SELECT TOP 1
                        u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                        rr.[ExactScoreCount] AS ExactCount,
                        r.[RoundNumber]
                    FROM [RoundResults] rr
                    INNER JOIN [Rounds] r ON r.[Id] = rr.[RoundId]
                    INNER JOIN [AspNetUsers] u ON u.[Id] = rr.[UserId]
                    INNER JOIN [LeagueMembers] lm ON lm.[UserId] = rr.[UserId] AND lm.[LeagueId] = l.[Id] AND lm.[Status] = @ApprovedStatus
                    WHERE r.[SeasonId] = @SeasonId
                    ORDER BY rr.[ExactScoreCount] DESC, r.[RoundNumber] ASC
                ) most_exact

                OUTER APPLY (
                    SELECT TOP 1
                        u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                        ISNULL(SUM(lrr.[BoostedPoints]), 0) AS Points
                    FROM [LeagueMembers] lm
                    INNER JOIN [AspNetUsers] u ON u.[Id] = lm.[UserId]
                    LEFT JOIN [LeagueRoundResults] lrr ON lrr.[UserId] = lm.[UserId] AND lrr.[LeagueId] = lm.[LeagueId]
                    WHERE lm.[LeagueId] = l.[Id] AND lm.[Status] = @ApprovedStatus
                    GROUP BY lm.[UserId], u.[FirstName], u.[LastName]
                    ORDER BY Points DESC
                ) champion

                OUTER APPLY (
                    SELECT TOP 1
                        u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                        SUM(w.[Amount]) AS Amount
                    FROM [Winnings] w
                    INNER JOIN [LeaguePrizeSettings] lps ON lps.[Id] = w.[LeaguePrizeSettingId]
                    INNER JOIN [AspNetUsers] u ON u.[Id] = w.[UserId]
                    WHERE lps.[LeagueId] = l.[Id]
                    GROUP BY w.[UserId], u.[FirstName], u.[LastName]
                    ORDER BY Amount DESC
                ) top_earner

                OUTER APPLY (
                    SELECT TOP 1
                        u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                        COUNT(*) AS WinCount
                    FROM (
                        SELECT lrr.[UserId],
                            RANK() OVER (PARTITION BY lrr.[RoundId] ORDER BY lrr.[BoostedPoints] DESC) AS Rnk,
                            lrr.[BoostedPoints]
                        FROM [LeagueRoundResults] lrr
                        INNER JOIN [Rounds] r ON r.[Id] = lrr.[RoundId]
                        WHERE lrr.[LeagueId] = l.[Id] AND r.[Status] = @CompletedStatus
                    ) ranked
                    INNER JOIN [AspNetUsers] u ON u.[Id] = ranked.[UserId]
                    WHERE ranked.[Rnk] = 1 AND ranked.[BoostedPoints] > 0
                    GROUP BY ranked.[UserId], u.[FirstName], u.[LastName]
                    ORDER BY WinCount DESC
                ) most_rounds

                OUTER APPLY (
                    SELECT TOP 1
                        u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                        COUNT(*) AS WinCount
                    FROM (
                        SELECT lrr.[UserId],
                            SUM(lrr.[BoostedPoints]) AS MonthPoints,
                            RANK() OVER (PARTITION BY MONTH(r.[StartDateUtc]), YEAR(r.[StartDateUtc]) ORDER BY SUM(lrr.[BoostedPoints]) DESC) AS Rnk
                        FROM [LeagueRoundResults] lrr
                        INNER JOIN [Rounds] r ON r.[Id] = lrr.[RoundId]
                        WHERE lrr.[LeagueId] = l.[Id] AND r.[Status] = @CompletedStatus
                        GROUP BY MONTH(r.[StartDateUtc]), YEAR(r.[StartDateUtc]), lrr.[UserId]
                    ) ranked_months
                    INNER JOIN [AspNetUsers] u ON u.[Id] = ranked_months.[UserId]
                    WHERE ranked_months.[Rnk] = 1 AND ranked_months.[MonthPoints] > 0
                    GROUP BY ranked_months.[UserId], u.[FirstName], u.[LastName]
                    ORDER BY WinCount DESC
                ) most_months

                OUTER APPLY (
                    SELECT SUM(rr.[ExactScoreCount]) AS Total
                    FROM [RoundResults] rr
                    INNER JOIN [Rounds] r ON r.[Id] = rr.[RoundId]
                    INNER JOIN [LeagueMembers] lm ON lm.[UserId] = rr.[UserId] AND lm.[LeagueId] = l.[Id] AND lm.[Status] = @ApprovedStatus
                    WHERE r.[SeasonId] = @SeasonId
                ) total_exact

                OUTER APPLY (
                    SELECT TOP 1
                        u.[FirstName] + ' ' + LEFT(u.[LastName], 1) AS PlayerName,
                        w.[Amount],
                        CASE
                            WHEN lps.[PrizeDescription] IS NOT NULL AND lps.[PrizeDescription] <> '' THEN lps.[PrizeDescription]
                            WHEN lps.[PrizeType] = @RoundPrizeType THEN 'Round ' + CAST(w.[RoundNumber] AS NVARCHAR(10))
                            WHEN lps.[PrizeType] = @MonthlyPrizeType THEN DATENAME(MONTH, DATEFROMPARTS(2000, w.[Month], 1))
                            ELSE NULL
                        END AS [Description]
                    FROM [Winnings] w
                    INNER JOIN [LeaguePrizeSettings] lps ON lps.[Id] = w.[LeaguePrizeSettingId]
                    INNER JOIN [AspNetUsers] u ON u.[Id] = w.[UserId]
                    WHERE lps.[LeagueId] = l.[Id]
                    ORDER BY w.[Amount] DESC, w.[AwardedDateUtc] ASC
                ) biggest_prize

                OUTER APPLY (
                    SELECT TOP 1
                        r.[RoundNumber],
                        SUM(lrr.[BoostedPoints]) AS TotalPoints
                    FROM [LeagueRoundResults] lrr
                    INNER JOIN [Rounds] r ON r.[Id] = lrr.[RoundId]
                    WHERE lrr.[LeagueId] = l.[Id]
                    GROUP BY r.[Id], r.[RoundNumber]
                    ORDER BY TotalPoints DESC, r.[RoundNumber] ASC
                ) top_gameweek
            WHERE
                l.[Id] = @LeagueId;";

        var result = await dbConnection.QuerySingleOrDefaultAsync<LeagueRecordsDto>(
            sql,
            cancellationToken,
            new
            {
                request.LeagueId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                CompletedStatus = nameof(RoundStatus.Completed),
                RoundPrizeType = PrizeType.Round,
                MonthlyPrizeType = PrizeType.Monthly
            });

        return result;
    }
}
