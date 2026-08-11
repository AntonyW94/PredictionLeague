using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Payouts;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// A league's payout screen: who is owed what, who has been paid, and the bank details to pay them with.
///
/// These tests used to mock the database connection and tell its four reads apart by their generic argument, so they were
/// coupled to how many statements the handler ran. They now arrange the port's answer instead - the same coverage, without
/// the coupling.
/// </summary>
public class GetLeaguePayoutsQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string AdminId = "user-admin";

    private static readonly DateTime PaidAt = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly ILeaguePayoutsQuery _payoutsQuery = Substitute.For<ILeaguePayoutsQuery>();
    private readonly IFieldEncryptionService _fieldEncryptionService = Substitute.For<IFieldEncryptionService>();
    private readonly GetLeaguePayoutsQueryHandler _handler;

    public GetLeaguePayoutsQueryHandlerTests()
    {
        // The encryption service is not under test here; it hands back whatever it was given.
        _fieldEncryptionService.Decrypt(Arg.Any<string?>()).Returns(call => call.Arg<string?>());

        _handler = new GetLeaguePayoutsQueryHandler(_payoutsQuery, _fieldEncryptionService);
    }

    #region Who may see the payouts

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        _payoutsQuery
            .ExecuteAsync(LeagueId, AdminId, Arg.Any<CancellationToken>())
            .Returns((LeaguePayoutsData?)null);

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldRefuseAnyoneButTheLeagueAdministrator()
    {
        // Arrange
        Given(isAdministrator: false, winnings: [Winning(AdminId, "Ada", "Lovelace", PrizeType.Overall, 50m)]);

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only the league administrator can view payouts.");
    }

    [Fact]
    public async Task Handle_ShouldNotDecryptAnyBankDetails_WhenTheCallerIsRefused()
    {
        // Arrange
        Given(
            isAdministrator: false,
            winnings: [Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 50m)],
            bankDetails: [Details("u1", "A Lovelace", "00-00-00", "12345678")]);

        // Act
        var act = async () => await HandleAsync();

        // Assert - nobody else's account details are unlocked, let alone returned.
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _fieldEncryptionService.DidNotReceiveWithAnyArgs().Decrypt(default);
    }

    #endregion

    #region What each winner is owed

    [Fact]
    public async Task Handle_ShouldTotalEveryPrizeAWinnerHasTaken()
    {
        // Arrange
        Given(winnings:
        [
            Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 50m),
            Winning("u1", "Ada", "Lovelace", PrizeType.Monthly, 20m),
            Winning("u1", "Ada", "Lovelace", PrizeType.Round, 5m)
        ]);

        // Act
        var winner = (await HandleAsync()).Winners.Single();

        // Assert
        winner.TotalAmount.Should().Be(75m);
    }

    [Fact]
    public async Task Handle_ShouldBreakTheTotalDownByPrizeCategory()
    {
        // Arrange
        Given(winnings:
        [
            Winning("u1", "Ada", "Lovelace", PrizeType.Round, 5m),
            Winning("u1", "Ada", "Lovelace", PrizeType.Round, 5m),
            Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 50m)
        ]);

        // Act
        var winner = (await HandleAsync()).Winners.Single();

        // Assert - one line per kind of prize, with that kind's prizes added together.
        winner.Breakdown.Should().HaveCount(2);
        winner.Breakdown.Sum(line => line.Amount).Should().Be(60m);
    }

    [Fact]
    public async Task Handle_ShouldShowTheWinnersFullName()
    {
        // Arrange
        Given(winnings: [Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 50m)]);

        // Act
        var winner = (await HandleAsync()).Winners.Single();

        // Assert - not the abbreviated "Ada L" used elsewhere: an administrator paying real money has to match the name
        // on a bank account.
        winner.UserName.Should().Be("Ada Lovelace");
    }

    [Fact]
    public async Task Handle_ShouldOrderWinnersByAmountThenName()
    {
        // Arrange
        Given(winnings:
        [
            Winning("u1", "Grace", "Hopper", PrizeType.Overall, 50m),
            Winning("u2", "Ada", "Lovelace", PrizeType.Overall, 50m),
            Winning("u3", "Alan", "Turing", PrizeType.Overall, 100m)
        ]);

        // Act
        var winners = (await HandleAsync()).Winners;

        // Assert
        winners.Select(winner => winner.UserName).Should().Equal("Alan Turing", "Ada Lovelace", "Grace Hopper");
    }

    [Fact]
    public async Task Handle_ShouldReturnNoWinners_WhenNothingHasBeenWon()
    {
        // Arrange
        Given();

        // Act
        var payouts = await HandleAsync();

        // Assert
        payouts.Winners.Should().BeEmpty();
        payouts.OutstandingTotal.Should().Be(0m);
        payouts.PaidTotal.Should().Be(0m);
    }

    #endregion

    #region Paid, unpaid and discrepancies

    [Fact]
    public async Task Handle_ShouldReportAWinnerAsUnpaid_WhenNoPayoutHasBeenRecorded()
    {
        // Arrange
        Given(winnings: [Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 50m)]);

        // Act
        var winner = (await HandleAsync()).Winners.Single();

        // Assert
        winner.IsPaid.Should().BeFalse();
        winner.PaidAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldReportAWinnerAsUnpaid_WhenThePayoutRowHasNoPaidDate()
    {
        // Arrange - a recorded intention to pay is not a payment.
        Given(
            winnings: [Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 50m)],
            storedPayouts: [Stored("u1", 50m, paidAtUtc: null)]);

        // Act
        var winner = (await HandleAsync()).Winners.Single();

        // Assert
        winner.IsPaid.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReportAWinnerAsPaid_WhenThePayoutHasADate()
    {
        // Arrange
        Given(
            winnings: [Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 50m)],
            storedPayouts: [Stored("u1", 50m, PaidAt)]);

        // Act
        var winner = (await HandleAsync()).Winners.Single();

        // Assert
        winner.IsPaid.Should().BeTrue();
        winner.PaidAtUtc.Should().Be(PaidAt);
        winner.HasDiscrepancy.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldFlagADiscrepancy_WhenThePaidAmountNoLongerMatchesWhatIsOwed()
    {
        // Arrange - a round was re-processed and the prize moved after the payment.
        Given(
            winnings: [Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 75m)],
            storedPayouts: [Stored("u1", 50m, PaidAt)]);

        // Act
        var winner = (await HandleAsync()).Winners.Single();

        // Assert
        winner.HasDiscrepancy.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldNotFlagADiscrepancy_WhenTheWinnerHasNotBeenPaid()
    {
        // Arrange
        Given(
            winnings: [Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 75m)],
            storedPayouts: [Stored("u1", 50m, paidAtUtc: null)]);

        // Act
        var winner = (await HandleAsync()).Winners.Single();

        // Assert - they are simply owed the new figure; flagging it would warn on every screen mid-season.
        winner.HasDiscrepancy.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldSplitTheTotalsBetweenPaidAndOutstanding()
    {
        // Arrange
        Given(
            winnings:
            [
                Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 50m),
                Winning("u2", "Grace", "Hopper", PrizeType.Overall, 30m)
            ],
            storedPayouts: [Stored("u1", 50m, PaidAt)]);

        // Act
        var payouts = await HandleAsync();

        // Assert
        payouts.PaidTotal.Should().Be(50m);
        payouts.OutstandingTotal.Should().Be(30m);
    }

    [Fact]
    public async Task Handle_ShouldCountTheAmountActuallyPaid_WhenThereIsADiscrepancy()
    {
        // Arrange - owed 75 now, but 50 was what left the administrator's account.
        Given(
            winnings: [Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 75m)],
            storedPayouts: [Stored("u1", 50m, PaidAt)]);

        // Act
        var payouts = await HandleAsync();

        // Assert - money already sent is a historical fact, so re-pricing a prize must not change it.
        payouts.PaidTotal.Should().Be(50m);
    }

    #endregion

    #region Whether the season is over

    [Fact]
    public async Task Handle_ShouldReportTheSeasonComplete_WhenEveryRoundHasFinished()
    {
        // Arrange
        Given(seasonRoundCount: 3, completedRoundCount: 3);

        // Act
        var payouts = await HandleAsync();

        // Assert
        payouts.SeasonComplete.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReportTheSeasonIncomplete_WhileARoundRemains()
    {
        // Arrange
        Given(seasonRoundCount: 3, completedRoundCount: 2);

        // Act
        var payouts = await HandleAsync();

        // Assert
        payouts.SeasonComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReportTheSeasonIncomplete_WhenItHasNoRoundsAtAll()
    {
        // Arrange
        Given(seasonRoundCount: 0, completedRoundCount: 0);

        // Act
        var payouts = await HandleAsync();

        // Assert - otherwise the screen would offer to pay out a season that has not started.
        payouts.SeasonComplete.Should().BeFalse();
    }

    #endregion

    #region The winners' bank details

    [Fact]
    public async Task Handle_ShouldReturnTheDecryptedBankDetails_WhenTheWinnerHasSharedThem()
    {
        // Arrange
        Given(
            winnings: [Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 50m)],
            bankDetails: [Details("u1", "A Lovelace", "00-00-00", "12345678")]);

        // Act
        var winner = (await HandleAsync()).Winners.Single();

        // Assert
        winner.HasSharedDetails.Should().BeTrue();
        winner.AccountName.Should().Be("A Lovelace");
        winner.SortCode.Should().Be("00-00-00");
        winner.AccountNumber.Should().Be("12345678");
    }

    [Fact]
    public async Task Handle_ShouldReportNoSharedDetails_WhenTheWinnerHasNotGivenAny()
    {
        // Arrange
        Given(winnings: [Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 50m)]);

        // Act
        var winner = (await HandleAsync()).Winners.Single();

        // Assert
        winner.HasSharedDetails.Should().BeFalse();
        winner.AccountName.Should().BeNull();
    }

    [Theory]
    [InlineData(null, "00-00-00", "12345678")]
    [InlineData("A Lovelace", null, "12345678")]
    [InlineData("A Lovelace", "00-00-00", null)]
    public async Task Handle_ShouldReportNoSharedDetails_WhenAnyPartIsMissing(
        string? accountName,
        string? sortCode,
        string? accountNumber)
    {
        // Arrange
        Given(
            winnings: [Winning("u1", "Ada", "Lovelace", PrizeType.Overall, 50m)],
            bankDetails: [Details("u1", accountName, sortCode, accountNumber)]);

        // Act
        var winner = (await HandleAsync()).Winners.Single();

        // Assert - showing two of the three would invite somebody to guess the rest.
        winner.HasSharedDetails.Should().BeFalse();
    }

    #endregion

    private void Given(
        bool isAdministrator = true,
        int seasonRoundCount = 3,
        int completedRoundCount = 3,
        IReadOnlyList<PayoutWinningRow>? winnings = null,
        IReadOnlyList<StoredPayoutRow>? storedPayouts = null,
        IReadOnlyList<PayoutBankDetailsRow>? bankDetails = null)
    {
        _payoutsQuery
            .ExecuteAsync(LeagueId, AdminId, Arg.Any<CancellationToken>())
            .Returns(new LeaguePayoutsData(
                isAdministrator,
                seasonRoundCount,
                completedRoundCount,
                winnings ?? [],
                storedPayouts ?? [],
                bankDetails ?? []));
    }

    private async Task<LeaguePayoutsDto> HandleAsync() =>
        await _handler.Handle(new GetLeaguePayoutsQuery(LeagueId, AdminId), CancellationToken.None);

    private static PayoutWinningRow Winning(
        string userId,
        string firstName,
        string lastName,
        PrizeType prizeType,
        decimal amount) =>
        new(userId, firstName, lastName, prizeType, amount);

    private static StoredPayoutRow Stored(string userId, decimal totalAmount, DateTime? paidAtUtc) =>
        new(userId, totalAmount, paidAtUtc);

    private static PayoutBankDetailsRow Details(
        string userId,
        string? accountName,
        string? sortCode,
        string? accountNumber) =>
        new(userId, accountName, sortCode, accountNumber);
}
