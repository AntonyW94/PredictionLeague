using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// A league's bank details.
///
/// The rule that decides who may read them had no tests: the handler carried the standard "a SQL string plus a mapping"
/// coverage exclusion, which was never true of this one.
/// </summary>
public class GetLeaguePaymentInfoQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string UserId = "user-me";

    private readonly ILeaguePaymentInfoQuery _paymentInfoQuery = Substitute.For<ILeaguePaymentInfoQuery>();
    private readonly IFieldEncryptionService _fieldEncryptionService = Substitute.For<IFieldEncryptionService>();
    private readonly GetLeaguePaymentInfoQueryHandler _handler;

    public GetLeaguePaymentInfoQueryHandlerTests()
    {
        // The encryption service is not under test here; it hands back whatever it was given.
        _fieldEncryptionService.Decrypt(Arg.Any<string?>()).Returns(call => call.Arg<string?>());

        _handler = new GetLeaguePaymentInfoQueryHandler(_paymentInfoQuery, _fieldEncryptionService);
    }

    #region Who may see the bank details

    [Fact]
    public async Task Handle_ShouldAllowTheLeagueAdministrator()
    {
        // Arrange
        Given(Row(isAdministrator: true, membershipStatus: null));

        // Act
        var info = await HandleAsync();

        // Assert
        info.LeagueName.Should().Be("Test League");
    }

    [Theory]
    [InlineData(LeagueMemberStatus.Approved)]
    [InlineData(LeagueMemberStatus.Pending)]
    public async Task Handle_ShouldAllowSomebodyWhoStillHasToPayIntoTheLeague(LeagueMemberStatus status)
    {
        // Arrange
        Given(Row(isAdministrator: false, membershipStatus: status));

        // Act
        var act = async () => await HandleAsync();

        // Assert - pending is the case that has to work: you ask to join, then you need the details in order to pay.
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_ShouldRefuseSomebodyWhoseRequestWasTurnedDown()
    {
        // Somebody turned away keeps no claim on the league's account number. The read used to answer only whether a
        // membership row existed, so a rejected applicant kept the details they had been shown while waiting.
        Given(Row(isAdministrator: false, membershipStatus: LeagueMemberStatus.Rejected));

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldNotDecryptAnything_ForSomebodyWhoseRequestWasTurnedDown()
    {
        // Arrange - the check happens before the plaintext exists, as it does for every other refused caller.
        Given(Row(isAdministrator: false, membershipStatus: LeagueMemberStatus.Rejected));

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fieldEncryptionService.DidNotReceiveWithAnyArgs().Decrypt(default);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAStrangerWithNoEntryCode()
    {
        // Arrange
        Given(Row(isAdministrator: false, membershipStatus: null, entryCode: "SECRET"));

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldAllowAProspectiveJoinerWithTheRightEntryCode()
    {
        // Arrange - a private league's joining page shows payment details before the request is approved.
        Given(Row(isAdministrator: false, membershipStatus: null, entryCode: "SECRET"));

        // Act
        var act = async () => await HandleAsync(entryCode: "SECRET");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_ShouldMatchTheEntryCodeWhateverItsCase()
    {
        // Arrange
        Given(Row(isAdministrator: false, membershipStatus: null, entryCode: "SECRET"));

        // Act
        var act = async () => await HandleAsync(entryCode: "secret");

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_ShouldRefuseTheWrongEntryCode()
    {
        // Arrange
        Given(Row(isAdministrator: false, membershipStatus: null, entryCode: "SECRET"));

        // Act
        var act = async () => await HandleAsync(entryCode: "GUESS");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldRefuseABlankEntryCode_EvenForALeagueThatHasNone(string? suppliedEntryCode)
    {
        // Arrange - the case that matters: a public league stores no code, so a blank supplied code must not be treated
        // as matching it, or its bank details would be readable by anybody.
        Given(Row(isAdministrator: false, membershipStatus: null, entryCode: null));

        // Act
        var act = async () => await HandleAsync(entryCode: suppliedEntryCode);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldNotDecryptAnything_ForACallerWhoIsRefused()
    {
        // Arrange
        Given(Row(isAdministrator: false, membershipStatus: null, entryCode: "SECRET"));

        // Act
        var act = async () => await HandleAsync();

        // Assert - the details are never even unlocked, let alone returned.
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fieldEncryptionService.DidNotReceiveWithAnyArgs().Decrypt(default);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        _paymentInfoQuery
            .ExecuteAsync(LeagueId, UserId, Arg.Any<CancellationToken>())
            .Returns((LeaguePaymentInfoRow?)null);

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    #endregion

    #region The details themselves

    [Fact]
    public async Task Handle_ShouldReturnTheDecryptedBankDetails()
    {
        // Arrange
        Given(Row(
            isAdministrator: true,
            encryptedAccountName: "A Willson",
            encryptedSortCode: "00-00-00",
            encryptedAccountNumber: "12345678",
            price: 15m));

        // Act
        var info = await HandleAsync();

        // Assert
        info.HasBankDetails.Should().BeTrue();
        info.AccountName.Should().Be("A Willson");
        info.SortCode.Should().Be("00-00-00");
        info.AccountNumber.Should().Be("12345678");
        info.Amount.Should().Be(15m);
    }

    [Theory]
    [InlineData(null, "00-00-00", "12345678")]
    [InlineData("A Willson", null, "12345678")]
    [InlineData("A Willson", "00-00-00", null)]
    public async Task Handle_ShouldReportNoBankDetails_WhenAnyPartIsMissing(
        string? accountName,
        string? sortCode,
        string? accountNumber)
    {
        // Arrange
        Given(Row(
            isAdministrator: true,
            encryptedAccountName: accountName,
            encryptedSortCode: sortCode,
            encryptedAccountNumber: accountNumber));

        // Act
        var info = await HandleAsync();

        // Assert - a partly filled-in account is no use to somebody trying to pay.
        info.HasBankDetails.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldUseTheLeaguesPaymentReference_WhenTheAdministratorSetOne()
    {
        // Arrange
        Given(Row(isAdministrator: true, paymentReferenceTemplate: "PREDICT2026"));

        // Act
        var info = await HandleAsync();

        // Assert
        info.Reference.Should().Be("PREDICT2026");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_ShouldFallBackToThePayersName_WhenTheLeagueSetNoReference(string? template)
    {
        // Arrange
        Given(Row(
            isAdministrator: true,
            paymentReferenceTemplate: template,
            requestingFirstName: "Ada",
            requestingLastName: "Lovelace"));

        // Act
        var info = await HandleAsync();

        // Assert - a reference is never empty, or the payment cannot be matched to anybody.
        info.Reference.Should().Be("Ada Lovelace");
    }

    [Fact]
    public async Task Handle_ShouldNotLeaveStraySpacing_WhenThePayerHasNoLastName()
    {
        // Arrange
        Given(Row(isAdministrator: true, requestingFirstName: "Ada", requestingLastName: null));

        // Act
        var info = await HandleAsync();

        // Assert
        info.Reference.Should().Be("Ada");
    }

    #endregion

    private void Given(LeaguePaymentInfoRow row)
    {
        _paymentInfoQuery.ExecuteAsync(LeagueId, UserId, Arg.Any<CancellationToken>()).Returns(row);
    }

    private async Task<LeaguePaymentInfoDto> HandleAsync(string? entryCode = null) =>
        await _handler.Handle(new GetLeaguePaymentInfoQuery(LeagueId, UserId, entryCode), CancellationToken.None);

    private static LeaguePaymentInfoRow Row(
        bool isAdministrator = false,
        LeagueMemberStatus? membershipStatus = null,
        string? entryCode = null,
        string? encryptedAccountName = "A Willson",
        string? encryptedSortCode = "00-00-00",
        string? encryptedAccountNumber = "12345678",
        string? paymentReferenceTemplate = null,
        decimal price = 10m,
        string? requestingFirstName = "Ada",
        string? requestingLastName = "Lovelace") =>
        new(
            "Test League",
            price,
            entryCode,
            encryptedAccountName,
            encryptedSortCode,
            encryptedAccountNumber,
            paymentReferenceTemplate,
            isAdministrator,
            membershipStatus,
            requestingFirstName,
            requestingLastName);
}
