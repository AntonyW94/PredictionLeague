using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class CompetitionTests
{
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc));

    private Competition CreateViaFactory(
        string code = "EPL",
        string name = "Premier League",
        CompetitionType type = CompetitionType.League,
        string? logoUrl = "https://cdn.example.com/epl.png",
        int? apiLeagueId = 39)
    {
        return Competition.Create(code, name, type, logoUrl, apiLeagueId, _dateTimeProvider);
    }

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldSetAllProperties_WhenValid()
    {
        // Act
        var competition = CreateViaFactory();

        // Assert
        competition.Code.Should().Be("EPL");
        competition.Name.Should().Be("Premier League");
        competition.Type.Should().Be(CompetitionType.League);
        competition.LogoUrl.Should().Be("https://cdn.example.com/epl.png");
        competition.ApiLeagueId.Should().Be(39);
        competition.CreatedAtUtc.Should().Be(_dateTimeProvider.UtcNow);
    }

    [Fact]
    public void Create_ShouldAcceptNullLogoUrl()
    {
        // Act
        var competition = CreateViaFactory(logoUrl: null);

        // Assert
        competition.LogoUrl.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldAcceptNullApiLeagueId()
    {
        // Act
        var competition = CreateViaFactory(apiLeagueId: null);

        // Assert
        competition.ApiLeagueId.Should().BeNull();
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenCodeIsNull()
    {
        // Act
        var act = () => CreateViaFactory(code: null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenCodeIsWhitespace()
    {
        // Act
        var act = () => CreateViaFactory(code: " ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenNameIsNull()
    {
        // Act
        var act = () => CreateViaFactory(name: null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenNameIsWhitespace()
    {
        // Act
        var act = () => CreateViaFactory(name: " ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region IsTournament

    [Fact]
    public void IsTournament_ShouldReturnTrue_WhenTypeIsTournament()
    {
        // Act
        var competition = CreateViaFactory(type: CompetitionType.Tournament);

        // Assert
        competition.IsTournament.Should().BeTrue();
    }

    [Fact]
    public void IsTournament_ShouldReturnFalse_WhenTypeIsLeague()
    {
        // Act
        var competition = CreateViaFactory(type: CompetitionType.League);

        // Assert
        competition.IsTournament.Should().BeFalse();
    }

    #endregion

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_ShouldUpdateAllEditableFields_WhenValid()
    {
        // Arrange
        var competition = CreateViaFactory();

        // Act
        competition.UpdateDetails("WORLD_CUP", "World Cup", CompetitionType.Tournament, "https://cdn.example.com/wc.png", 1);

        // Assert
        competition.Code.Should().Be("WORLD_CUP");
        competition.Name.Should().Be("World Cup");
        competition.Type.Should().Be(CompetitionType.Tournament);
        competition.LogoUrl.Should().Be("https://cdn.example.com/wc.png");
        competition.ApiLeagueId.Should().Be(1);
    }

    [Fact]
    public void UpdateDetails_ShouldClearApiLeagueId_WhenNull()
    {
        // Arrange
        var competition = CreateViaFactory(apiLeagueId: 39);

        // Act
        competition.UpdateDetails("EPL", "Premier League", CompetitionType.League, null, null);

        // Assert
        competition.ApiLeagueId.Should().BeNull();
        competition.LogoUrl.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_ShouldNotChangeIdOrCreatedAt_WhenUpdating()
    {
        // Arrange — public constructor so we can set Id + CreatedAtUtc
        var createdAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var competition = new Competition(id: 5, code: "EPL", name: "Premier League", type: CompetitionType.League, logoUrl: null, apiLeagueId: 39, createdAtUtc: createdAt);

        // Act
        competition.UpdateDetails("EPL", "English Premier League", CompetitionType.League, null, 39);

        // Assert
        competition.Id.Should().Be(5);
        competition.CreatedAtUtc.Should().Be(createdAt);
        competition.Name.Should().Be("English Premier League");
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenCodeIsBlank()
    {
        // Arrange
        var competition = CreateViaFactory();

        // Act
        var act = () => competition.UpdateDetails(" ", "Premier League", CompetitionType.League, null, 39);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenNameIsBlank()
    {
        // Arrange
        var competition = CreateViaFactory();

        // Act
        var act = () => competition.UpdateDetails("EPL", " ", CompetitionType.League, null, 39);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion
}
