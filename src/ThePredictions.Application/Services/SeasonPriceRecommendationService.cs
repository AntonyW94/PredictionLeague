using Ardalis.GuardClauses;
using ThePredictions.Application.Data;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Pricing;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Services;

public class SeasonPriceRecommendationService(IApplicationReadDbConnection dbConnection)
    : ISeasonPriceRecommendationService
{
    // Pricing assumptions (ADR 0006). Kept as constants for now; can move to config later.
    private const decimal BufferRate = 0.15m;          // +15% on top of covered costs
    private const decimal StripePercent = 0.015m;      // UK card fee ~1.5%
    private const decimal StripeFixedFee = 0.20m;      // UK card fee 20p
    private const decimal MinimumFloor = 1.00m;        // small floor covering fees + a little
    private const decimal RoundingIncrement = 0.50m;   // round suggestions up to the nearest 50p

    public async Task<PriceRecommendation> RecommendAsync(
        int competitionId,
        int numberOfRounds,
        DateTime startDateUtc,
        int? seasonId,
        CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(competitionId);
        Guard.Against.NegativeOrZero(numberOfRounds);
        Guard.Against.Default(startDateUtc);

        var annualRunningCost = await GetAnnualRunningCostAsync(cancellationToken);
        var otherPaidRounds = await GetOtherPaidRoundsInHorizonAsync(startDateUtc, seasonId, cancellationToken);
        var expectedPlayers = await GetLastComparableSeasonPlayerCountAsync(competitionId, seasonId, cancellationToken);

        return PriceRecommendationCalculator.Recommend(
            annualRunningCost: annualRunningCost,
            seasonRounds: numberOfRounds,
            totalPaidRoundsInHorizon: numberOfRounds + otherPaidRounds,
            expectedPlayers: expectedPlayers,
            bufferRate: BufferRate,
            stripePercent: StripePercent,
            stripeFixedFee: StripeFixedFee,
            minimumFloor: MinimumFloor,
            roundingIncrement: RoundingIncrement);
    }

    private async Task<decimal> GetAnnualRunningCostAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                rc.[Amount],
                rc.[Frequency]
            FROM
                [RunningCosts] rc;";

        var rows = await dbConnection.QueryAsync<CostRow>(sql, cancellationToken);

        // Reuse the domain's annualisation (Monthly x12, Annual/OneOff as-is) for every recorded cost.
        return rows
            .Select(row => new RunningCost(
                0,
                "cost",
                row.Amount,
                Enum.Parse<CostFrequency>(row.Frequency),
                DateTime.UnixEpoch,
                null,
                null,
                DateTime.UnixEpoch))
            .Sum(cost => cost.AnnualisedAmount);
    }

    private async Task<int> GetOtherPaidRoundsInHorizonAsync(DateTime startDateUtc, int? seasonId, CancellationToken cancellationToken)
    {
        // Annual costs are shared across the paid seasons that run during the 12 months from this
        // season's start, weighted by length. A season overlaps the window when it starts before the
        // window ends and ends after the window begins. Free seasons (no Standard price) are excluded.
        const string sql = @"
            SELECT
                ISNULL(SUM(s.[NumberOfRounds]), 0)
            FROM
                [Seasons] s
            WHERE
                s.[PassStandardPrice] IS NOT NULL
                AND (@SeasonId IS NULL OR s.[Id] <> @SeasonId)
                AND s.[StartDateUtc] < @HorizonEnd
                AND s.[EndDateUtc] > @HorizonStart;";

        return await dbConnection.QuerySingleOrDefaultAsync<int>(
            sql,
            cancellationToken,
            new
            {
                SeasonId = seasonId,
                HorizonStart = startDateUtc,
                HorizonEnd = startDateUtc.AddYears(1)
            });
    }

    private async Task<int?> GetLastComparableSeasonPlayerCountAsync(int competitionId, int? seasonId, CancellationToken cancellationToken)
    {
        // Break-even denominator: distinct approved participants of the most recently ended season of
        // the same competition. Returns 0 when there is no comparable season; mapped to null so the
        // calculator returns a blank, explained recommendation.
        const string sql = @"
            SELECT
                COUNT(DISTINCT lm.[UserId])
            FROM
                [LeagueMembers] lm
                INNER JOIN [Leagues] l ON l.[Id] = lm.[LeagueId]
            WHERE
                lm.[Status] = @ApprovedStatus
                AND l.[SeasonId] = (
                    SELECT TOP 1
                        s.[Id]
                    FROM
                        [Seasons] s
                    WHERE
                        s.[CompetitionId] = @CompetitionId
                        AND (@SeasonId IS NULL OR s.[Id] <> @SeasonId)
                        AND s.[EndDateUtc] < @NowUtc
                    ORDER BY
                        s.[EndDateUtc] DESC
                );";

        var count = await dbConnection.QuerySingleOrDefaultAsync<int>(
            sql,
            cancellationToken,
            new
            {
                CompetitionId = competitionId,
                SeasonId = seasonId,
                ApprovedStatus = nameof(LeagueMemberStatus.Approved),
                NowUtc = DateTime.UtcNow
            });

        return count > 0 ? count : null;
    }

    private sealed class CostRow
    {
        public decimal Amount { get; init; }
        public string Frequency { get; init; } = string.Empty;
    }
}
