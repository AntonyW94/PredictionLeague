using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Users.Queries;

/// <summary>
/// The administrator's list of accounts.
///
/// Most of these tests are about the money and membership figures on that screen. Each was a correlated subquery with its
/// definition in a WHERE clause - what counts as spend, what counts as a league joined - and none of them had ever been
/// stated anywhere a person would read.
/// </summary>
public class GetAllUsersQueryHandlerTests
{
    private const string UserId = "user-me";

    private readonly IAdminUsersQuery _adminUsersQuery = Substitute.For<IAdminUsersQuery>();
    private readonly GetAllUsersQueryHandler _handler;

    public GetAllUsersQueryHandlerTests()
    {
        _handler = new GetAllUsersQueryHandler(_adminUsersQuery);
    }

    #region The accounts

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenThereAreNoAccounts()
    {
        // Arrange
        Given(new AdminUsersData([], [], [], [], []));

        // Act
        var users = await HandleAsync();

        // Assert
        users.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldListAccountsByFullName()
    {
        // Arrange
        Given(Data(users: [User("u1", "Wanda", "Zeta"), User("u2", "Ada", "Lovelace")]));

        // Act
        var users = await HandleAsync();

        // Assert
        users.Select(user => user.FullName).Should().Equal("Ada Lovelace", "Wanda Zeta");
    }

    [Fact]
    public async Task Handle_ShouldNameAccountsInFullRatherThanTheWayPlayersSeeEachOther()
    {
        // This screen exists to tell accounts apart, so it is "Ada Lovelace" rather than the "Ada L" every player-facing
        // screen shows.
        Given(Data(users: [User(UserId, "Ada", "Lovelace")]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.FullName.Should().Be("Ada Lovelace");
    }

    [Fact]
    public async Task Handle_ShouldReportEachAccountsDetails()
    {
        // Arrange
        Given(Data(users: [User(UserId, "Ada", "Lovelace") with
        {
            Email = "ada@example.com",
            PhoneNumber = "07700900000",
            EmailConfirmed = true,
            HasPassword = true,
            IsAdmin = true
        }]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.Id.Should().Be(UserId);
        user.Email.Should().Be("ada@example.com");
        user.PhoneNumber.Should().Be("07700900000");
        user.EmailConfirmed.Should().BeTrue();
        user.HasLocalPassword.Should().BeTrue();
        user.IsAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReportAnAccountWithNoPasswordOfItsOwn()
    {
        // A social-only sign-in. The hash itself never leaves the database; only whether one exists.
        Given(Data(users: [User(UserId, "Ada", "Lovelace") with { HasPassword = false }]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.HasLocalPassword.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldReportAnAccountThatNeverFinishedSigningUp()
    {
        // Arrange
        Given(Data(users: [User(UserId, null, null)]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert - an empty name rather than a crash, so the administrator can still see the account exists.
        user.FullName.Should().BeEmpty();
    }

    #endregion

    #region Social sign-ins

    [Fact]
    public async Task Handle_ShouldListEverySocialSignInSeparately()
    {
        // The statement this replaces joined these into one comma-separated string and the handler split it back apart.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            loginProviders: [new UserLoginProviderRow(UserId, "Google"), new UserLoginProviderRow(UserId, "Facebook")]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.SocialProviders.Should().BeEquivalentTo(["Google", "Facebook"]);
    }

    [Fact]
    public async Task Handle_ShouldReportNoSocialSignIns_ForAnAccountWithNone()
    {
        // Arrange
        Given(Data(users: [User(UserId, "Ada", "Lovelace")]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.SocialProviders.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldNotGiveOneAccountsSocialSignInToAnother()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace"), User("u2", "Grace", "Hopper")],
            loginProviders: [new UserLoginProviderRow("u2", "Google")]));

        // Act
        var users = await HandleAsync();

        // Assert
        users.Single(user => user.Id == UserId).SocialProviders.Should().BeEmpty();
        users.Single(user => user.Id == "u2").SocialProviders.Should().Equal("Google");
    }

    #endregion

    #region Leagues

    [Fact]
    public async Task Handle_ShouldCountLeaguesCreatedJoinedAndStillPending()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues:
            [
                League(1, isAdministrator: true),
                League(2, status: LeagueMemberStatus.Approved),
                League(3, status: LeagueMemberStatus.Approved),
                League(4, status: LeagueMemberStatus.Pending),
                League(5, status: LeagueMemberStatus.Rejected)
            ]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert - a rejected request is neither joined nor pending.
        user.LeaguesCreated.Should().Be(1);
        user.LeaguesJoinedApproved.Should().Be(2);
        user.LeaguesJoinedPending.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldNotCountAnotherAccountsLeagues()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace"), User("u2", "Grace", "Hopper")],
            leagues: [League(1, status: LeagueMemberStatus.Approved, userId: "u2")]));

        // Act
        var users = await HandleAsync();

        // Assert
        users.Single(user => user.Id == UserId).LeaguesJoinedApproved.Should().Be(0);
        users.Single(user => user.Id == "u2").LeaguesJoinedApproved.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldCountWhatTheyHavePaidToEnterLeagues()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues:
            [
                League(1, status: LeagueMemberStatus.Approved, price: 10m),
                League(2, status: LeagueMemberStatus.Approved, price: 15m)
            ]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.LeagueEntrySpend.Should().Be(25m);
    }

    [Fact]
    public async Task Handle_ShouldNotCountALeagueTheyWereNeverAcceptedInto()
    {
        // A request that was never accepted was never paid for.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues:
            [
                League(1, status: LeagueMemberStatus.Pending, price: 10m),
                League(2, status: LeagueMemberStatus.Rejected, price: 10m)
            ]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.LeagueEntrySpend.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ShouldNotCountAFreeLeagueAsMoneySpent()
    {
        // Two ways of saying the same thing that the data does not guarantee agree, so both are checked.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues:
            [
                League(1, status: LeagueMemberStatus.Approved, price: 10m, isFree: true),
                League(2, status: LeagueMemberStatus.Approved, price: 0m)
            ]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.LeagueEntrySpend.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ShouldNotSubtractALeagueWithANegativePrice()
    {
        // The reason the price check is there as well as the free-league check. A negative price is not a state the
        // application creates, but it is one the column allows, and a bad row must not reduce what somebody has spent.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues:
            [
                League(1, status: LeagueMemberStatus.Approved, price: 10m),
                League(2, status: LeagueMemberStatus.Approved, price: -5m)
            ]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.LeagueEntrySpend.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_ShouldNotCountALeagueTheyRunButHaveNotJoined()
    {
        // Administering a league is not entering it. The row for that has no membership status at all.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues: [League(1, isAdministrator: true, price: 10m)]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.LeagueEntrySpend.Should().Be(0m);
        user.LeaguesCreated.Should().Be(1);
    }

    #endregion

    #region Season passes and winnings

    [Fact]
    public async Task Handle_ShouldReportThatTheyHoldASeasonPass()
    {
        // Arrange
        Given(Data(users: [User(UserId, "Ada", "Lovelace")], seasonPasses: [Pass(SeasonPassSource.Purchased, 10m)]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.HasSeasonPass.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldCountATrialAsHoldingAPassButNotAsMoneySpent()
    {
        // A trial or a pass handed out by an administrator is still a pass, and is still not money anybody spent.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            seasonPasses: [Pass(SeasonPassSource.Trial, 10m)]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.HasSeasonPass.Should().BeTrue();
        user.SeasonPassSpend.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ShouldCountWhatTheyPaidForPassesIncludingTheTextMessageFee()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            seasonPasses: [Pass(SeasonPassSource.Purchased, 10m, smsFeePaid: 2m), Pass(SeasonPassSource.Purchased, 12m)]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.SeasonPassSpend.Should().Be(24m);
    }

    [Fact]
    public async Task Handle_ShouldReportNoPass_ForAnAccountWithNone()
    {
        // Arrange
        Given(Data(users: [User(UserId, "Ada", "Lovelace")]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.HasSeasonPass.Should().BeFalse();
        user.SeasonPassSpend.Should().Be(0m);
        user.IsDormant.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldTotalTheirWinnings()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace"), User("u2", "Grace", "Hopper")],
            winnings: [new UserWinningRow(UserId, 25m), new UserWinningRow(UserId, 10m), new UserWinningRow("u2", 5m)]));

        // Act
        var users = await HandleAsync();

        // Assert
        users.Single(user => user.Id == UserId).TotalWinnings.Should().Be(35m);
        users.Single(user => user.Id == "u2").TotalWinnings.Should().Be(5m);
    }

    [Fact]
    public async Task Handle_ShouldReportNoWinnings_ForAnAccountThatHasWonNothing()
    {
        // Arrange
        Given(Data(users: [User(UserId, "Ada", "Lovelace")]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.TotalWinnings.Should().Be(0m);
    }

    #endregion

    private void Given(AdminUsersData data) =>
        _adminUsersQuery.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(data);

    private static AdminUsersData Data(
        AdminUserRow[]? users = null,
        UserLoginProviderRow[]? loginProviders = null,
        UserLeagueRow[]? leagues = null,
        UserSeasonPassRow[]? seasonPasses = null,
        UserWinningRow[]? winnings = null) =>
        new(users ?? [], loginProviders ?? [], leagues ?? [], seasonPasses ?? [], winnings ?? []);

    private static AdminUserRow User(string id, string? firstName, string? lastName) =>
        new(id, firstName, lastName, $"{id}@example.com", PhoneNumber: null,
            EmailConfirmed: false, HasPassword: true, IsAdmin: false);

    private static UserLeagueRow League(
        int leagueId,
        bool isAdministrator = false,
        LeagueMemberStatus? status = null,
        decimal price = 0m,
        bool isFree = false,
        string userId = UserId) =>
        new(userId, leagueId, isAdministrator, status, isFree, price);

    private static UserSeasonPassRow Pass(SeasonPassSource source, decimal amountPaid, decimal smsFeePaid = 0m) =>
        new(UserId, source, amountPaid, smsFeePaid);

    private Task<IEnumerable<UserDto>> HandleAsync() =>
        _handler.Handle(new GetAllUsersQuery(), CancellationToken.None);
}
