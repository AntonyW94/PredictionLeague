using FluentAssertions;
using PredictionLeague.Domain.Models;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class TeamTests
{
    private static Team CreateTeamViaFactory(
        string name = "Manchester United",
        string shortName = "Man Utd",
        string logoUrl = "https://example.com/mufc.png",
        string abbreviation = "MUN",
        int? apiTeamId = null)
    {
        return Team.Create(name, shortName, logoUrl, abbreviation, apiTeamId);
    }

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreateTeam_WhenValidParametersProvided()
    {
        // Act
        var team = CreateTeamViaFactory();

        // Assert
        team.Name.Should().Be("Manchester United");
        team.ShortName.Should().Be("Man Utd");
        team.LogoUrl.Should().Be("https://example.com/mufc.png");
        team.Abbreviation.Should().Be("MUN");
    }

    [Fact]
    public void Create_ShouldCreateTeam_WhenAbbreviationIsExactlyThreeCharacters()
    {
        // Act
        var team = CreateTeamViaFactory(abbreviation: "MUN");

        // Assert
        team.Abbreviation.Should().Be("MUN");
    }

    [Fact]
    public void Create_ShouldAcceptNullApiTeamId()
    {
        // Act
        var act = () => CreateTeamViaFactory(apiTeamId: null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_ShouldSetApiTeamId_WhenProvided()
    {
        // Act
        var team = CreateTeamViaFactory(apiTeamId: 42);

        // Assert
        team.ApiTeamId.Should().Be(42);
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenNameIsNull()
    {
        // Act
        var act = () => CreateTeamViaFactory(name: null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenNameIsEmpty()
    {
        // Act
        var act = () => CreateTeamViaFactory(name: "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenShortNameIsNull()
    {
        // Act
        var act = () => CreateTeamViaFactory(shortName: null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenShortNameIsEmpty()
    {
        // Act
        var act = () => CreateTeamViaFactory(shortName: "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenLogoUrlIsNull()
    {
        // Act
        var act = () => CreateTeamViaFactory(logoUrl: null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenLogoUrlIsEmpty()
    {
        // Act
        var act = () => CreateTeamViaFactory(logoUrl: "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenAbbreviationIsNull()
    {
        // Act
        var act = () => CreateTeamViaFactory(abbreviation: null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenAbbreviationIsEmpty()
    {
        // Act
        var act = () => CreateTeamViaFactory(abbreviation: "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenAbbreviationIsTooShort()
    {
        // Act
        var act = () => CreateTeamViaFactory(abbreviation: "AB");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenAbbreviationIsTooLong()
    {
        // Act
        var act = () => CreateTeamViaFactory(abbreviation: "ABCD");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_ShouldUpdateAllProperties_WhenValid()
    {
        // Arrange
        var team = CreateTeamViaFactory();

        // Act
        team.UpdateDetails("Liverpool", "Liverpool", "https://example.com/lfc.png", "LIV", 99);

        // Assert
        team.Name.Should().Be("Liverpool");
        team.ShortName.Should().Be("Liverpool");
        team.LogoUrl.Should().Be("https://example.com/lfc.png");
        team.Abbreviation.Should().Be("LIV");
        team.ApiTeamId.Should().Be(99);
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenNameIsNull()
    {
        // Arrange
        var team = CreateTeamViaFactory();

        // Act
        var act = () => team.UpdateDetails(null!, "Short", "https://logo.png", "ABC", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenNameIsEmpty()
    {
        // Arrange
        var team = CreateTeamViaFactory();

        // Act
        var act = () => team.UpdateDetails("", "Short", "https://logo.png", "ABC", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenShortNameIsEmpty()
    {
        // Arrange
        var team = CreateTeamViaFactory();

        // Act
        var act = () => team.UpdateDetails("Name", "", "https://logo.png", "ABC", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenLogoUrlIsEmpty()
    {
        // Arrange
        var team = CreateTeamViaFactory();

        // Act
        var act = () => team.UpdateDetails("Name", "Short", "", "ABC", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenAbbreviationIsTooShort()
    {
        // Arrange
        var team = CreateTeamViaFactory();

        // Act
        var act = () => team.UpdateDetails("Name", "Short", "https://logo.png", "AB", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenAbbreviationIsTooLong()
    {
        // Arrange
        var team = CreateTeamViaFactory();

        // Act
        var act = () => team.UpdateDetails("Name", "Short", "https://logo.png", "ABCD", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldAcceptNullApiTeamId()
    {
        // Arrange
        var team = CreateTeamViaFactory(apiTeamId: 42);

        // Act
        team.UpdateDetails("Name", "Short", "https://logo.png", "ABC", null);

        // Assert
        team.ApiTeamId.Should().BeNull();
    }

    #endregion
}
