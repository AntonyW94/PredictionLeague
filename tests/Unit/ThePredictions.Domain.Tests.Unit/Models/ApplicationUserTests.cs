using FluentAssertions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

public class ApplicationUserTests
{
    #region Create — Happy Path

    [Fact]
    public void Create_ShouldCreateUser_WhenValidParametersProvided()
    {
        // Act
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");

        // Assert
        user.FirstName.Should().Be("John");
        user.LastName.Should().Be("Doe");
        user.Email.Should().Be("john@example.com");
        user.UserName.Should().Be("john@example.com");
    }

    [Fact]
    public void Create_ShouldSetUserNameToEmail()
    {
        // Act
        var user = ApplicationUser.Create("John", "Doe", "test@example.com");

        // Assert
        user.UserName.Should().Be("test@example.com");
    }

    [Fact]
    public void Create_ShouldSetEmailToProvidedValue()
    {
        // Act
        var user = ApplicationUser.Create("John", "Doe", "test@example.com");

        // Assert
        user.Email.Should().Be("test@example.com");
    }

    #endregion

    #region Create — Validation

    [Fact]
    public void Create_ShouldThrowException_WhenFirstNameIsNull()
    {
        // Act
        var act = () => ApplicationUser.Create(null!, "Doe", "test@example.com");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenFirstNameIsEmpty()
    {
        // Act
        var act = () => ApplicationUser.Create("", "Doe", "test@example.com");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenFirstNameIsWhitespace()
    {
        // Act
        var act = () => ApplicationUser.Create(" ", "Doe", "test@example.com");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenLastNameIsNull()
    {
        // Act
        var act = () => ApplicationUser.Create("John", null!, "test@example.com");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenLastNameIsEmpty()
    {
        // Act
        var act = () => ApplicationUser.Create("John", "", "test@example.com");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenLastNameIsWhitespace()
    {
        // Act
        var act = () => ApplicationUser.Create("John", " ", "test@example.com");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenEmailIsNull()
    {
        // Act
        var act = () => ApplicationUser.Create("John", "Doe", null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenEmailIsEmpty()
    {
        // Act
        var act = () => ApplicationUser.Create("John", "Doe", "");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_ShouldThrowException_WhenEmailIsWhitespace()
    {
        // Act
        var act = () => ApplicationUser.Create("John", "Doe", " ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_ShouldUpdateProperties_WhenValid()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");

        // Act
        user.UpdateDetails("Jane", "Smith", "07700900000");

        // Assert
        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Smith");
        user.PhoneNumber.Should().Be("07700900000");
    }

    [Fact]
    public void UpdateDetails_ShouldAcceptNullPhoneNumber()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");

        // Act
        user.UpdateDetails("Jane", "Smith", null);

        // Assert
        user.PhoneNumber.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_ShouldSetPhoneNumber_WhenProvided()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");

        // Act
        user.UpdateDetails("John", "Doe", "07700900000");

        // Assert
        user.PhoneNumber.Should().Be("07700900000");
    }

    [Fact]
    public void UpdateDetails_ShouldNotChangeEmail_WhenUpdating()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");

        // Act
        user.UpdateDetails("Jane", "Smith", null);

        // Assert
        user.Email.Should().Be("john@example.com");
    }

    [Fact]
    public void UpdateDetails_ShouldNotChangeUserName_WhenUpdating()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");

        // Act
        user.UpdateDetails("Jane", "Smith", null);

        // Assert
        user.UserName.Should().Be("john@example.com");
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenFirstNameIsNull()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");

        // Act
        var act = () => user.UpdateDetails(null!, "Doe", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenFirstNameIsEmpty()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");

        // Act
        var act = () => user.UpdateDetails("", "Doe", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenFirstNameIsWhitespace()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");

        // Act
        var act = () => user.UpdateDetails(" ", "Doe", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenLastNameIsNull()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");

        // Act
        var act = () => user.UpdateDetails("John", null!, null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenLastNameIsEmpty()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");

        // Act
        var act = () => user.UpdateDetails("John", "", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDetails_ShouldThrowException_WhenLastNameIsWhitespace()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");

        // Act
        var act = () => user.UpdateDetails("John", " ", null);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region RecordRegistrationConsent

    [Fact]
    public void RecordRegistrationConsent_ShouldStampTermsAndMarketingTimestamps_WhenMarketingOptInChosen()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");
        var nowUtc = new DateTime(2026, 4, 28, 10, 0, 0, DateTimeKind.Utc);

        // Act
        user.RecordRegistrationConsent(marketingOptIn: true, nowUtc);

        // Assert
        user.TermsAcceptedAtUtc.Should().Be(nowUtc);
        user.MarketingOptInAtUtc.Should().Be(nowUtc);
    }

    [Fact]
    public void RecordRegistrationConsent_ShouldLeaveMarketingOptInNull_WhenMarketingNotChosen()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");
        var nowUtc = new DateTime(2026, 4, 28, 10, 0, 0, DateTimeKind.Utc);

        // Act
        user.RecordRegistrationConsent(marketingOptIn: false, nowUtc);

        // Assert
        user.TermsAcceptedAtUtc.Should().Be(nowUtc);
        user.MarketingOptInAtUtc.Should().BeNull();
    }

    #endregion

    #region SetMarketingOptIn

    [Fact]
    public void SetMarketingOptIn_ShouldStampMarketingTimestamp_WhenOptedIn()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");
        var nowUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        // Act
        user.SetMarketingOptIn(marketingOptIn: true, nowUtc);

        // Assert
        user.MarketingOptInAtUtc.Should().Be(nowUtc);
    }

    [Fact]
    public void SetMarketingOptIn_ShouldClearMarketingTimestamp_WhenOptedOut()
    {
        // Arrange
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");
        user.SetMarketingOptIn(marketingOptIn: true, new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc));

        // Act
        user.SetMarketingOptIn(marketingOptIn: false, new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));

        // Assert
        user.MarketingOptInAtUtc.Should().BeNull();
    }

    [Fact]
    public void SetMarketingOptIn_ShouldNotChangeTermsAcceptance()
    {
        // Arrange - a registered user whose terms were accepted at registration
        var user = ApplicationUser.Create("John", "Doe", "john@example.com");
        var registeredUtc = new DateTime(2026, 4, 28, 10, 0, 0, DateTimeKind.Utc);
        user.RecordRegistrationConsent(marketingOptIn: false, registeredUtc);

        // Act - later toggling marketing must leave the terms timestamp alone
        user.SetMarketingOptIn(marketingOptIn: true, new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc));

        // Assert
        user.TermsAcceptedAtUtc.Should().Be(registeredUtc);
    }

    #endregion
}
