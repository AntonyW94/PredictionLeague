using FluentAssertions;
using ThePredictions.Application.Features.Admin.Rounds.Commands;
using ThePredictions.Application.Features.Admin.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Rounds.Commands;

public class PrizeNotificationFormatterTests
{
    private static WonPrize Prize(
        PrizeType prizeType,
        string? prizeDescription = null,
        int rank = 1,
        string? stage = null,
        decimal amount = 10m,
        int? roundNumber = null,
        int? month = null,
        string? prizeRoundName = null) =>
        new(LeagueId: 5, LeagueName: "Office League", LeaguePrizeSettingId: 1, PrizeType: prizeType,
            PrizeDescription: prizeDescription, Rank: rank, Stage: stage, Amount: amount,
            RoundNumber: roundNumber, Month: month, PrizeRoundName: prizeRoundName, AlreadyNotified: false);

    #region Title

    [Fact]
    public void Title_ShouldNameTheRound_ForRoundPrize()
    {
        var title = PrizeNotificationFormatter.Title(Prize(PrizeType.Round, roundNumber: 12, prizeRoundName: "Gameweek 12"));

        title.Should().Be("Gameweek 12 round winner");
    }

    [Fact]
    public void Title_ShouldFallBack_ForRoundPrizeWithNoRoundName()
    {
        var title = PrizeNotificationFormatter.Title(Prize(PrizeType.Round, roundNumber: 12, prizeRoundName: null));

        title.Should().Be("Round winner");
    }

    [Fact]
    public void Title_ShouldNameTheMonth_ForMonthlyPrize()
    {
        var title = PrizeNotificationFormatter.Title(Prize(PrizeType.Monthly, month: 11));

        title.Should().Be("November monthly winner");
    }

    [Fact]
    public void Title_ShouldFallBack_ForMonthlyPrizeWithInvalidMonth()
    {
        var title = PrizeNotificationFormatter.Title(Prize(PrizeType.Monthly, month: 0));

        title.Should().Be("Monthly winner");
    }

    [Fact]
    public void Title_ShouldReadMostExactScores_ForMostExactScoresPrize()
    {
        var title = PrizeNotificationFormatter.Title(Prize(PrizeType.MostExactScores));

        title.Should().Be("Most exact scores");
    }

    [Fact]
    public void Title_ShouldUseOrdinalRank_ForOverallPrize()
    {
        var title = PrizeNotificationFormatter.Title(Prize(PrizeType.Overall, rank: 2));

        title.Should().Be("Overall - 2nd");
    }

    [Fact]
    public void Title_ShouldNameTheStage_ForStagePrize()
    {
        var title = PrizeNotificationFormatter.Title(Prize(PrizeType.Stages, stage: "Group stage", rank: 1));

        title.Should().Be("Group stage - 1st");
    }

    [Fact]
    public void Title_ShouldFallBack_ForStagePrizeWithNoStage()
    {
        var title = PrizeNotificationFormatter.Title(Prize(PrizeType.Stages, stage: null, rank: 3));

        title.Should().Be("Stage winner - 3rd");
    }

    [Fact]
    public void Title_ShouldUsePrizeDescription_ForUnknownPrizeTypeWithDescription()
    {
        var title = PrizeNotificationFormatter.Title(Prize((PrizeType)999, prizeDescription: "Wooden spoon"));

        title.Should().Be("Wooden spoon");
    }

    [Fact]
    public void Title_ShouldFallBackToPrize_ForUnknownPrizeTypeWithNoDescription()
    {
        var title = PrizeNotificationFormatter.Title(Prize((PrizeType)999, prizeDescription: null));

        title.Should().Be("Prize");
    }

    #endregion

    #region Money

    [Theory]
    [InlineData(10, "£10")]
    [InlineData(0, "£0")]
    [InlineData(100, "£100")]
    public void Money_ShouldDropPence_ForWholeAmounts(int amount, string expected)
    {
        PrizeNotificationFormatter.Money(amount).Should().Be(expected);
    }

    [Theory]
    [InlineData(10.50, "£10.50")]
    [InlineData(7.05, "£7.05")]
    [InlineData(12.5, "£12.50")]
    public void Money_ShouldShowTwoDecimals_ForFractionalAmounts(decimal amount, string expected)
    {
        PrizeNotificationFormatter.Money(amount).Should().Be(expected);
    }

    #endregion
}
