using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Payouts;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;
using static ThePredictions.Application.Features.Leagues.Queries.GetLeaguePayoutsQueryHandler;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// The league administrator's end-of-season payout list: who is owed what, whether they have been paid,
/// and the bank details to pay them with. Three things here are worth more than "a SQL string plus a
/// mapping" - it is the only place that refuses a non-administrator, the only place that decrypts a
/// player's bank details, and the only place that notices a payout was recorded for an amount that no
/// longer matches what the player is actually owed.
/// </summary>
public class GetLeaguePayoutsQueryHandlerTests
{
    private const int LeagueId = 10;
    private const string AdminUserId = "admin-user";

    private static readonly DateTime PaidAt = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc);

    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly IFieldEncryptionService _fieldEncryptionService = Substitute.For<IFieldEncryptionService>();
    private readonly GetLeaguePayoutsQueryHandler _handler;

    public GetLeaguePayoutsQueryHandlerTests()
    {
        // The encryption service is exercised in its own tests; here it stands in as a pass-through so
        // these tests assert what the handler does with the decrypted values.
        _fieldEncryptionService.Decrypt(Arg.Any<string?>()).Returns(call => call.Arg<string?>());
        _handler = new GetLeaguePayoutsQueryHandler(_dbConnection, _fieldEncryptionService);

        GivenLeague();
    }

    private void GivenLeague(string administratorUserId = AdminUserId, bool seasonComplete = true) =>
        _dbConnection.QuerySingleOrDefaultAsync<LeagueRow>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(new LeagueRow(administratorUserId, seasonComplete));

    private void GivenNoLeague() =>
        _dbConnection.QuerySingleOrDefaultAsync<LeagueRow>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns((LeagueRow?)null);

    private void GivenWinnings(params WinningRow[] rows) =>
        _dbConnection.QueryAsync<WinningRow>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>()).Returns(rows);

    private void GivenStoredPayouts(params StoredPayoutRow[] rows) =>
        _dbConnection.QueryAsync<StoredPayoutRow>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>()).Returns(rows);

    private void GivenPayoutDetails(params PayoutDetailRow[] rows) =>
        _dbConnection.QueryAsync<PayoutDetailRow>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>()).Returns(rows);

    private static WinningRow Winning(string userId, string userName, decimal amount, PrizeType prizeType = PrizeType.Overall) =>
        new(userId, userName, prizeType, amount);

    private Task<LeaguePayoutsDto> HandleAsync(string requestingUserId = AdminUserId) =>
        _handler.Handle(new GetLeaguePayoutsQuery(LeagueId, requestingUserId), CancellationToken.None);

    // ---------- who may look ----------

    [Fact]
    public async Task Handle_ShouldThrowNotFound_WhenTheLeagueDoesNotExist()
    {
        GivenNoLeague();

        await FluentActions.Awaiting(() => HandleAsync()).Should().ThrowAsync<KeyNotFoundException>();
    }

    // Bank details sit behind this check, so it is the difference between an admin tool and a leak.
    [Fact]
    public async Task Handle_ShouldRefuseAnyoneButTheLeagueAdministrator()
    {
        var act = () => HandleAsync(requestingUserId: "someone-else");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_ShouldNotReadAnyWinnings_WhenTheCallerIsRefused()
    {
        await FluentActions.Awaiting(() => HandleAsync(requestingUserId: "someone-else")).Should().ThrowAsync<UnauthorizedAccessException>();

        await _dbConnection.DidNotReceiveWithAnyArgs().QueryAsync<WinningRow>(default!, CancellationToken.None, default);
    }

    // ---------- totals per winner ----------

    [Fact]
    public async Task Handle_ShouldTotalEveryPrizeAWinnerHasTaken()
    {
        GivenWinnings(
            Winning("user-1", "Ada L", 20m, PrizeType.Overall),
            Winning("user-1", "Ada L", 5m, PrizeType.Round));

        var winner = (await HandleAsync()).Winners.Single();

        winner.TotalAmount.Should().Be(25m);
    }

    [Fact]
    public async Task Handle_ShouldBreakTheTotalDownByPrizeCategory()
    {
        GivenWinnings(
            Winning("user-1", "Ada L", 20m, PrizeType.Overall),
            Winning("user-1", "Ada L", 3m, PrizeType.Round),
            Winning("user-1", "Ada L", 2m, PrizeType.Round));

        var breakdown = (await HandleAsync()).Winners.Single().Breakdown;

        breakdown.Should().HaveCount(2);
        breakdown.Sum(b => b.Amount).Should().Be(25m);
        breakdown.Should().ContainSingle(b => b.Amount == 5m, "the two round prizes are added together");
    }

    [Fact]
    public async Task Handle_ShouldOrderWinnersByAmountThenName()
    {
        GivenWinnings(
            Winning("user-1", "Zoe W", 10m),
            Winning("user-2", "Ada L", 30m),
            Winning("user-3", "Grace H", 10m));

        (await HandleAsync()).Winners.Select(w => w.UserName).Should().Equal("Ada L", "Grace H", "Zoe W");
    }

    // ---------- paid, unpaid, and the discrepancy flag ----------

    [Fact]
    public async Task Handle_ShouldReportAWinnerAsUnpaid_WhenNoPayoutHasBeenRecorded()
    {
        GivenWinnings(Winning("user-1", "Ada L", 25m));

        var winner = (await HandleAsync()).Winners.Single();

        winner.IsPaid.Should().BeFalse();
        winner.PaidAtUtc.Should().BeNull();
    }

    // A payout row can exist before it is settled, so it is the date that decides, not the row.
    [Fact]
    public async Task Handle_ShouldReportAWinnerAsUnpaid_WhenThePayoutRowHasNoPaidDate()
    {
        GivenWinnings(Winning("user-1", "Ada L", 25m));
        GivenStoredPayouts(new StoredPayoutRow("user-1", 25m, PaidAtUtc: null));

        (await HandleAsync()).Winners.Single().IsPaid.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReportAWinnerAsPaid_WhenThePayoutHasADate()
    {
        GivenWinnings(Winning("user-1", "Ada L", 25m));
        GivenStoredPayouts(new StoredPayoutRow("user-1", 25m, PaidAt));

        var winner = (await HandleAsync()).Winners.Single();

        winner.IsPaid.Should().BeTrue();
        winner.PaidAtUtc.Should().Be(PaidAt);
        winner.HasDiscrepancy.Should().BeFalse();
    }

    // The money check: a prize settled after the payout was recorded leaves the admin having paid the
    // wrong amount, and nothing else in the app would notice.
    [Fact]
    public async Task Handle_ShouldFlagADiscrepancy_WhenThePaidAmountNoLongerMatchesWhatIsOwed()
    {
        GivenWinnings(Winning("user-1", "Ada L", 30m));
        GivenStoredPayouts(new StoredPayoutRow("user-1", 25m, PaidAt));

        (await HandleAsync()).Winners.Single().HasDiscrepancy.Should().BeTrue();
    }

    // An unpaid winner cannot be out by the wrong amount, because nothing has been paid.
    [Fact]
    public async Task Handle_ShouldNotFlagADiscrepancy_WhenTheWinnerHasNotBeenPaid()
    {
        GivenWinnings(Winning("user-1", "Ada L", 30m));
        GivenStoredPayouts(new StoredPayoutRow("user-1", 25m, PaidAtUtc: null));

        (await HandleAsync()).Winners.Single().HasDiscrepancy.Should().BeFalse();
    }

    // ---------- league totals ----------

    [Fact]
    public async Task Handle_ShouldSplitTheTotalsBetweenPaidAndOutstanding()
    {
        GivenWinnings(Winning("user-1", "Ada L", 30m), Winning("user-2", "Grace H", 20m));
        GivenStoredPayouts(new StoredPayoutRow("user-1", 30m, PaidAt));

        var result = await HandleAsync();

        result.PaidTotal.Should().Be(30m);
        result.OutstandingTotal.Should().Be(20m);
    }

    // The paid total is what was actually handed over, not what is now owed - otherwise a discrepancy
    // would silently correct itself in the summary.
    [Fact]
    public async Task Handle_ShouldCountTheAmountActuallyPaid_WhenThereIsADiscrepancy()
    {
        GivenWinnings(Winning("user-1", "Ada L", 30m));
        GivenStoredPayouts(new StoredPayoutRow("user-1", 25m, PaidAt));

        (await HandleAsync()).PaidTotal.Should().Be(25m);
    }

    [Fact]
    public async Task Handle_ShouldReportWhetherTheSeasonIsComplete()
    {
        GivenLeague(seasonComplete: false);
        GivenWinnings(Winning("user-1", "Ada L", 10m));

        (await HandleAsync()).SeasonComplete.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReturnNoWinners_WhenNothingHasBeenWon()
    {
        GivenWinnings();

        var result = await HandleAsync();

        result.Winners.Should().BeEmpty();
        result.PaidTotal.Should().Be(0m);
        result.OutstandingTotal.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ShouldNotLookUpBankDetails_WhenThereAreNoWinners()
    {
        GivenWinnings();

        await HandleAsync();

        await _dbConnection.DidNotReceiveWithAnyArgs().QueryAsync<PayoutDetailRow>(default!, CancellationToken.None, default);
    }

    // ---------- bank details ----------

    [Fact]
    public async Task Handle_ShouldReturnTheDecryptedBankDetails_WhenTheWinnerHasSharedThem()
    {
        GivenWinnings(Winning("user-1", "Ada L", 25m));
        GivenPayoutDetails(new PayoutDetailRow("user-1", "A Lovelace", "00-11-22", "12345678"));

        var winner = (await HandleAsync()).Winners.Single();

        winner.HasSharedDetails.Should().BeTrue();
        winner.AccountName.Should().Be("A Lovelace");
        winner.SortCode.Should().Be("00-11-22");
        winner.AccountNumber.Should().Be("12345678");
    }

    [Fact]
    public async Task Handle_ShouldReportNoSharedDetails_WhenTheWinnerHasNotGivenAny()
    {
        GivenWinnings(Winning("user-1", "Ada L", 25m));

        var winner = (await HandleAsync()).Winners.Single();

        winner.HasSharedDetails.Should().BeFalse();
        winner.AccountName.Should().BeNull();
    }

    // A partly-filled record cannot be paid into, so it does not count as shared.
    [Fact]
    public async Task Handle_ShouldReportNoSharedDetails_WhenAnyPartIsMissing()
    {
        GivenWinnings(Winning("user-1", "Ada L", 25m));
        GivenPayoutDetails(new PayoutDetailRow("user-1", "A Lovelace", "00-11-22", AccountNumber: null));

        (await HandleAsync()).Winners.Single().HasSharedDetails.Should().BeFalse();
    }
}
