using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// A league's bank details, for the administrator's own edit form.
/// </summary>
/// <remarks>
/// The read is deliberately unfiltered by user: it returns the details along with who administers the league, and this handler
/// decides whether the caller may see them. That check therefore has to happen before anything is decrypted, and it is the only
/// thing standing between one player and another league's bank account.
/// </remarks>
public class GetLeagueBankDetailsQueryHandlerTests
{
    private const int LeagueId = 7;
    private const string AdministratorUserId = "admin-1";
    private const string OtherUserId = "user-2";

    private readonly ILeagueBankDetailsQuery _bankDetailsQuery = Substitute.For<ILeagueBankDetailsQuery>();
    private readonly IFieldEncryptionService _fieldEncryptionService = Substitute.For<IFieldEncryptionService>();
    private readonly GetLeagueBankDetailsQueryHandler _handler;

    public GetLeagueBankDetailsQueryHandlerTests()
    {
        _fieldEncryptionService.Decrypt(Arg.Any<string?>())
            .Returns(call => call.Arg<string?>() is { } value ? value.Replace("encrypted-", string.Empty) : null);

        _handler = new GetLeagueBankDetailsQueryHandler(_bankDetailsQuery, _fieldEncryptionService);
    }

    #region Who may see them

    [Fact]
    public async Task Handle_ShouldRefuseSomebodyWhoDoesNotAdministerTheLeague()
    {
        // Arrange
        GivenBankDetails(Row());

        // Act
        var act = () => HandleAsync(OtherUserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldNotDecryptAnything_WhenTheCallerIsNotTheAdministrator()
    {
        // The order matters: the check is made before the plaintext exists at all.
        GivenBankDetails(Row());

        // Act
        var act = () => HandleAsync(OtherUserId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fieldEncryptionService.DidNotReceiveWithAnyArgs().Decrypt(default);
    }

    [Fact]
    public async Task Handle_ShouldReportTheLeagueIsMissing_WhenThereIsNoSuchLeague()
    {
        // Arrange
        _bankDetailsQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((EncryptedLeagueBankDetailsRow?)null);

        // Act
        var act = () => HandleAsync(AdministratorUserId);

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    #endregion

    #region What the administrator sees

    [Fact]
    public async Task Handle_ShouldReturnTheDecryptedDetails_ForTheAdministrator()
    {
        // Arrange
        GivenBankDetails(Row());

        // Act
        var details = await HandleAsync(AdministratorUserId);

        // Assert
        details.BankAccountName.Should().Be("A Lovelace");
        details.BankSortCode.Should().Be("00-00-00");
        details.BankAccountNumber.Should().Be("12345678");
    }

    [Fact]
    public async Task Handle_ShouldReturnThePaymentReferenceAsStored()
    {
        // Arrange - the reference template is not sensitive, so it is not encrypted and must not be put through the decryption.
        GivenBankDetails(Row());

        // Act
        var details = await HandleAsync(AdministratorUserId);

        // Assert
        details.PaymentReferenceTemplate.Should().Be("OFFICE-{Name}");
    }

    [Fact]
    public async Task Handle_ShouldReturnNothingForEachFieldTheAdministratorHasNotFilledIn()
    {
        // Arrange - a league can have some details recorded and not others, and the form has to open either way.
        GivenBankDetails(new EncryptedLeagueBankDetailsRow(AdministratorUserId, null, null, null, null));

        // Act
        var details = await HandleAsync(AdministratorUserId);

        // Assert
        details.BankAccountName.Should().BeNull();
        details.BankSortCode.Should().BeNull();
        details.BankAccountNumber.Should().BeNull();
        details.PaymentReferenceTemplate.Should().BeNull();
    }

    #endregion

    private static EncryptedLeagueBankDetailsRow Row() =>
        new(AdministratorUserId, "encrypted-A Lovelace", "encrypted-00-00-00", "encrypted-12345678", "OFFICE-{Name}");

    private void GivenBankDetails(EncryptedLeagueBankDetailsRow row) =>
        _bankDetailsQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(row);

    private Task<LeagueBankDetailsDto> HandleAsync(string requestingUserId) =>
        _handler.Handle(new GetLeagueBankDetailsQuery(LeagueId, requestingUserId), CancellationToken.None);
}
