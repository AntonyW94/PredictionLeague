using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;
using ThePredictions.Domain.Common.Exceptions;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class TournamentRoundMappingTests
{
    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreateMapping_WhenValidParametersProvided()
    {
        // Act
        var mapping = TournamentRoundMapping.Create(1, 1, "Group Stage - Matchday 1", "Group1", 24);

        // Assert
        mapping.SeasonId.Should().Be(1);
        mapping.RoundNumber.Should().Be(1);
        mapping.DisplayName.Should().Be("Group Stage - Matchday 1");
        mapping.Stages.Should().Be("Group1");
        mapping.ExpectedMatchCount.Should().Be(24);
    }

    [Fact]
    public void Create_ShouldCreateMapping_WhenMultipleStagesProvided()
    {
        // Act
        var mapping = TournamentRoundMapping.Create(1, 7, "Semi-finals, Final & Third Place", "SemiFinals|ThirdPlace|Final", 4);

        // Assert
        mapping.Stages.Should().Be("SemiFinals|ThirdPlace|Final");
        mapping.ExpectedMatchCount.Should().Be(4);
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrow_WhenSeasonIdIsZero()
    {
        // Act
        var act = () => TournamentRoundMapping.Create(0, 1, "Test", "Group1", 24);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenRoundNumberIsZero()
    {
        // Act
        var act = () => TournamentRoundMapping.Create(1, 0, "Test", "Group1", 24);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenDisplayNameIsEmpty()
    {
        // Act
        var act = () => TournamentRoundMapping.Create(1, 1, "", "Group1", 24);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenDisplayNameIsWhitespace()
    {
        // Act
        var act = () => TournamentRoundMapping.Create(1, 1, "   ", "Group1", 24);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenStagesIsEmpty()
    {
        // Act
        var act = () => TournamentRoundMapping.Create(1, 1, "Test", "", 24);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenExpectedMatchCountIsZero()
    {
        // Act
        var act = () => TournamentRoundMapping.Create(1, 1, "Test", "Group1", 0);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region GetStageList

    [Fact]
    public void GetStageList_ShouldReturnSingleStage_WhenOneStageProvided()
    {
        // Arrange
        var mapping = TournamentRoundMapping.Create(1, 1, "Test", "Group1", 24);

        // Act
        var stages = mapping.GetStageList();

        // Assert
        stages.Should().HaveCount(1);
        stages[0].Should().Be(TournamentStage.Group1);
    }

    [Fact]
    public void GetStageList_ShouldReturnMultipleStages_WhenPipeDelimited()
    {
        // Arrange
        var mapping = TournamentRoundMapping.Create(1, 7, "Test", "SemiFinals|ThirdPlace|Final", 4);

        // Act
        var stages = mapping.GetStageList();

        // Assert
        stages.Should().HaveCount(3);
        stages[0].Should().Be(TournamentStage.SemiFinals);
        stages[1].Should().Be(TournamentStage.ThirdPlace);
        stages[2].Should().Be(TournamentStage.Final);
    }

    [Fact]
    public void GetStageList_ShouldSkipInvalidValues_WhenMixedWithValid()
    {
        // Arrange
        var mapping = new TournamentRoundMapping(1, 1, 1, "Test", "Group1|InvalidStage|Final", 24);

        // Act
        var stages = mapping.GetStageList();

        // Assert
        stages.Should().HaveCount(2);
        stages[0].Should().Be(TournamentStage.Group1);
        stages[1].Should().Be(TournamentStage.Final);
    }

    [Fact]
    public void GetStageList_ShouldReturnEmpty_WhenStagesIsWhitespace()
    {
        // Arrange
        var mapping = new TournamentRoundMapping(1, 1, 1, "Test", "   ", 24);

        // Act
        var stages = mapping.GetStageList();

        // Assert
        stages.Should().BeEmpty();
    }

    #endregion

    #region GetPrimaryStage

    [Fact]
    public void GetPrimaryStage_ShouldReturnFirstStage_WhenMultipleStagesExist()
    {
        // Arrange
        var mapping = TournamentRoundMapping.Create(1, 7, "Test", "SemiFinals|ThirdPlace|Final", 4);

        // Act
        var primary = mapping.GetPrimaryStage();

        // Assert
        primary.Should().Be(TournamentStage.SemiFinals);
    }

    [Fact]
    public void GetPrimaryStage_ShouldReturnSingleStage_WhenOnlyOneExists()
    {
        // Arrange
        var mapping = TournamentRoundMapping.Create(1, 1, "Test", "QuarterFinals", 4);

        // Act
        var primary = mapping.GetPrimaryStage();

        // Assert
        primary.Should().Be(TournamentStage.QuarterFinals);
    }

    [Fact]
    public void GetPrimaryStage_ShouldThrow_WhenNoValidStages()
    {
        // Arrange
        var mapping = new TournamentRoundMapping(1, 1, 1, "Test", "   ", 24);

        // Act
        var act = () => mapping.GetPrimaryStage();

        // Assert
        act.Should().Throw<BusinessRuleViolationException>();
    }

    #endregion

    #region Constructor (Dapper hydration)

    [Fact]
    public void Constructor_ShouldSetAllProperties_WhenCalledDirectly()
    {
        // Act
        var mapping = new TournamentRoundMapping(42, 5, 3, "Quarter-finals", "QuarterFinals", 4);

        // Assert
        mapping.Id.Should().Be(42);
        mapping.SeasonId.Should().Be(5);
        mapping.RoundNumber.Should().Be(3);
        mapping.DisplayName.Should().Be("Quarter-finals");
        mapping.Stages.Should().Be("QuarterFinals");
        mapping.ExpectedMatchCount.Should().Be(4);
    }

    #endregion
}
