using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Account.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Account.Queries;

/// <summary>
/// A player's own account screens: their details, and the bank details they would be paid into.
/// </summary>
public class AccountQueryHandlerTests
{
    private const string UserId = "user-me";

    private static readonly DateTime OptedInAt = new(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly IAccountProfileQuery _profileQuery = Substitute.For<IAccountProfileQuery>();
    private readonly IMyPayoutDetailsQuery _payoutQuery = Substitute.For<IMyPayoutDetailsQuery>();
    private readonly IFieldEncryptionService _encryption = Substitute.For<IFieldEncryptionService>();

    public AccountQueryHandlerTests()
    {
        // The encryption service is a pass-through here: what matters is which values reach it, not what it does to them.
        _encryption.Decrypt(Arg.Any<string?>()).Returns(call => call.Arg<string?>());

        _payoutQuery.GetPayingAdministratorsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    #region Their own details

    [Fact]
    public async Task GetUser_ShouldReportNotFound_WhenThereIsNoSuchAccount()
    {
        // Arrange
        _profileQuery.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((AccountProfileRow?)null);

        // Act
        var act = () => new GetUserQueryHandler(_profileQuery).Handle(new GetUserQuery(UserId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task GetUser_ShouldReportTheirDetails()
    {
        // Arrange
        GivenProfile(new AccountProfileRow("Ada", "Lovelace", "ada@example.com", "07700900000", "dark", OptedInAt));

        // Act
        var user = await new GetUserQueryHandler(_profileQuery).Handle(new GetUserQuery(UserId), CancellationToken.None);

        // Assert
        user.FirstName.Should().Be("Ada");
        user.LastName.Should().Be("Lovelace");
        user.Email.Should().Be("ada@example.com");
        user.PhoneNumber.Should().Be("07700900000");
        user.PreferredTheme.Should().Be("dark");
    }

    [Fact]
    public async Task GetUser_ShouldTreatARecordedOptInDateAsConsent()
    {
        // Consent is stored as the moment it was given, so that it can be evidenced later. The screen only needs the answer.
        GivenProfile(new AccountProfileRow("Ada", "Lovelace", "ada@example.com", null, "light", OptedInAt));

        // Act
        var user = await new GetUserQueryHandler(_profileQuery).Handle(new GetUserQuery(UserId), CancellationToken.None);

        // Assert
        user.MarketingOptIn.Should().BeTrue();
    }

    [Fact]
    public async Task GetUser_ShouldTreatNoOptInDateAsNoConsent()
    {
        // Arrange
        GivenProfile(new AccountProfileRow("Ada", "Lovelace", "ada@example.com", null, "light", null));

        // Act
        var user = await new GetUserQueryHandler(_profileQuery).Handle(new GetUserQuery(UserId), CancellationToken.None);

        // Assert
        user.MarketingOptIn.Should().BeFalse();
    }

    #endregion

    #region Their bank details

    [Fact]
    public async Task GetMyPayoutDetails_ShouldReportNoDetails_WhenNoneHaveBeenSaved()
    {
        // Arrange
        GivenDetails(null);

        // Act
        var details = await HandlePayoutsAsync();

        // Assert
        details.HasDetails.Should().BeFalse();
        details.AccountName.Should().BeNull();
    }

    [Fact]
    public async Task GetMyPayoutDetails_ShouldReportCompleteDetails()
    {
        // Arrange
        GivenDetails(new EncryptedPayoutDetailsRow("Ada Lovelace", "00-00-00", "12345678"));

        // Act
        var details = await HandlePayoutsAsync();

        // Assert
        details.AccountName.Should().Be("Ada Lovelace");
        details.SortCode.Should().Be("00-00-00");
        details.AccountNumber.Should().Be("12345678");
        details.HasDetails.Should().BeTrue();
    }

    [Fact]
    public async Task GetMyPayoutDetails_ShouldNotCallDetailsComplete_WhenOneFieldIsMissing()
    {
        // Nobody can be paid with two thirds of a bank account, and the judgement is made after decryption rather than on the
        // stored values.
        GivenDetails(new EncryptedPayoutDetailsRow("Ada Lovelace", null, "12345678"));

        // Act
        var details = await HandlePayoutsAsync();

        // Assert
        details.HasDetails.Should().BeFalse();
    }

    [Fact]
    public async Task GetMyPayoutDetails_ShouldNameThePayingAdministratorsInFull()
    {
        // The full name rather than the abbreviated one players see, because this is telling somebody who to expect money from.
        GivenDetails(null);
        GivenAdministrators(
            new PayingAdministratorRow("u2", "Grace", "Hopper"),
            new PayingAdministratorRow("u3", "Ada", "Lovelace"));

        // Act
        var details = await HandlePayoutsAsync();

        // Assert
        details.VisibleToAdmins.Should().Equal("Ada Lovelace", "Grace Hopper");
    }

    [Fact]
    public async Task GetMyPayoutDetails_ShouldNameAnAdministratorOnce_WhenTheyRunSeveralOfTheirLeagues()
    {
        // Arrange
        GivenDetails(null);
        GivenAdministrators(
            new PayingAdministratorRow("u2", "Grace", "Hopper"),
            new PayingAdministratorRow("u2", "Grace", "Hopper"));

        // Act
        var details = await HandlePayoutsAsync();

        // Assert
        details.VisibleToAdmins.Should().Equal("Grace Hopper");
    }

    [Fact]
    public async Task GetMyPayoutDetails_ShouldReportNoAdministrators_WhenNoneOfTheirLeaguesPayPrizes()
    {
        // Arrange
        GivenDetails(null);

        // Act
        var details = await HandlePayoutsAsync();

        // Assert
        details.VisibleToAdmins.Should().BeEmpty();
    }

    #endregion

    private void GivenProfile(AccountProfileRow row) =>
        _profileQuery.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(row);

    private void GivenDetails(EncryptedPayoutDetailsRow? row) =>
        _payoutQuery.GetDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(row);

    private void GivenAdministrators(params PayingAdministratorRow[] administrators) =>
        _payoutQuery.GetPayingAdministratorsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(administrators);

    private Task<Contracts.Payouts.MyPayoutDetailsDto> HandlePayoutsAsync() =>
        new GetMyPayoutDetailsQueryHandler(_payoutQuery, _encryption)
            .Handle(new GetMyPayoutDetailsQuery(UserId), CancellationToken.None);
}
