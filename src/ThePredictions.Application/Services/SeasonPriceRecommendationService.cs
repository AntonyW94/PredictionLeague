using Ardalis.GuardClauses;
using ThePredictions.Application.Data;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Pricing;
using ThePredictions.Domain.Models;

namespace ThePredictions.Application.Services;

public class SeasonPriceRecommendationService(IApplicationReadDbConnection dbConnection)
    : ISeasonPriceRecommendationService
{
    // The buffer, Stripe fee and floor are admin-editable (PricingSettings). Rounding stays fixed.
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

        var settings = await GetPricingSettingsAsync(cancellationToken);
        var stripeFee = await GetStripeFeeAsync(cancellationToken);
        var annualRunningCost = await GetAnnualRunningCostAsync(cancellationToken);
        var otherPaidRounds = await GetOtherPaidRoundsInHorizonAsync(startDateUtc, seasonId, cancellationToken);
        var expectedPlayers = await GetLastComparableSeasonPlayerCountAsync(competitionId, seasonId, cancellationToken);

        return PriceRecommendationCalculator.Recommend(
            annualRunningCost: annualRunningCost,
            seasonRounds: numberOfRounds,
            totalPaidRoundsInHorizon: numberOfRounds + otherPaidRounds,
            expectedPlayers: expectedPlayers,
            bufferRate: settings.BufferRate,
            stripePercent: stripeFee.PercentFee,
            stripeFixedFee: stripeFee.FixedFee,
            minimumFloor: settings.MinimumFloor,
            roundingIncrement: RoundingIncrement);
    }

    private async Task<PricingSettings> GetPricingSettingsAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT TOP 1
                ps.[Id],
                ps.[BufferRate],
                ps.[MinimumFloor]
            FROM
                [PricingSettings] ps
            ORDER BY
                ps.[Id];";

        var settings = await dbConnection.QuerySingleOrDefaultAsync<PricingSettings>(sql, cancellationToken);

        // Fall back to built-in defaults if no row has been seeded yet.
        return settings ?? PricingSettings.CreateDefault();
    }

    private async Task<ServiceFee> GetStripeFeeAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT
                sf.[Id],
                sf.[Provider],
                sf.[PercentFee],
                sf.[FixedFee]
            FROM
                [ServiceFees] sf
            WHERE
                sf.[Provider] = @Provider;";

        var fee = await dbConnection.QuerySingleOrDefaultAsync<ServiceFee>(
            sql, cancellationToken, new { Provider = nameof(ServiceFeeProvider.Stripe) });

        // Fall back to the built-in Stripe default if no row has been seeded yet.
        return fee ?? ServiceFee.CreateDefault(ServiceFeeProvider.Stripe);
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

    // internal so a test can supply rows for the annualisation below; InternalsVisibleTo already
    // exposes this assembly to ThePredictions.Application.Tests.Unit.
    internal sealed class CostRow
    {
        public decimal Amount { get; init; }
        public string Frequency { get; init; } = string.Empty;
    }
}
