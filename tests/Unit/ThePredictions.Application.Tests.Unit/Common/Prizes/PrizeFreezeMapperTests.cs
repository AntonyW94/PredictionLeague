using FluentAssertions;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Prizes;

public class PrizeFreezeMapperTests
{
    private static PrizeBreakdownDto Breakdown() => new()
    {
        Pot = 200,
        EntrantCount = 20,
        Categories = new List<PrizeCategoryBreakdownDto>
        {
            new()
            {
                Category = PrizeType.Overall, Kind = PrizeCategoryKind.EndOfSeason, SubPot = 120,
                Slots = new List<PrizeSlotDto>
                {
                    new() { Label = "1st", Amount = 80, Rank = 1 },
                    new() { Label = "2nd", Amount = 40, Rank = 2 }
                }
            },
            new()
            {
                Category = PrizeType.Round, Kind = PrizeCategoryKind.Recurring, SubPot = 38,
                Slots = new List<PrizeSlotDto> { new() { Label = "Per round", Amount = 1 } }
            },
            new()
            {
                Category = PrizeType.Stages, Kind = PrizeCategoryKind.Staged, SubPot = 42,
                Slots = new List<PrizeSlotDto>
                {
                    new() { Label = "Group stage - 1st", Amount = 21, Rank = 1, StageName = "Group stage" },
                    new() { Label = "Knockout stage - 1st", Amount = 21, Rank = 1, StageName = "Knockout stage" }
                }
            }
        }
    };

    [Fact]
    public void ToPrizeSettings_ShouldMapRankedSlotsPerRank()
    {
        var settings = PrizeFreezeMapper.ToPrizeSettings(Breakdown(), leagueId: 5);

        var overall = settings.Where(s => s.PrizeType == PrizeType.Overall).OrderBy(s => s.Rank).ToList();
        overall.Should().HaveCount(2);
        overall[0].Rank.Should().Be(1);
        overall[0].PrizeAmount.Should().Be(80);
        overall.Should().OnlyContain(s => s.LeagueId == 5);
    }

    [Fact]
    public void ToPrizeSettings_ShouldCollapseRecurringToSinglePerEventSetting()
    {
        var settings = PrizeFreezeMapper.ToPrizeSettings(Breakdown(), leagueId: 5);

        var round = settings.Where(s => s.PrizeType == PrizeType.Round).ToList();
        round.Should().ContainSingle();
        round[0].Rank.Should().Be(1);
        round[0].PrizeAmount.Should().Be(1);
    }

    [Fact]
    public void ToPrizeSettings_ShouldTagSectionSettingsWithStage()
    {
        var settings = PrizeFreezeMapper.ToPrizeSettings(Breakdown(), leagueId: 5);

        var section = settings.Where(s => s.PrizeType == PrizeType.Stages).ToList();
        section.Should().HaveCount(2);
        section.Should().Contain(s => s.Stage == "Group stage");
        section.Should().Contain(s => s.Stage == "Knockout stage");
    }

    [Fact]
    public void ToPrizeSettings_ShouldSkipZeroAmountSlots()
    {
        var breakdown = new PrizeBreakdownDto
        {
            Categories = new List<PrizeCategoryBreakdownDto>
            {
                new()
                {
                    Category = PrizeType.Overall, Kind = PrizeCategoryKind.EndOfSeason,
                    Slots = new List<PrizeSlotDto>
                    {
                        new() { Label = "1st", Amount = 10, Rank = 1 },
                        new() { Label = "2nd", Amount = 0, Rank = 2 }
                    }
                }
            }
        };

        var settings = PrizeFreezeMapper.ToPrizeSettings(breakdown, leagueId: 5);

        settings.Should().ContainSingle();
    }

    [Fact]
    public void ToPrizeSettings_ShouldFallBackToTheFirstSlot_WhenARecurringPrizeHasNoPerEventSlot()
    {
        // A recurring category normally carries one unranked per-event slot. If it only has ranked
        // ones, the first is still the per-event amount rather than nothing being settled at all.
        var breakdown = new PrizeBreakdownDto
        {
            Categories =
            [
                new PrizeCategoryBreakdownDto
                {
                    Category = PrizeType.Round, Kind = PrizeCategoryKind.Recurring, SubPot = 38,
                    Slots = [new PrizeSlotDto { Label = "1st", Amount = 5, Rank = 1 }]
                }
            ]
        };

        var settings = PrizeFreezeMapper.ToPrizeSettings(breakdown, 1);

        settings.Should().ContainSingle();
        settings[0].PrizeType.Should().Be(PrizeType.Round);
        settings[0].PrizeAmount.Should().Be(5);
    }

    [Fact]
    public void ToPrizeSettings_ShouldSettleNothing_WhenARecurringCategoryHasNoSlotsAtAll()
    {
        var breakdown = new PrizeBreakdownDto
        {
            Categories =
            [
                new PrizeCategoryBreakdownDto
                {
                    Category = PrizeType.Round, Kind = PrizeCategoryKind.Recurring, SubPot = 0, Slots = []
                }
            ]
        };

        PrizeFreezeMapper.ToPrizeSettings(breakdown, 1).Should().BeEmpty();
    }
}
