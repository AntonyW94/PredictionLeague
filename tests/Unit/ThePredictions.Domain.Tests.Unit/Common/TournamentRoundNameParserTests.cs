using FluentAssertions;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Common;

public class TournamentRoundNameParserTests
{
    #region TryParseStage — Group Stage

    [Theory]
    [InlineData("Group Stage - 1", TournamentStage.Group1)]
    [InlineData("Group Stage - 2", TournamentStage.Group2)]
    [InlineData("Group Stage - 3", TournamentStage.Group3)]
    public void TryParseStage_ShouldParseGroupStage_WhenValidMatchdayProvided(string apiRoundName, TournamentStage expectedStage)
    {
        // Act
        var result = TournamentRoundNameParser.TryParseStage(apiRoundName, out var stage);

        // Assert
        result.Should().BeTrue();
        stage.Should().Be(expectedStage);
    }

    [Fact]
    public void TryParseStage_ShouldReturnFalse_WhenGroupStageMatchdayTooHigh()
    {
        // Act
        var result = TournamentRoundNameParser.TryParseStage("Group Stage - 4", out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryParseStage_ShouldReturnFalse_WhenGroupStageMatchdayIsZero()
    {
        // Act
        var result = TournamentRoundNameParser.TryParseStage("Group Stage - 0", out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryParseStage_ShouldReturnFalse_WhenGroupStageMatchdayNotNumeric()
    {
        // Act
        var result = TournamentRoundNameParser.TryParseStage("Group Stage - X", out _);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region TryParseStage — Knockout Stages

    [Theory]
    [InlineData("Round of 32", TournamentStage.RoundOf32)]
    [InlineData("Round of 16", TournamentStage.RoundOf16)]
    [InlineData("Quarter-finals", TournamentStage.QuarterFinals)]
    [InlineData("Semi-finals", TournamentStage.SemiFinals)]
    [InlineData("3rd Place Final", TournamentStage.ThirdPlace)]
    [InlineData("Final", TournamentStage.Final)]
    public void TryParseStage_ShouldParseKnockoutStage_WhenValidNameProvided(string apiRoundName, TournamentStage expectedStage)
    {
        // Act
        var result = TournamentRoundNameParser.TryParseStage(apiRoundName, out var stage);

        // Assert
        result.Should().BeTrue();
        stage.Should().Be(expectedStage);
    }

    [Fact]
    public void TryParseStage_ShouldBeCaseInsensitive_ForKnockoutStages()
    {
        // Act
        var result = TournamentRoundNameParser.TryParseStage("quarter-finals", out var stage);

        // Assert
        result.Should().BeTrue();
        stage.Should().Be(TournamentStage.QuarterFinals);
    }

    #endregion

    #region TryParseStage — Invalid Input

    [Fact]
    public void TryParseStage_ShouldReturnFalse_WhenNameIsNull()
    {
        // Act
        var result = TournamentRoundNameParser.TryParseStage(null!, out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryParseStage_ShouldReturnFalse_WhenNameIsEmpty()
    {
        // Act
        var result = TournamentRoundNameParser.TryParseStage("", out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryParseStage_ShouldReturnFalse_WhenNameIsWhitespace()
    {
        // Act
        var result = TournamentRoundNameParser.TryParseStage("   ", out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TryParseStage_ShouldReturnFalse_WhenNameIsUnrecognised()
    {
        // Act
        var result = TournamentRoundNameParser.TryParseStage("Regular Season - 1", out _);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CalculateExpectedMatchCount

    [Theory]
    [InlineData(TournamentStage.Group1, 48, 24)]
    [InlineData(TournamentStage.Group2, 48, 24)]
    [InlineData(TournamentStage.Group3, 48, 24)]
    [InlineData(TournamentStage.Group1, 32, 16)]
    public void CalculateExpectedMatchCount_ShouldReturnHalfTeams_ForGroupStages(TournamentStage stage, int totalTeams, int expectedCount)
    {
        // Act
        var count = TournamentRoundNameParser.CalculateExpectedMatchCount(stage, totalTeams);

        // Assert
        count.Should().Be(expectedCount);
    }

    [Theory]
    [InlineData(TournamentStage.RoundOf32, 16)]
    [InlineData(TournamentStage.RoundOf16, 8)]
    [InlineData(TournamentStage.QuarterFinals, 4)]
    [InlineData(TournamentStage.SemiFinals, 2)]
    [InlineData(TournamentStage.ThirdPlace, 1)]
    [InlineData(TournamentStage.Final, 1)]
    public void CalculateExpectedMatchCount_ShouldReturnFixedCount_ForKnockoutStages(TournamentStage stage, int expectedCount)
    {
        // Act
        var count = TournamentRoundNameParser.CalculateExpectedMatchCount(stage, 48);

        // Assert
        count.Should().Be(expectedCount);
    }

    #endregion

    #region GetDefaultDisplayName

    [Theory]
    [InlineData(TournamentStage.Group1, "Group Stage - Matchday 1")]
    [InlineData(TournamentStage.Group2, "Group Stage - Matchday 2")]
    [InlineData(TournamentStage.Group3, "Group Stage - Matchday 3")]
    [InlineData(TournamentStage.RoundOf32, "Round of 32")]
    [InlineData(TournamentStage.RoundOf16, "Round of 16")]
    [InlineData(TournamentStage.QuarterFinals, "Quarter-finals")]
    [InlineData(TournamentStage.SemiFinals, "Semi-finals")]
    [InlineData(TournamentStage.ThirdPlace, "Third Place Playoff")]
    [InlineData(TournamentStage.Final, "Final")]
    public void GetDefaultDisplayName_ShouldReturnCorrectName_ForEachStage(TournamentStage stage, string expectedName)
    {
        // Act
        var name = TournamentRoundNameParser.GetDefaultDisplayName(stage);

        // Assert
        name.Should().Be(expectedName);
    }

    #endregion

    #region GetCombinedDisplayName

    [Fact]
    public void GetCombinedDisplayName_ShouldReturnSingleName_WhenOneStage()
    {
        // Act
        var name = TournamentRoundNameParser.GetCombinedDisplayName([TournamentStage.QuarterFinals]);

        // Assert
        name.Should().Be("Quarter-finals");
    }

    [Fact]
    public void GetCombinedDisplayName_ShouldJoinWithAmpersand_WhenTwoStages()
    {
        // Act
        var name = TournamentRoundNameParser.GetCombinedDisplayName([TournamentStage.SemiFinals, TournamentStage.Final]);

        // Assert
        name.Should().Be("Semi-finals & Final");
    }

    [Fact]
    public void GetCombinedDisplayName_ShouldJoinWithCommaAndAmpersand_WhenThreeStages()
    {
        // Act
        var name = TournamentRoundNameParser.GetCombinedDisplayName([TournamentStage.SemiFinals, TournamentStage.ThirdPlace, TournamentStage.Final]);

        // Assert
        name.Should().Be("Semi-finals, Third Place Playoff & Final");
    }

    [Fact]
    public void GetCombinedDisplayName_ShouldReturnEmpty_WhenNoStages()
    {
        // Act
        var name = TournamentRoundNameParser.GetCombinedDisplayName([]);

        // Assert
        name.Should().BeEmpty();
    }

    #endregion

    #region CalculateExpectedMatchCount — Default

    [Fact]
    public void CalculateExpectedMatchCount_ShouldReturnZero_ForUnknownStage()
    {
        // Act
        var count = TournamentRoundNameParser.CalculateExpectedMatchCount((TournamentStage)999, 48);

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region GetDefaultDisplayName — Default

    [Fact]
    public void GetDefaultDisplayName_ShouldReturnEnumString_ForUnknownStage()
    {
        // Act
        var name = TournamentRoundNameParser.GetDefaultDisplayName((TournamentStage)999);

        // Assert
        name.Should().Be("999");
    }

    #endregion

    #region GetPlaceholderMatchName

    [Theory]
    [InlineData(TournamentStage.Group1, 1, "Group Match 1")]
    [InlineData(TournamentStage.Group2, 5, "Group Match 5")]
    [InlineData(TournamentStage.Group3, 10, "Group Match 10")]
    [InlineData(TournamentStage.RoundOf32, 5, "R32 Match 5")]
    [InlineData(TournamentStage.RoundOf16, 3, "R16 Match 3")]
    [InlineData(TournamentStage.QuarterFinals, 2, "QF 2")]
    [InlineData(TournamentStage.SemiFinals, 1, "Semi-final 1")]
    [InlineData(TournamentStage.ThirdPlace, 1, "Third Place Playoff")]
    [InlineData(TournamentStage.Final, 1, "Final")]
    public void GetPlaceholderMatchName_ShouldReturnCorrectName_ForEachStage(TournamentStage stage, int matchNumber, string expectedName)
    {
        // Act
        var name = TournamentRoundNameParser.GetPlaceholderMatchName(stage, matchNumber);

        // Assert
        name.Should().Be(expectedName);
    }

    [Fact]
    public void GetPlaceholderMatchName_ShouldReturnGenericName_ForUnknownStage()
    {
        // Act
        var name = TournamentRoundNameParser.GetPlaceholderMatchName((TournamentStage)999, 1);

        // Assert
        name.Should().Be("Match 1");
    }

    #endregion

    #region IsKnockoutStage

    [Theory]
    [InlineData(TournamentStage.RoundOf32, true)]
    [InlineData(TournamentStage.RoundOf16, true)]
    [InlineData(TournamentStage.QuarterFinals, true)]
    [InlineData(TournamentStage.SemiFinals, true)]
    [InlineData(TournamentStage.ThirdPlace, true)]
    [InlineData(TournamentStage.Final, true)]
    [InlineData(TournamentStage.Group1, false)]
    [InlineData(TournamentStage.Group2, false)]
    [InlineData(TournamentStage.Group3, false)]
    public void IsKnockoutStage_ShouldReturnExpectedResult_WhenStageProvided(TournamentStage stage, bool expectedResult)
    {
        // Act
        var result = TournamentRoundNameParser.IsKnockoutStage(stage);

        // Assert
        result.Should().Be(expectedResult);
    }

    #endregion

    #region GetPredictionBatch

    [Theory]
    [InlineData(TournamentStage.Group1)]
    [InlineData(TournamentStage.Group2)]
    [InlineData(TournamentStage.Group3)]
    [InlineData(TournamentStage.RoundOf32)]
    [InlineData(TournamentStage.RoundOf16)]
    [InlineData(TournamentStage.QuarterFinals)]
    [InlineData(TournamentStage.SemiFinals)]
    [InlineData(TournamentStage.ThirdPlace)]
    public void GetPredictionBatch_ShouldReturnOwnStageOrdinal_WhenStageIsNotTheFinal(TournamentStage stage)
    {
        // Act
        var result = TournamentRoundNameParser.GetPredictionBatch(stage);

        // Assert
        result.Should().Be((int)stage);
    }

    [Fact]
    public void GetPredictionBatch_ShouldShareTheThirdPlaceBatch_WhenStageIsTheFinal()
    {
        // Act
        var finalBatch = TournamentRoundNameParser.GetPredictionBatch(TournamentStage.Final);

        // Assert - the final and third-place playoff are both decided by the semi-finals, so they batch together
        finalBatch.Should().Be(TournamentRoundNameParser.GetPredictionBatch(TournamentStage.ThirdPlace));
    }

    [Fact]
    public void GetPredictionBatch_ShouldOrderBatchesByDepth_WhenComparingConsecutiveStages()
    {
        // Assert - later tournament stages produce higher (or equal, for the terminal pair) batch values
        TournamentRoundNameParser.GetPredictionBatch(TournamentStage.QuarterFinals)
            .Should().BeLessThan(TournamentRoundNameParser.GetPredictionBatch(TournamentStage.SemiFinals));

        TournamentRoundNameParser.GetPredictionBatch(TournamentStage.SemiFinals)
            .Should().BeLessThan(TournamentRoundNameParser.GetPredictionBatch(TournamentStage.Final));
    }

    #endregion
}
