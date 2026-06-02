using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Services.Prizes;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Services.Prizes;

public class PrizeApportionmentServiceTests
{
    private static RankTable DefaultTable() => new(new[]
    {
        new RankBand(2, 5, new[] { 100 }),
        new RankBand(6, 10, new[] { 70, 30 }),
        new RankBand(11, 20, new[] { 50, 30, 20 }),
        new RankBand(21, 40, new[] { 50, 25, 15, 10 }),
        new RankBand(41, 75, new[] { 40, 25, 15, 12, 8 }),
        new RankBand(76, null, new[] { 35, 22, 15, 12, 9, 7 })
    });

    private static PrizeCategoryAllocation Overall(int perEntry, RankTable? table = null) =>
        new() { Category = PrizeType.Overall, Kind = PrizeCategoryKind.EndOfSeason, PerEntryPounds = perEntry, RankTable = table ?? DefaultTable() };

    private static PrizeCategoryAllocation Round(int perEntry) =>
        new() { Category = PrizeType.Round, Kind = PrizeCategoryKind.Recurring, PerEntryPounds = perEntry };

    private static PrizeCategoryAllocation Monthly(int perEntry) =>
        new() { Category = PrizeType.Monthly, Kind = PrizeCategoryKind.Recurring, PerEntryPounds = perEntry };

    private static PrizeCategoryAllocation Exact(int perEntry) =>
        new() { Category = PrizeType.MostExactScores, Kind = PrizeCategoryKind.EndOfSeason, PerEntryPounds = perEntry };

    private static PrizeCategoryAllocation Section(int perEntry, RankTable? table = null) =>
        new() { Category = PrizeType.Section, Kind = PrizeCategoryKind.Staged, PerEntryPounds = perEntry, RankTable = table ?? DefaultTable() };

    private static int TotalAllocated(PrizeBreakdown breakdown) => breakdown.Categories.Sum(c => c.SubPotPounds);

    private static decimal SlotFor(PrizeBreakdown breakdown, PrizeType category, int rank) =>
        breakdown.Categories.First(c => c.Category == category).Slots.First(s => s.Rank == rank).Amount;

    #region Conservation

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(50)]
    [InlineData(100)]
    public void Apportion_ShouldConserveThePot_AcrossEntrantCounts(int entrants)
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = entrants,
            StakePounds = 13,
            AdminTopUpPounds = 0,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            NumberOfMonths = 9,
            Categories = new[] { Overall(8), Round(3), Exact(2) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        breakdown.PotPounds.Should().Be(13 * entrants);
        TotalAllocated(breakdown).Should().Be(breakdown.PotPounds);
    }

    [Fact]
    public void Apportion_ShouldConserveThePot_WithAdminTopUpAndAllCategories()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 30,
            StakePounds = 10,
            AdminTopUpPounds = 57,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            NumberOfMonths = 9,
            Categories = new[] { Overall(5), Round(3), Monthly(0), Exact(2) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        breakdown.PotPounds.Should().Be(10 * 30 + 57);
        TotalAllocated(breakdown).Should().Be(breakdown.PotPounds);
    }

    #endregion

    #region Overall - £5 rounding

    [Fact]
    public void Apportion_ShouldRoundEveryOverallRankToCleanFiver_WhenAboveThreshold()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 12,
            StakePounds = 13,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[] { Overall(13) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        var overall = breakdown.Categories.Single(c => c.Category == PrizeType.Overall);
        overall.Slots.Should().HaveCount(3);
        // Overall is the only category, so the odd £1 falls on 1st; ranks below 1st stay clean £5.
        overall.Slots.Where(s => s.Rank > 1).Select(s => s.Amount).Should().AllSatisfy(a => (a % 5).Should().Be(0));
        // overallSub = 156 -> floored 155 (31 units), [50/30/20] -> [16,9,6] units -> [80,45,30]; odd £1 onto 1st.
        SlotFor(breakdown, PrizeType.Overall, 1).Should().Be(81m);
        SlotFor(breakdown, PrizeType.Overall, 2).Should().Be(45m);
        SlotFor(breakdown, PrizeType.Overall, 3).Should().Be(30m);
    }

    [Fact]
    public void Apportion_ShouldSpillOverallRemainderToExact_WhenExactEnabled()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 12,
            StakePounds = 13,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[] { Overall(11), Exact(2) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // overallSub = 132 -> floored 130, odd £2 -> Exact. exactSub = 2*12 + 2 = 26.
        SlotFor(breakdown, PrizeType.Overall, 1).Should().Be(70m);
        breakdown.Categories.Single(c => c.Category == PrizeType.MostExactScores).SubPotPounds.Should().Be(26);
        TotalAllocated(breakdown).Should().Be(breakdown.PotPounds);
    }

    [Fact]
    public void Apportion_ShouldNotSpill_WhenOverallSubIsAlreadyCleanFiver()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 10,
            StakePounds = 12,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[] { Overall(10), Exact(2) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // overallSub = 100 (clean), no remainder; exact = 20.
        breakdown.Categories.Single(c => c.Category == PrizeType.MostExactScores).SubPotPounds.Should().Be(20);
        TotalAllocated(breakdown).Should().Be(breakdown.PotPounds);
    }

    [Fact]
    public void Apportion_ShouldUsePoundGranularity_WhenOverallBelowThreshold()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 8,
            StakePounds = 10,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[] { Overall(10) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // overallSub = 80 (< £100 threshold) -> £1 granular [70,30] -> [56,24].
        SlotFor(breakdown, PrizeType.Overall, 1).Should().Be(56m);
        SlotFor(breakdown, PrizeType.Overall, 2).Should().Be(24m);
    }

    [Fact]
    public void Apportion_ShouldUsePoundGranularity_WhenOverallSubBelowFive()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 3,
            StakePounds = 1,
            OverallRoundingThresholdPounds = 0,
            NumberOfRounds = 38,
            Categories = new[] { Overall(1) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // Threshold 0 but overallSub = 3 (< £5) so still £1 granular, winner takes all.
        SlotFor(breakdown, PrizeType.Overall, 1).Should().Be(3m);
        TotalAllocated(breakdown).Should().Be(3);
    }

    [Fact]
    public void Apportion_ShouldDropZeroPlaces_WhenPotTooSmallForLowerRanks()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 11,
            StakePounds = 1,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[] { Overall(1) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // overallSub = 11, table [50,30,20] -> [6,3,2], all > 0... use even smaller to drop.
        var overall = breakdown.Categories.Single(c => c.Category == PrizeType.Overall);
        overall.Slots.Should().OnlyContain(s => s.Amount > 0);
        TotalAllocated(breakdown).Should().Be(11);
    }

    [Fact]
    public void Apportion_ShouldDropZeroLowerPlaces_WhenSubPotIsOnePound()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 11,
            StakePounds = 0,
            AdminTopUpPounds = 1,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[] { Overall(0) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // Pot = £1 -> only 1st place lights up.
        var overall = breakdown.Categories.Single(c => c.Category == PrizeType.Overall);
        overall.Slots.Should().ContainSingle();
        SlotFor(breakdown, PrizeType.Overall, 1).Should().Be(1m);
    }

    #endregion

    #region Recurring

    [Fact]
    public void Apportion_ShouldKeepPerRoundPrizeUniform_AndSpillRemainderToExact()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 10,
            StakePounds = 5,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 7,
            Categories = new[] { Round(3), Exact(2) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // Round sub = 30, 7 rounds -> £4/round, remainder £2 -> Exact. exact = 2*10 + 2 = 22.
        var round = breakdown.Categories.Single(c => c.Category == PrizeType.Round);
        round.Slots.Should().ContainSingle(s => s.Label == "Per round" && s.Amount == 4m);
        round.SubPotPounds.Should().Be(28);
        breakdown.Categories.Single(c => c.Category == PrizeType.MostExactScores).SubPotPounds.Should().Be(22);
        TotalAllocated(breakdown).Should().Be(50);
    }

    [Fact]
    public void Apportion_ShouldLabelMonthlyPrize_AndSpillRemainderToOverall_WhenNoExact()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 10,
            StakePounds = 5,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            NumberOfMonths = 4,
            Categories = new[] { Monthly(3), Overall(2) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // Monthly sub = 30, 4 months -> £7/month, remainder £2 -> Overall. overallSub = 2*10 + 2 = 22.
        var monthly = breakdown.Categories.Single(c => c.Category == PrizeType.Monthly);
        monthly.Slots.Should().ContainSingle(s => s.Label == "Per month" && s.Amount == 7m);
        breakdown.Categories.Single(c => c.Category == PrizeType.Overall).SubPotPounds.Should().Be(22);
        TotalAllocated(breakdown).Should().Be(50);
    }

    [Fact]
    public void Apportion_ShouldSpillRecurringRemainderToSection_WhenOnlySectionAbsorbs()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 10,
            StakePounds = 5,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 7,
            Categories = new[] { Round(3), Section(2) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // Round sub = 30, 7 rounds -> £4/round, remainder £2 -> Section. sectionSub = 2*10 + 2 = 22.
        breakdown.Categories.Single(c => c.Category == PrizeType.Section).SubPotPounds.Should().Be(22);
        TotalAllocated(breakdown).Should().Be(50);
    }

    [Fact]
    public void Apportion_ShouldAddFinalEventBonus_WhenRecurringOnly()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 40,
            StakePounds = 1,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[] { Round(1) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // Round sub = 40, 38 rounds -> £1/round, remainder £2 -> final round bonus (per-round stays uniform).
        var round = breakdown.Categories.Single(c => c.Category == PrizeType.Round);
        round.Slots.Should().Contain(s => s.Label == "Per round" && s.Amount == 1m);
        round.Slots.Should().Contain(s => s.Label == "Final round bonus" && s.Amount == 2m);
        round.SubPotPounds.Should().Be(40);
        TotalAllocated(breakdown).Should().Be(40);
    }

    [Fact]
    public void Apportion_ShouldAddFinalMonthBonus_WhenMonthlyOnly()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 10,
            StakePounds = 1,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            NumberOfMonths = 3,
            Categories = new[] { Monthly(1) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // Monthly sub = 10, 3 months -> £3/month, remainder £1 -> final month bonus.
        var monthly = breakdown.Categories.Single(c => c.Category == PrizeType.Monthly);
        monthly.Slots.Should().Contain(s => s.Label == "Final month bonus" && s.Amount == 1m);
        TotalAllocated(breakdown).Should().Be(10);
    }

    [Fact]
    public void Apportion_ShouldProduceNoPerRoundSlot_WhenSubPotBelowRoundCount()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 1,
            StakePounds = 5,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[] { Round(2), Exact(3) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // Round sub = 2 < 38 rounds -> £0/round, no per-round slot; whole £2 spills to Exact.
        var round = breakdown.Categories.Single(c => c.Category == PrizeType.Round);
        round.Slots.Should().BeEmpty();
        round.SubPotPounds.Should().Be(0);
        breakdown.Categories.Single(c => c.Category == PrizeType.MostExactScores).SubPotPounds.Should().Be(5);
        TotalAllocated(breakdown).Should().Be(5);
    }

    [Fact]
    public void Apportion_ShouldTreatRoundAsZeroEvents_WhenNumberOfRoundsIsZero()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 10,
            StakePounds = 5,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 0,
            Categories = new[] { Round(3), Exact(2) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // 0 rounds -> per-event 0, the entire round sub (£30) spills to Exact.
        breakdown.Categories.Single(c => c.Category == PrizeType.Round).Slots.Should().BeEmpty();
        breakdown.Categories.Single(c => c.Category == PrizeType.MostExactScores).SubPotPounds.Should().Be(50);
        TotalAllocated(breakdown).Should().Be(50);
    }

    #endregion

    #region Section

    [Fact]
    public void Apportion_ShouldSplitSectionFiftyFifty_AndRankEachStage()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 10,
            StakePounds = 5,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[] { Section(5) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        var section = breakdown.Categories.Single(c => c.Category == PrizeType.Section);
        // sectionSub = 50 -> 25/25; each stage table [70,30] -> [18,7] (top-down leftover).
        section.Slots.Should().Contain(s => s.StageName == "Group stage" && s.Rank == 1 && s.Amount == 18m);
        section.Slots.Should().Contain(s => s.StageName == "Group stage" && s.Rank == 2 && s.Amount == 7m);
        section.Slots.Should().Contain(s => s.StageName == "Knockout stage" && s.Rank == 1 && s.Amount == 18m);
        section.Slots.Should().Contain(s => s.StageName == "Knockout stage" && s.Rank == 2 && s.Amount == 7m);
        section.SubPotPounds.Should().Be(50);
        TotalAllocated(breakdown).Should().Be(50);
    }

    [Fact]
    public void Apportion_ShouldGiveOddSectionPoundToGroupStage()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 5,
            StakePounds = 5,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[] { Section(5) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // sectionSub = 25 -> 13/12 (group stage gets the odd pound, top-down).
        var section = breakdown.Categories.Single(c => c.Category == PrizeType.Section);
        section.Slots.Single(s => s.StageName == "Group stage").Amount.Should().Be(13m);
        section.Slots.Single(s => s.StageName == "Knockout stage").Amount.Should().Be(12m);
    }

    [Fact]
    public void Apportion_ShouldUseSinglePlace_WhenSectionHasNoRankTable()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 10,
            StakePounds = 5,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[]
            {
                new PrizeCategoryAllocation { Category = PrizeType.Section, Kind = PrizeCategoryKind.Staged, PerEntryPounds = 5, RankTable = null }
            }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        var section = breakdown.Categories.Single(c => c.Category == PrizeType.Section);
        section.Slots.Should().HaveCount(2);
        section.Slots.Should().OnlyContain(s => s.Rank == 1);
    }

    #endregion

    #region Overall - no rank table

    [Fact]
    public void Apportion_ShouldUseSinglePlace_WhenOverallHasNoRankTable()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 10,
            StakePounds = 10,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[]
            {
                new PrizeCategoryAllocation { Category = PrizeType.Overall, Kind = PrizeCategoryKind.EndOfSeason, PerEntryPounds = 10, RankTable = null }
            }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        var overall = breakdown.Categories.Single(c => c.Category == PrizeType.Overall);
        overall.Slots.Should().ContainSingle();
        SlotFor(breakdown, PrizeType.Overall, 1).Should().Be(100m);
    }

    #endregion

    #region Admin top-up

    [Fact]
    public void Apportion_ShouldSplitAdminTopUpByAllocationWeights()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 1,
            StakePounds = 10,
            AdminTopUpPounds = 10,
            OverallRoundingThresholdPounds = 1000,
            NumberOfRounds = 38,
            Categories = new[] { Overall(8), Exact(2) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // Top-up £10 split 8:2 -> +£8 overall, +£2 exact. overallSub = 8 + 8 = 16, exact = 2 + 2 = 4.
        breakdown.Categories.Single(c => c.Category == PrizeType.Overall).SubPotPounds.Should().Be(16);
        breakdown.Categories.Single(c => c.Category == PrizeType.MostExactScores).SubPotPounds.Should().Be(4);
        TotalAllocated(breakdown).Should().Be(20);
    }

    [Fact]
    public void Apportion_ShouldSplitAdminTopUpEqually_WhenFreeLeagueHasNoAllocationWeights()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 5,
            StakePounds = 0,
            AdminTopUpPounds = 11,
            OverallRoundingThresholdPounds = 1000,
            NumberOfRounds = 38,
            Categories = new[] { Overall(0), Exact(0) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        // Free league: weights all zero -> equal split with top-down leftover -> £6 overall, £5 exact.
        breakdown.PotPounds.Should().Be(11);
        breakdown.Categories.Single(c => c.Category == PrizeType.Overall).SubPotPounds.Should().Be(6);
        breakdown.Categories.Single(c => c.Category == PrizeType.MostExactScores).SubPotPounds.Should().Be(5);
    }

    [Fact]
    public void Apportion_ShouldProduceEmptyBreakdown_WhenPotIsZero()
    {
        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 0,
            StakePounds = 0,
            AdminTopUpPounds = 0,
            OverallRoundingThresholdPounds = 100,
            NumberOfRounds = 38,
            Categories = new[] { Overall(0), Exact(0) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        breakdown.PotPounds.Should().Be(0);
        breakdown.Categories.Should().OnlyContain(c => c.SubPotPounds == 0 && !c.Slots.Any());
    }

    #endregion

    #region Ordinals

    [Fact]
    public void Apportion_ShouldFormatOrdinalsCorrectly_AcrossManyPlaces()
    {
        var thirteenPlaces = new RankTable(new[]
        {
            new RankBand(1, null, new[] { 20, 14, 12, 10, 9, 8, 7, 5, 4, 3, 3, 3, 2 })
        });

        var request = new PrizeApportionmentRequest
        {
            EntrantCount = 50,
            StakePounds = 200,
            OverallRoundingThresholdPounds = 100000,
            NumberOfRounds = 38,
            Categories = new[] { Overall(200, thirteenPlaces) }
        };

        var breakdown = PrizeApportionmentService.Apportion(request);

        var labels = breakdown.Categories.Single(c => c.Category == PrizeType.Overall).Slots.Select(s => s.Label).ToList();
        labels.Should().Contain(new[] { "1st", "2nd", "3rd", "4th", "11th", "12th", "13th" });
    }

    #endregion

    #region Guards

    [Fact]
    public void Apportion_ShouldThrow_WhenRequestIsNull()
    {
        var act = () => PrizeApportionmentService.Apportion(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apportion_ShouldThrow_WhenEntrantCountIsNegative()
    {
        var request = new PrizeApportionmentRequest { EntrantCount = -1, StakePounds = 10, NumberOfRounds = 38, Categories = new[] { Overall(10) } };
        var act = () => PrizeApportionmentService.Apportion(request);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Apportion_ShouldThrow_WhenStakeIsNegative()
    {
        var request = new PrizeApportionmentRequest { EntrantCount = 1, StakePounds = -1, NumberOfRounds = 38, Categories = new[] { Overall(10) } };
        var act = () => PrizeApportionmentService.Apportion(request);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Apportion_ShouldThrow_WhenAdminTopUpIsNegative()
    {
        var request = new PrizeApportionmentRequest { EntrantCount = 1, StakePounds = 10, AdminTopUpPounds = -1, NumberOfRounds = 38, Categories = new[] { Overall(10) } };
        var act = () => PrizeApportionmentService.Apportion(request);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Apportion_ShouldThrow_WhenThresholdIsNegative()
    {
        var request = new PrizeApportionmentRequest { EntrantCount = 1, StakePounds = 10, OverallRoundingThresholdPounds = -1, NumberOfRounds = 38, Categories = new[] { Overall(10) } };
        var act = () => PrizeApportionmentService.Apportion(request);
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
