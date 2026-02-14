using FluentAssertions;
using PredictionLeague.Domain.Common.Enumerations;
using PredictionLeague.Domain.Models;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class LeaguePrizeSettingTests
{
    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreatePrizeSetting_WhenValidParametersProvided()
    {
        // Act
        var setting = LeaguePrizeSetting.Create(1, PrizeType.Overall, 1, 100m);

        // Assert
        setting.LeagueId.Should().Be(1);
        setting.PrizeType.Should().Be(PrizeType.Overall);
        setting.Rank.Should().Be(1);
        setting.PrizeAmount.Should().Be(100m);
    }

    [Fact]
    public void Create_ShouldSetPrizeType_WhenProvided()
    {
        // Act
        var setting = LeaguePrizeSetting.Create(1, PrizeType.Monthly, 1, 50m);

        // Assert
        setting.PrizeType.Should().Be(PrizeType.Monthly);
    }

    [Fact]
    public void Create_ShouldAllowZeroPrizeAmount()
    {
        // Act
        var setting = LeaguePrizeSetting.Create(1, PrizeType.Overall, 1, 0m);

        // Assert
        setting.PrizeAmount.Should().Be(0m);
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenLeagueIdIsZero()
    {
        // Act
        var act = () => LeaguePrizeSetting.Create(0, PrizeType.Overall, 1, 100m);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenLeagueIdIsNegative()
    {
        // Act
        var act = () => LeaguePrizeSetting.Create(-1, PrizeType.Overall, 1, 100m);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenRankIsZero()
    {
        // Act
        var act = () => LeaguePrizeSetting.Create(1, PrizeType.Overall, 0, 100m);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenRankIsNegative()
    {
        // Act
        var act = () => LeaguePrizeSetting.Create(1, PrizeType.Overall, -1, 100m);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenPrizeAmountIsNegative()
    {
        // Act
        var act = () => LeaguePrizeSetting.Create(1, PrizeType.Overall, 1, -1m);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
