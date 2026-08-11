using FluentAssertions;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ILeaguePayoutsQuery"/> implementation must return.
///
/// The narrowing on the bank details is the obligation to take seriously: <c>UserPayoutDetails</c> holds the most
/// sensitive rows in the schema, and only the league's own winners' details may come back. An adapter reading the whole
/// table would still produce a correct screen, which is exactly why it needs a test.
/// </summary>
public abstract class LeaguePayoutsQueryConformanceTests
{
    protected abstract ILeaguePayoutsQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNull_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId + 5_000, world.AdministratorId, CancellationToken.None);

        // Assert
        data.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportTheAdministrator()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert
        data!.IsAdministrator.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReportAnOrdinaryMemberAsTheAdministrator()
    {
        // Arrange
        var world = await ArrangeAsync();
        var memberId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddLeagueMemberAsync(world.LeagueId, memberId);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, memberId, CancellationToken.None);

        // Assert - the league still comes back; refusing the caller is the handler's rule.
        data!.IsAdministrator.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountTheSeasonsRoundsAndTheCompletedOnes()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddRoundAsync(world.SeasonId, 1, DateTime.UtcNow.AddDays(-2), RoundStatus.Completed);
        await Seed.AddRoundAsync(world.SeasonId, 2, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddRoundAsync(world.SeasonId, 3, DateTime.UtcNow.AddDays(7));

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert - counts, not a verdict. Whether that means the season is over is SeasonCompletion, and this screen
        // asks the question differently from the dashboards.
        data!.SeasonRoundCount.Should().Be(3);
        data.CompletedRoundCount.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportNoRounds_ForASeasonThatHasNotStarted()
    {
        // Arrange
        var world = await ArrangeAsync();

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert - the zero is what stops the screen offering to pay out a season with nothing in it.
        data!.SeasonRoundCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryPrizeWonWithTheWinnersNameParts()
    {
        // Arrange
        var world = await ArrangeAsync();
        var overallId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Overall, 50m);
        var monthlyId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Monthly, 20m);
        await Seed.AddWinningAsync(world.AdministratorId, overallId, 50m);
        await Seed.AddWinningAsync(world.AdministratorId, monthlyId, 20m, month: 3);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert - one row per prize, unaggregated, and the name in parts so the handler can format it in full.
        data!.Winnings.Should().HaveCount(2);
        data.Winnings.Select(winning => winning.PrizeType)
            .Should().BeEquivalentTo([PrizeType.Overall, PrizeType.Monthly]);
        data.Winnings.Should().OnlyContain(winning => winning.FirstName == "Ada" && winning.LastName == "Lovelace");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoWinnings_FromAnotherLeague()
    {
        // Arrange
        var world = await ArrangeAsync();
        var otherLeagueId = await Seed.AddLeagueAsync(world.SeasonId, world.AdministratorId, "Other League");
        var settingId = await Seed.AddLeaguePrizeSettingAsync(otherLeagueId, PrizeType.Overall, 500m);
        await Seed.AddWinningAsync(world.AdministratorId, settingId, 500m);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert
        data!.Winnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnWhatHasBeenRecordedAsPaid()
    {
        // Arrange
        var world = await ArrangeAsync();
        var paidAtUtc = DateTime.UtcNow.AddDays(-1);
        await Seed.AddLeaguePayoutAsync(world.LeagueId, world.AdministratorId, 50m, paidAtUtc);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert - the recorded amount, which is how the handler spots that prizes moved after a payment.
        var stored = data!.StoredPayouts.Single();
        stored.UserId.Should().Be(world.AdministratorId);
        stored.TotalAmount.Should().Be(50m);
        stored.PaidAtUtc.Should().BeCloseTo(paidAtUtc, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAPayoutWithNoPaidDate()
    {
        // Arrange - a recorded intention to pay is not a payment, and the handler needs to be able to tell.
        var world = await ArrangeAsync();
        await Seed.AddLeaguePayoutAsync(world.LeagueId, world.AdministratorId, 50m, paidAtUtc: null);

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert
        data!.StoredPayouts.Single().PaidAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnBankDetailsForAWinner()
    {
        // Arrange
        var world = await ArrangeAsync();
        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Overall, 50m);
        await Seed.AddWinningAsync(world.AdministratorId, settingId, 50m);
        await Seed.AddUserPayoutDetailsAsync(world.AdministratorId, "A Lovelace", "00-00-00", "12345678");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert - as stored, still encrypted. Decrypting is the handler's job, after it has checked who is asking.
        var details = data!.BankDetails.Single();
        details.UserId.Should().Be(world.AdministratorId);
        details.EncryptedAccountName.Should().Be("A Lovelace");
        details.EncryptedSortCode.Should().Be("00-00-00");
        details.EncryptedAccountNumber.Should().Be("12345678");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnBankDetailsForSomeoneWhoHasWonNothingInThisLeague()
    {
        // Arrange - a player who has shared their details but won nothing here.
        var world = await ArrangeAsync();
        var strangerId = await Seed.AddUserAsync("Grace", "Hopper");
        await Seed.AddUserPayoutDetailsAsync(strangerId, "G Hopper", "11-11-11", "87654321");

        var settingId = await Seed.AddLeaguePrizeSettingAsync(world.LeagueId, PrizeType.Overall, 50m);
        await Seed.AddWinningAsync(world.AdministratorId, settingId, 50m);
        await Seed.AddUserPayoutDetailsAsync(world.AdministratorId, "A Lovelace", "00-00-00", "12345678");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert - the most sensitive table in the schema, so nobody else's rows may arrive at all.
        data!.BankDetails.Select(details => details.UserId).Should().Equal(world.AdministratorId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoBankDetails_WhenNothingHasBeenWon()
    {
        // Arrange
        var world = await ArrangeAsync();
        await Seed.AddUserPayoutDetailsAsync(world.AdministratorId, "A Lovelace", "00-00-00", "12345678");

        // Act
        var data = await Query.ExecuteAsync(world.LeagueId, world.AdministratorId, CancellationToken.None);

        // Assert
        data!.BankDetails.Should().BeEmpty();
    }

    private async Task<PayoutsWorld> ArrangeAsync()
    {
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        return new PayoutsWorld(leagueId, backdrop.SeasonId, backdrop.UserId);
    }

    private sealed record PayoutsWorld(int LeagueId, int SeasonId, string AdministratorId);
}
