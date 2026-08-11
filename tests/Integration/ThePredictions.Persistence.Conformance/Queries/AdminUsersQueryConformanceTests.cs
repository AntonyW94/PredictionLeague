using FluentAssertions;
using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="IAdminUsersQuery"/> implementation must return: the accounts, and the memberships, passes and
/// winnings behind the figures on the administrator's list - with none of the counting or summing done.
/// </summary>
public abstract class AdminUsersQueryConformanceTests
{
    protected abstract IAdminUsersQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    #region The accounts

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNothing_WhenThereAreNoAccounts()
    {
        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert
        data.Users.Should().BeEmpty();
        data.Leagues.Should().BeEmpty();
        data.SeasonPasses.Should().BeEmpty();
        data.Winnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnBothNamePartsAndTheContactDetails()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var user = (await Query.ExecuteAsync(CancellationToken.None)).Users.Single();

        // Assert - composing the name is a rule, so the parts arrive raw.
        user.Id.Should().Be(backdrop.UserId);
        user.FirstName.Should().Be("Ada");
        user.LastName.Should().Be("Lovelace");
        user.Email.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSayWhetherAnAccountHasAPasswordWithoutReturningIt()
    {
        // Arrange
        await Seed.AddBackdropAsync();

        // Act
        var user = (await Query.ExecuteAsync(CancellationToken.None)).Users.Single();

        // Assert - a flag, never the hash. The screen needs to know an account can sign in without a social provider; it
        // has no business holding the credential.
        user.HasPassword.Should().BeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryAccount()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert - this list is not filtered: an account that never finished signing up is one an administrator most
        // wants to see.
        data.Users.Select(user => user.Id).Should().BeEquivalentTo([backdrop.UserId, otherUserId]);
    }

    #endregion

    #region Leagues

    [Fact]
    public async Task ExecuteAsync_ShouldReturnALeagueTheAccountAdministers()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);

        // Act
        var leagues = (await Query.ExecuteAsync(CancellationToken.None)).Leagues;

        // Assert - administering a league is not the same as entering it, so the row carries no membership status.
        var administered = leagues.Single(league => league.IsAdministrator);
        administered.UserId.Should().Be(backdrop.UserId);
        administered.LeagueId.Should().Be(leagueId);
        administered.Status.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnAMembershipWithItsStatusAndTheLeaguesPrice()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, otherUserId, LeagueMemberStatus.Pending);

        // Act
        var leagues = (await Query.ExecuteAsync(CancellationToken.None)).Leagues;

        // Assert - what counts as money spent on league entry is a rule, so the price and the free flag both arrive.
        var membership = leagues.Single(league => league.UserId == otherUserId && !league.IsAdministrator);
        membership.LeagueId.Should().Be(leagueId);
        membership.Status.Should().Be(LeagueMemberStatus.Pending);
        membership.Price.Should().BeGreaterThanOrEqualTo(0m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnBothRows_WhenAnAccountAdministersALeagueAndIsInIt()
    {
        // Arrange - the common case: whoever sets a league up usually plays in it too.
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        // Act
        var leagues = (await Query.ExecuteAsync(CancellationToken.None)).Leagues
            .Where(league => league.UserId == backdrop.UserId)
            .ToList();

        // Assert - one row for running it and one for playing in it, because they count towards different figures.
        leagues.Should().HaveCount(2);
        leagues.Should().ContainSingle(league => league.IsAdministrator);
        leagues.Should().ContainSingle(league => !league.IsAdministrator && league.Status == LeagueMemberStatus.Approved);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnARejectedMembership()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, otherUserId, LeagueMemberStatus.Rejected);

        // Act
        var leagues = (await Query.ExecuteAsync(CancellationToken.None)).Leagues;

        // Assert - whether a rejected request counts as a league joined is a rule, and the read must not settle it.
        leagues.Should().ContainSingle(league => league.UserId == otherUserId && league.Status == LeagueMemberStatus.Rejected);
    }

    #endregion

    #region Season passes and winnings

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachPassWithItsSourceAndWhatWasPaid()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId, SeasonPassTier.Standard, SeasonPassSource.Trial);

        // Act
        var pass = (await Query.ExecuteAsync(CancellationToken.None)).SeasonPasses.Single();

        // Assert - the source arrives because a trial is still a pass but is not money spent, and that distinction was a
        // condition inside a sum.
        pass.UserId.Should().Be(backdrop.UserId);
        pass.Source.Should().Be(SeasonPassSource.Trial);
        pass.AmountPaid.Should().Be(0m);
        pass.SmsFeePaid.Should().Be(0m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryPassAnAccountHolds()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var secondSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);
        await Seed.AddSeasonPassAsync(backdrop.UserId, secondSeasonId);

        // Act
        var passes = (await Query.ExecuteAsync(CancellationToken.None)).SeasonPasses;

        // Assert - summing them is the handler's job, so both rows arrive.
        passes.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachPrizeWonRatherThanATotal()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        var prizeSettingId = await Seed.AddLeaguePrizeSettingAsync(leagueId, PrizeType.Round, 25m);

        await Seed.AddWinningAsync(backdrop.UserId, prizeSettingId, 25m);
        await Seed.AddWinningAsync(backdrop.UserId, prizeSettingId, 10m, roundNumber: 2);

        // Act
        var winnings = (await Query.ExecuteAsync(CancellationToken.None)).Winnings;

        // Assert
        winnings.Should().HaveCount(2);
        winnings.Select(winning => winning.Amount).Should().BeEquivalentTo([25m, 10m]);
        winnings.Should().OnlyContain(winning => winning.UserId == backdrop.UserId);
    }

    #endregion
}
