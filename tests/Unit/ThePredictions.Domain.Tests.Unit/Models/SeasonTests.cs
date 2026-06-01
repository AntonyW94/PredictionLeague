using FluentAssertions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class SeasonTests
{
    private static readonly DateTime ValidStart = new(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ValidEnd = new(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);

    private static Season CreateSeasonViaFactory(
        string name = "2025/26 Season",
        DateTime? startDateUtc = null,
        DateTime? endDateUtc = null,
        bool isActive = true,
        int numberOfRounds = 38,
        int competitionId = 1,
        decimal? passStandardPrice = null,
        decimal? passPremiumPrice = null)
    {
        return Season.Create(
            name,
            startDateUtc ?? ValidStart,
            endDateUtc ?? ValidEnd,
            isActive,
            numberOfRounds,
            competitionId,
            passStandardPrice,
            passPremiumPrice);
    }

    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreateSeason_WhenValidParametersProvided()
    {
        // Act
        var season = CreateSeasonViaFactory();

        // Assert
        season.Name.Should().Be("2025/26 Season");
        season.StartDateUtc.Should().Be(ValidStart);
        season.EndDateUtc.Should().Be(ValidEnd);
        season.IsActive.Should().BeTrue();
        season.NumberOfRounds.Should().Be(38);
    }

    [Fact]
    public void Create_ShouldSetAllProperties_WhenCreated()
    {
        // Act
        var season = CreateSeasonViaFactory(competitionId: 7);

        // Assert
        season.Name.Should().Be("2025/26 Season");
        season.StartDateUtc.Should().Be(ValidStart);
        season.EndDateUtc.Should().Be(ValidEnd);
        season.IsActive.Should().BeTrue();
        season.NumberOfRounds.Should().Be(38);
        season.CompetitionId.Should().Be(7);
    }

    [Fact]
    public void Create_ShouldSetIsActiveTrue_WhenTrue()
    {
        // Act
        var season = CreateSeasonViaFactory(isActive: true);

        // Assert
        season.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldSetIsActiveFalse_WhenFalse()
    {
        // Act
        var season = CreateSeasonViaFactory(isActive: false);

        // Assert
        season.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldSetCompetitionId_WhenProvided()
    {
        // Act
        var season = CreateSeasonViaFactory(competitionId: 42);

        // Assert
        season.CompetitionId.Should().Be(42);
    }

    #endregion

    #region Create — Name Validation

    [Fact]
    public void Create_ShouldThrowException_WhenNameIsNull()
    {
        // Act
        var act = () => CreateSeasonViaFactory(name: null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenNameIsEmpty()
    {
        // Act
        var act = () => CreateSeasonViaFactory(name: "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenNameIsWhitespace()
    {
        // Act
        var act = () => CreateSeasonViaFactory(name: " ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Create — Date Validation

    [Fact]
    public void Create_ShouldThrowException_WhenStartDateIsDefault()
    {
        // Act
        var act = () => CreateSeasonViaFactory(startDateUtc: DateTime.MinValue);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenEndDateIsDefault()
    {
        // Act
        var act = () => CreateSeasonViaFactory(endDateUtc: DateTime.MinValue);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenEndDateIsBeforeStartDate()
    {
        // Act
        var act = () => CreateSeasonViaFactory(
            startDateUtc: ValidStart,
            endDateUtc: ValidStart.AddDays(-1));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenEndDateEqualsStartDate()
    {
        // Act
        var act = () => CreateSeasonViaFactory(
            startDateUtc: ValidStart,
            endDateUtc: ValidStart);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Create — Duration Validation

    [Fact]
    public void Create_ShouldThrowException_WhenDurationExceedsTenMonths()
    {
        // Guard uses: endDateUtc > startDateUtc.AddMonths(10)
        var start = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(10).AddDays(1);

        // Act
        var act = () => Season.Create("Test", start, end, true, 38, 1, null, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldAcceptDurationOfExactlyTenMonths()
    {
        // Guard uses strict >, so exactly AddMonths(10) should pass
        var start = new DateTime(2025, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(10);

        // Act
        var act = () => Season.Create("Test", start, end, true, 38, 1, null, null);

        // Assert
        act.Should().NotThrow();
    }

    #endregion

    #region Create — NumberOfRounds Validation

    [Fact]
    public void Create_ShouldThrowException_WhenNumberOfRoundsIsZero()
    {
        // Act
        var act = () => CreateSeasonViaFactory(numberOfRounds: 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenNumberOfRoundsIsNegative()
    {
        // Act
        var act = () => CreateSeasonViaFactory(numberOfRounds: -1);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenNumberOfRoundsExceeds52()
    {
        // Act
        var act = () => CreateSeasonViaFactory(numberOfRounds: 53);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_ShouldAcceptNumberOfRoundsAtLowerBoundary()
    {
        // Act
        var season = CreateSeasonViaFactory(numberOfRounds: 1);

        // Assert
        season.NumberOfRounds.Should().Be(1);
    }

    [Fact]
    public void Create_ShouldAcceptNumberOfRoundsAtUpperBoundary()
    {
        // Act
        var season = CreateSeasonViaFactory(numberOfRounds: 52);

        // Assert
        season.NumberOfRounds.Should().Be(52);
    }

    #endregion

    #region Create — CompetitionId Validation

    [Fact]
    public void Create_ShouldThrowException_WhenCompetitionIdIsZero()
    {
        // Act
        var act = () => CreateSeasonViaFactory(competitionId: 0);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenCompetitionIdIsNegative()
    {
        // Act
        var act = () => CreateSeasonViaFactory(competitionId: -1);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Pricing & RequiresPayment

    [Fact]
    public void Create_ShouldBeFreeSeason_WhenNoPrices()
    {
        // Act
        var season = CreateSeasonViaFactory();

        // Assert
        season.PassStandardPrice.Should().BeNull();
        season.PassPremiumPrice.Should().BeNull();
        season.RequiresPayment.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldRequirePayment_WhenPricesSet()
    {
        // Act
        var season = CreateSeasonViaFactory(passStandardPrice: 10m, passPremiumPrice: 15m);

        // Assert
        season.PassStandardPrice.Should().Be(10m);
        season.PassPremiumPrice.Should().Be(15m);
        season.RequiresPayment.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldAcceptEqualEntryAndPassPremiumPrice()
    {
        // Act
        var act = () => CreateSeasonViaFactory(passStandardPrice: 10m, passPremiumPrice: 10m);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_ShouldRequirePayment_WhenStandardPriceSetWithoutPremium()
    {
        // Act
        var season = CreateSeasonViaFactory(passStandardPrice: 10m, passPremiumPrice: null);

        // Assert
        season.PassStandardPrice.Should().Be(10m);
        season.PassPremiumPrice.Should().BeNull();
        season.RequiresPayment.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldThrow_WhenPassPremiumPriceSetButPassStandardPriceNull()
    {
        // Act
        var act = () => CreateSeasonViaFactory(passStandardPrice: null, passPremiumPrice: 15m);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenPassStandardPriceZero()
    {
        // Act
        var act = () => CreateSeasonViaFactory(passStandardPrice: 0m, passPremiumPrice: 15m);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrow_WhenPassPremiumPriceBelowPassStandardPrice()
    {
        // Act
        var act = () => CreateSeasonViaFactory(passStandardPrice: 15m, passPremiumPrice: 10m);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_ShouldUpdateAllProperties_WhenValid()
    {
        // Arrange
        var season = CreateSeasonViaFactory();
        var newStart = ValidStart.AddMonths(1);
        var newEnd = ValidEnd.AddMonths(1);

        // Act
        season.UpdateDetails("Updated Season", newStart, newEnd, false, 20, 99, 10m, 15m);

        // Assert
        season.Name.Should().Be("Updated Season");
        season.StartDateUtc.Should().Be(newStart);
        season.EndDateUtc.Should().Be(newEnd);
        season.IsActive.Should().BeFalse();
        season.NumberOfRounds.Should().Be(20);
        season.CompetitionId.Should().Be(99);
        season.PassStandardPrice.Should().Be(10m);
        season.PassPremiumPrice.Should().Be(15m);
        season.RequiresPayment.Should().BeTrue();
    }

    [Fact]
    public void UpdateDetails_ShouldNotChangeId_WhenUpdating()
    {
        // Arrange — use public constructor so we can set Id
        var season = new Season(id: 42, name: "Test", startDateUtc: ValidStart, endDateUtc: ValidEnd,
            isActive: true, numberOfRounds: 38, competitionId: 1, passStandardPrice: null, passPremiumPrice: null);

        // Act
        season.UpdateDetails("Updated", ValidStart, ValidEnd, false, 20, 1, null, null);

        // Assert
        season.Id.Should().Be(42);
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenNameIsNull()
    {
        // Arrange
        var season = CreateSeasonViaFactory();

        // Act
        var act = () => season.UpdateDetails(null!, ValidStart, ValidEnd, true, 38, 1, null, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenNameIsEmpty()
    {
        // Arrange
        var season = CreateSeasonViaFactory();

        // Act
        var act = () => season.UpdateDetails("", ValidStart, ValidEnd, true, 38, 1, null, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenEndDateBeforeStartDate()
    {
        // Arrange
        var season = CreateSeasonViaFactory();

        // Act
        var act = () => season.UpdateDetails("Test", ValidStart, ValidStart.AddDays(-1), true, 38, 1, null, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenEndDateEqualsStartDate()
    {
        // Arrange
        var season = CreateSeasonViaFactory();

        // Act
        var act = () => season.UpdateDetails("Test", ValidStart, ValidStart, true, 38, 1, null, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenDurationExceedsTenMonths()
    {
        // Arrange
        var season = CreateSeasonViaFactory();
        var farEnd = ValidStart.AddMonths(10).AddDays(1);

        // Act
        var act = () => season.UpdateDetails("Test", ValidStart, farEnd, true, 38, 1, null, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenStartDateIsDefault()
    {
        // Arrange
        var season = CreateSeasonViaFactory();

        // Act
        var act = () => season.UpdateDetails("Test", default, ValidEnd, true, 38, 1, null, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenEndDateIsDefault()
    {
        // Arrange
        var season = CreateSeasonViaFactory();

        // Act
        var act = () => season.UpdateDetails("Test", ValidStart, default, true, 38, 1, null, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenNumberOfRoundsIsZero()
    {
        // Arrange
        var season = CreateSeasonViaFactory();

        // Act
        var act = () => season.UpdateDetails("Test", ValidStart, ValidEnd, true, 0, 1, null, null);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenNumberOfRoundsExceeds52()
    {
        // Arrange
        var season = CreateSeasonViaFactory();

        // Act
        var act = () => season.UpdateDetails("Test", ValidStart, ValidEnd, true, 53, 1, null, null);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenCompetitionIdIsZero()
    {
        // Arrange
        var season = CreateSeasonViaFactory();

        // Act
        var act = () => season.UpdateDetails("Test", ValidStart, ValidEnd, true, 38, 0, null, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldAllowStandardOnlyPaidSeason_WhenPremiumNull()
    {
        // Arrange
        var season = CreateSeasonViaFactory();

        // Act
        season.UpdateDetails("Test", ValidStart, ValidEnd, true, 38, 1, 10m, null);

        // Assert
        season.PassStandardPrice.Should().Be(10m);
        season.PassPremiumPrice.Should().BeNull();
        season.RequiresPayment.Should().BeTrue();
    }

    #endregion

    #region SetIsActive

    [Fact]
    public void SetIsActive_ShouldSetToTrue_WhenCalledWithTrue()
    {
        // Arrange
        var season = CreateSeasonViaFactory(isActive: false);

        // Act
        season.SetIsActive(true);

        // Assert
        season.IsActive.Should().BeTrue();
    }

    [Fact]
    public void SetIsActive_ShouldSetToFalse_WhenCalledWithFalse()
    {
        // Arrange
        var season = CreateSeasonViaFactory(isActive: true);

        // Act
        season.SetIsActive(false);

        // Assert
        season.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SetIsActive_ShouldBeIdempotent_WhenCalledWithSameValue()
    {
        // Arrange
        var season = CreateSeasonViaFactory(isActive: true);

        // Act
        season.SetIsActive(true);
        season.SetIsActive(true);

        // Assert
        season.IsActive.Should().BeTrue();
    }

    #endregion
}
