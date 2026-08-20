using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Contracts.Admin.Users;
using ThePredictions.Contracts.Onboarding;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Users.Queries;

/// <summary>
/// The administrator's list of accounts.
///
/// Most of these tests are about the money and membership figures on that screen. Each was a correlated subquery with its
/// definition in a WHERE clause - what counts as spend, what counts as a league joined - and none of them had ever been
/// stated anywhere a person would read.
///
/// The rest are about the question the screen got wrong: it asked whether an account had a Season Pass, and said yes for
/// an account holding nothing but passes for seasons that finished a year ago. Whether a season has finished is settled
/// from the rounds that exist, so a season with rounds still to play is current and a season with none at all - one not
/// yet synced from the fixture provider - is also current, because it has not been played.
/// </summary>
public class GetAllUsersQueryHandlerTests
{
    private const string UserId = "user-me";

    private const int CurrentSeasonId = 3;
    private const int FinishedSeasonId = 1;

    private static readonly DateTime Awarded = new(2026, 5, 24, 17, 10, 0, DateTimeKind.Utc);

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
        Given(new AdminUsersData([], [], [], [], [], [], [], [], []));

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
            IsAdmin = true,
            CreatedAtUtc = new DateTime(2026, 8, 20, 21, 54, 37, DateTimeKind.Utc)
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
        user.CreatedAtUtc.Should().Be(new DateTime(2026, 8, 20, 21, 54, 37, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Handle_ShouldReportNoCreationDate_ForAnAccountThatPredatesTheColumn()
    {
        // Passed through as null rather than substituted. Accounts predating the column were dated by a one-off script
        // from their earliest provable activity, and the rest left null, so the screen has to be able to say it does not know.
        Given(Data(users: [User(UserId, "Ada", "Lovelace")]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.CreatedAtUtc.Should().BeNull();
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

    #region Consent

    [Fact]
    public async Task Handle_ShouldReportConsentAsGiven_WhenThereIsADateAgainstIt()
    {
        // Arrange
        Given(Data(users: [User(UserId, "Ada", "Lovelace") with
        {
            TermsAcceptedAtUtc = Awarded,
            MarketingOptInAtUtc = Awarded
        }]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.TermsAccepted.Should().BeTrue();
        user.MarketingOptIn.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReportNoConsentRecord_WhenThereIsNoDate()
    {
        // Accounts that predate the click-wrap wording on Register have no stored proof of consent, which is the one flag
        // on this screen with a legal consequence.
        Given(Data(users: [User(UserId, "Ada", "Lovelace")]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.TermsAccepted.Should().BeFalse();
        user.MarketingOptIn.Should().BeFalse();
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

    #region The leagues behind the counts

    [Fact]
    public async Task Handle_ShouldListTheMembershipsNewestSeasonFirstAndByNameWithinASeason()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues:
            [
                League(1, status: LeagueMemberStatus.Approved, seasonId: FinishedSeasonId, name: "Old League"),
                League(2, status: LeagueMemberStatus.Approved, seasonId: CurrentSeasonId, name: "Zebra League"),
                League(3, status: LeagueMemberStatus.Approved, seasonId: CurrentSeasonId, name: "Alpha League")
            ],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.Memberships.Select(membership => membership.LeagueName)
            .Should().Equal("Alpha League", "Zebra League", "Old League");
    }

    [Fact]
    public async Task Handle_ShouldNotListALeagueTheyRunButHaveNotJoinedAsAMembership()
    {
        // The schema allows administering a league without being in it, and that row has no status. Showing it as a
        // membership with a blank status would be inventing one.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues: [League(1, isAdministrator: true)],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.Memberships.Should().BeEmpty();
        user.AdministeredLeagues.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ShouldSayWhetherTheyPlayInALeagueTheyRun()
    {
        // Two rows for one league: one for running it, one for playing in it. Whether both exist is the question.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues:
            [
                League(1, isAdministrator: true, name: "Playing In It"),
                League(1, status: LeagueMemberStatus.Approved, name: "Playing In It"),
                League(2, isAdministrator: true, name: "Just Running It")
            ],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.AdministeredLeagues.Single(league => league.LeagueId == 1).AlsoPlaying.Should().BeTrue();
        user.AdministeredLeagues.Single(league => league.LeagueId == 2).AlsoPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldNotCountAPendingRequestAsPlayingInALeagueTheyRun()
    {
        // Defends the choice above: a request awaiting approval is not yet playing.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues:
            [
                League(1, isAdministrator: true),
                League(1, status: LeagueMemberStatus.Pending)
            ],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.AdministeredLeagues.Single().AlsoPlaying.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldCarryHowManyPeopleAreInALeagueTheyRun()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues: [League(1, isAdministrator: true, approvedMemberCount: 12)],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.AdministeredLeagues.Single().ApprovedMemberCount.Should().Be(12);
    }

    [Fact]
    public async Task Handle_ShouldCarryTheSeasonAndFeeOfEachMembership()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues: [League(1, status: LeagueMemberStatus.Pending, seasonId: CurrentSeasonId, price: 25m)],
            seasons: BothSeasons));

        // Act
        var membership = (await HandleAsync()).Single().Memberships.Single();

        // Assert - the fee is shown against a pending request too, struck through, because the fee is real and the point
        // is that this account has not paid it.
        membership.SeasonName.Should().Be("Premier League 2026/27");
        membership.IsCurrentSeason.Should().BeTrue();
        membership.Status.Should().Be(LeagueMemberStatus.Pending);
        membership.Price.Should().Be(25m);
    }

    #endregion

    #region Season passes

    [Fact]
    public async Task Handle_ShouldReportAPassForASeasonStillRunningAsACurrentPass()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            seasonPasses: [Pass(CurrentSeasonId, SeasonPassSource.Purchased, 10m)],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.HasCurrentSeasonPass.Should().BeTrue();
        user.HasEverHeldSeasonPass.Should().BeTrue();
        user.CurrentPassSeasonNames.Should().Equal("Premier League 2026/27");
    }

    [Fact]
    public async Task Handle_ShouldNotReportAPassForAFinishedSeasonAsACurrentPass()
    {
        // The bug this screen had. Every round of that season is complete, so the pass buys nothing now - and this is
        // exactly the account an administrator is looking for.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            seasonPasses: [Pass(FinishedSeasonId, SeasonPassSource.Purchased, 10m)],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.HasCurrentSeasonPass.Should().BeFalse();
        user.HasEverHeldSeasonPass.Should().BeTrue();
        user.HasEverPurchasedSeasonPass.Should().BeTrue();
        user.CurrentPassSeasonNames.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldTreatASeasonWithNoRoundsYetAsStillToCome()
    {
        // A season an administrator has created but the fixture provider has not filled in. It has not been played, so a
        // pass for it is a current pass - reading it as finished would hide everybody who has just signed up.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            seasonPasses: [Pass(9, SeasonPassSource.Purchased, 10m)],
            seasons: [new UserSeasonRow(9, "Premier League 2027/28", RoundCount: 0, CompletedRoundCount: 0)]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.HasCurrentSeasonPass.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldTreatASeasonWithARoundStillToPlayAsCurrent()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            seasonPasses: [Pass(9, SeasonPassSource.Purchased, 10m)],
            seasons: [new UserSeasonRow(9, "Half Done", RoundCount: 38, CompletedRoundCount: 37)]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.HasCurrentSeasonPass.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReportAPassAsNotCurrent_WhenNothingIsKnownAboutItsSeason()
    {
        // A foreign key makes this impossible in the database, but the read cannot promise it. Not current is the safe way
        // round: it under-reports a pass rather than telling an administrator somebody is covered when nothing confirms it.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            seasonPasses: [Pass(404, SeasonPassSource.Purchased, 10m)]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.HasCurrentSeasonPass.Should().BeFalse();
        user.SeasonPasses.Single().SeasonName.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReportATrialAsAPassButNotAsAPurchase()
    {
        // The distinction the two pills exist for: an account can hold a current pass and never have paid a penny.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            seasonPasses: [Pass(CurrentSeasonId, SeasonPassSource.Trial)],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.HasCurrentSeasonPass.Should().BeTrue();
        user.HasEverHeldSeasonPass.Should().BeTrue();
        user.HasEverPurchasedSeasonPass.Should().BeFalse();
        user.SeasonPassSpend.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_ShouldCountWhatTheyPaidForPassesIncludingTheTextMessageFee()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            seasonPasses:
            [
                Pass(CurrentSeasonId, SeasonPassSource.Purchased, 10m, smsFeePaid: 2m),
                Pass(FinishedSeasonId, SeasonPassSource.Purchased, 12m)
            ],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.SeasonPassSpend.Should().Be(24m);
    }

    [Fact]
    public async Task Handle_ShouldCarryTheTierAndPurchaseDateOfEachPass()
    {
        // Both are shown in the popup, and the tier is the one field on a pass that is stored as text and read back as an
        // enum - so a rename on either side would break it, silently, at runtime.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            seasonPasses: [PremiumPass(CurrentSeasonId)],
            seasons: BothSeasons));

        // Act
        var pass = (await HandleAsync()).Single().SeasonPasses.Single();

        // Assert
        pass.Tier.Should().Be(SeasonPassTier.Premium);
        pass.CreatedAtUtc.Should().Be(Awarded);
        pass.TotalPaid.Should().Be(12m);
    }

    [Fact]
    public async Task Handle_ShouldListPassesNewestSeasonFirst()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            seasonPasses: [Pass(FinishedSeasonId, SeasonPassSource.Free), Pass(CurrentSeasonId, SeasonPassSource.Purchased, 10m)],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.SeasonPasses.Select(pass => pass.SeasonId).Should().Equal(CurrentSeasonId, FinishedSeasonId);
    }

    [Fact]
    public async Task Handle_ShouldReportNoPass_ForAnAccountWithNone()
    {
        // Arrange
        Given(Data(users: [User(UserId, "Ada", "Lovelace")]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.HasEverHeldSeasonPass.Should().BeFalse();
        user.HasCurrentSeasonPass.Should().BeFalse();
        user.HasEverPurchasedSeasonPass.Should().BeFalse();
        user.SeasonPassSpend.Should().Be(0m);
        user.IsDormant.Should().BeTrue();
    }

    #endregion

    #region Winnings

    [Fact]
    public async Task Handle_ShouldTotalTheirWinnings()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace"), User("u2", "Grace", "Hopper")],
            winnings: [Winning(25m), Winning(10m), Winning(5m, userId: "u2")],
            seasons: BothSeasons));

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
        user.Prizes.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldListPrizesNewestSeasonFirstAndBiggestWithinASeason()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            winnings:
            [
                Winning(5m, seasonId: CurrentSeasonId),
                Winning(90m, seasonId: FinishedSeasonId),
                Winning(25m, seasonId: CurrentSeasonId)
            ],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.Prizes.Select(prize => prize.Amount).Should().Equal(25m, 5m, 90m);
    }

    [Fact]
    public async Task Handle_ShouldNameEachPrize()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            winnings: [Winning(4m, prizeType: PrizeType.Round, roundNumber: 21)],
            seasons: BothSeasons));

        // Act
        var prize = (await HandleAsync()).Single().Prizes.Single();

        // Assert
        prize.Title.Should().Be("Round 21 winner");
        prize.LeagueName.Should().Be("The League");
    }

    #endregion

    #region Payout details

    [Fact]
    public async Task Handle_ShouldReportThatAnAccountHasSavedPayoutDetails()
    {
        // Whether a row exists, never what is in it - the three account columns are encrypted and this screen is not
        // allowed to decrypt them.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            userIdsWithPayoutDetails: [UserId]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.HasPayoutDetails.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldNotGiveOneAccountsPayoutDetailsToAnother()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace"), User("u2", "Grace", "Hopper")],
            userIdsWithPayoutDetails: ["u2"]));

        // Act
        var users = await HandleAsync();

        // Assert
        users.Single(user => user.Id == UserId).HasPayoutDetails.Should().BeFalse();
        users.Single(user => user.Id == "u2").HasPayoutDetails.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldFlagAWinnerWithNoWayToBePaid()
    {
        // The one combination on this screen worth acting on the same day.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            winnings: [Winning(290m)],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.IsOwedMoneyWithNowhereToSendIt.Should().BeTrue();
    }

    #endregion

    #region Badges

    [Fact]
    public async Task Handle_ShouldNameEachBadgeFromTheCatalogue()
    {
        // The database has never stored a badge name - they live in the catalogue in code.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            badges: [new UserBadgeRow(UserId, "sharpshooter-2", "4 in a round", Awarded, null)]));

        // Act
        var badge = (await HandleAsync()).Single().Badges.Single();

        // Assert
        badge.Name.Should().Be("Sharpshooter");
        badge.Detail.Should().Be("4 in a round");
        badge.SeasonName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldFallBackToTheStoredKey_ForABadgeTheCatalogueNoLongerDefines()
    {
        // Badges are defined in code and earned rows outlive the definition. The raw key is ugly and it is the truth.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            badges: [new UserBadgeRow(UserId, "retired-badge", null, Awarded, null)]));

        // Act
        var badge = (await HandleAsync()).Single().Badges.Single();

        // Assert
        badge.Name.Should().Be("retired-badge");
    }

    [Fact]
    public async Task Handle_ShouldNameTheSeasonOfASeasonScopedBadge()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            badges: [new UserBadgeRow(UserId, "ever-present", null, Awarded, FinishedSeasonId)],
            seasons: BothSeasons));

        // Act
        var badge = (await HandleAsync()).Single().Badges.Single();

        // Assert
        badge.SeasonName.Should().Be("Premier League 2025/26");
    }

    [Fact]
    public async Task Handle_ShouldListBadgesMostRecentFirst()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            badges:
            [
                new UserBadgeRow(UserId, "veteran", null, Awarded.AddDays(-10), null),
                new UserBadgeRow(UserId, "champion", null, Awarded, null)
            ]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.Badges.Select(badge => badge.BadgeKey).Should().Equal("champion", "veteran");
        user.BadgeCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldShowARepeatableBadgeOnce_WhenItHasBeenWonSeveralTimes()
    {
        // A badge won every round has a row per round, and counting those rows reported more badges than the catalogue
        // defines. The badge is one badge however many times it has been won.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            badges:
            [
                new UserBadgeRow(UserId, "round-winner", "Round 4", Awarded.AddDays(-20), null),
                new UserBadgeRow(UserId, "round-winner", "Round 9", Awarded.AddDays(-5), null),
                new UserBadgeRow(UserId, "round-winner", "Round 12", Awarded, null)
            ]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.Badges.Should().ContainSingle().Which.BadgeKey.Should().Be("round-winner");
        user.BadgeCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldShowTheFirstTimeARepeatableBadgeWasEarned()
    {
        // The date, the detail and the season all describe one occasion, so they come from the same one - the first.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            badges:
            [
                new UserBadgeRow(UserId, "ever-present", "Later", Awarded, CurrentSeasonId),
                new UserBadgeRow(UserId, "ever-present", "First", Awarded.AddDays(-30), FinishedSeasonId)
            ],
            seasons: BothSeasons));

        // Act
        var badge = (await HandleAsync()).Single().Badges.Single();

        // Assert
        badge.AwardedUtc.Should().Be(Awarded.AddDays(-30));
        badge.Detail.Should().Be("First");
        badge.SeasonName.Should().Be("Premier League 2025/26");
    }

    [Fact]
    public async Task Handle_ShouldNotGiveOneAccountsBadgesToAnother()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace"), User("u2", "Grace", "Hopper")],
            badges: [new UserBadgeRow("u2", "champion", null, Awarded, null)]));

        // Act
        var users = await HandleAsync();

        // Assert
        users.Single(user => user.Id == UserId).Badges.Should().BeEmpty();
        users.Single(user => user.Id == "u2").Badges.Should().HaveCount(1);
    }

    #endregion

    #region Onboarding

    [Fact]
    public async Task Handle_ShouldReportEverySetupStepAsDone_ForAFullySetUpAccount()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace") with { PhoneNumber = "07700900000" }],
            leagues: [League(1, status: LeagueMemberStatus.Approved)],
            seasonPasses: [Pass(CurrentSeasonId, SeasonPassSource.Purchased, 10m)],
            userIdsWithPayoutDetails: [UserId],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.OnboardingStepCount.Should().Be(4);
        user.OnboardingStepsCompleted.Should().Be(4);
        user.OnboardingComplete.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldLockJoiningALeagueBehindGettingAPass()
    {
        // An account that registered and stopped. The second step is not merely outstanding, it is blocked.
        Given(Data(users: [User(UserId, "Ada", "Lovelace")]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.OnboardingStepsCompleted.Should().Be(0);
        StateOf(user, OnboardingStepKeys.GetPass).Should().Be(OnboardingStepStates.Active);
        StateOf(user, OnboardingStepKeys.JoinLeague).Should().Be(OnboardingStepStates.Locked);
    }

    [Fact]
    public async Task Handle_ShouldReportAStepTheAccountDismissedAsSkipped()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            onboardingSkips: [new UserOnboardingSkipRow(UserId, OnboardingStepKeys.AddPayoutDetails)]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        StateOf(user, OnboardingStepKeys.AddPayoutDetails).Should().Be(OnboardingStepStates.Skipped);
    }

    [Fact]
    public async Task Handle_ShouldReportADismissedStepAsDone_WhenTheAccountDidItAnyway()
    {
        // Three dev accounts dismissed the payout-details prompt and then added payout details. The data is the authority;
        // the skip is only a record of them dismissing the prompt.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            userIdsWithPayoutDetails: [UserId],
            onboardingSkips: [new UserOnboardingSkipRow(UserId, OnboardingStepKeys.AddPayoutDetails)]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        StateOf(user, OnboardingStepKeys.AddPayoutDetails).Should().Be(OnboardingStepStates.Completed);
    }

    [Fact]
    public async Task Handle_ShouldNotApplyOneAccountsSkipToAnother()
    {
        // Arrange
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace"), User("u2", "Grace", "Hopper")],
            onboardingSkips: [new UserOnboardingSkipRow("u2", OnboardingStepKeys.AddMobile)]));

        // Act
        var users = await HandleAsync();

        // Assert
        StateOf(users.Single(user => user.Id == UserId), OnboardingStepKeys.AddMobile).Should().Be(OnboardingStepStates.Active);
        StateOf(users.Single(user => user.Id == "u2"), OnboardingStepKeys.AddMobile).Should().Be(OnboardingStepStates.Skipped);
    }

    [Fact]
    public async Task Handle_ShouldCountAPendingRequestTowardsHavingJoinedALeagueForSetup()
    {
        // What the dashboard checklist has always counted, and it is right: somebody whose request is awaiting approval
        // has done their part of joining a league.
        Given(Data(
            users: [User(UserId, "Ada", "Lovelace")],
            leagues: [League(1, status: LeagueMemberStatus.Pending)],
            seasonPasses: [Pass(CurrentSeasonId, SeasonPassSource.Purchased, 10m)],
            seasons: BothSeasons));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        user.LeaguesJoinedApproved.Should().Be(0);
        StateOf(user, OnboardingStepKeys.JoinLeague).Should().Be(OnboardingStepStates.Completed);
    }

    [Fact]
    public async Task Handle_ShouldNotCountABlankPhoneNumberAsAMobileNumber()
    {
        // Arrange
        Given(Data(users: [User(UserId, "Ada", "Lovelace") with { PhoneNumber = "   " }]));

        // Act
        var user = (await HandleAsync()).Single();

        // Assert
        StateOf(user, OnboardingStepKeys.AddMobile).Should().Be(OnboardingStepStates.Active);
    }

    #endregion

    private static string StateOf(UserDto user, string stepKey) =>
        user.Onboarding.Steps.Single(step => step.Key == stepKey).State;

    private void Given(AdminUsersData data) =>
        _adminUsersQuery.ExecuteAsync(Arg.Any<CancellationToken>()).Returns(data);

    /// <summary>One season that has run its course and one that has not, which is what "current" turns on.</summary>
    private static UserSeasonRow[] BothSeasons =>
    [
        new(FinishedSeasonId, "Premier League 2025/26", RoundCount: 38, CompletedRoundCount: 38),
        new(CurrentSeasonId, "Premier League 2026/27", RoundCount: 38, CompletedRoundCount: 0)
    ];

    private static AdminUsersData Data(
        AdminUserRow[]? users = null,
        UserLoginProviderRow[]? loginProviders = null,
        UserLeagueRow[]? leagues = null,
        UserSeasonPassRow[]? seasonPasses = null,
        UserWinningRow[]? winnings = null,
        UserSeasonRow[]? seasons = null,
        string[]? userIdsWithPayoutDetails = null,
        UserOnboardingSkipRow[]? onboardingSkips = null,
        UserBadgeRow[]? badges = null) =>
        new(users ?? [], loginProviders ?? [], leagues ?? [], seasonPasses ?? [], winnings ?? [],
            seasons ?? [], userIdsWithPayoutDetails ?? [], onboardingSkips ?? [], badges ?? []);

    private static AdminUserRow User(string id, string? firstName, string? lastName, DateTime? createdAtUtc = null) =>
        new(id, firstName, lastName, $"{id}@example.com", PhoneNumber: null,
            EmailConfirmed: false, HasPassword: true, IsAdmin: false,
            TermsAcceptedAtUtc: null, MarketingOptInAtUtc: null, CreatedAtUtc: createdAtUtc);

    private static UserLeagueRow League(
        int leagueId,
        bool isAdministrator = false,
        LeagueMemberStatus? status = null,
        decimal price = 0m,
        bool isFree = false,
        string userId = UserId,
        int seasonId = CurrentSeasonId,
        string name = "The League",
        int approvedMemberCount = 0) =>
        new(userId, leagueId, name, seasonId, isAdministrator, status, isFree, price, approvedMemberCount);

    private static UserSeasonPassRow Pass(
        int seasonId,
        SeasonPassSource source,
        decimal amountPaid = 0m,
        decimal smsFeePaid = 0m) =>
        new(UserId, seasonId, SeasonPassTier.Standard, source, amountPaid, smsFeePaid, Awarded);

    /// <summary>A Premium pass with a text-message fee on it, which is the only shape that exercises both.</summary>
    private static UserSeasonPassRow PremiumPass(int seasonId) =>
        new(UserId, seasonId, SeasonPassTier.Premium, SeasonPassSource.Purchased, 10m, 2m, Awarded);

    private static UserWinningRow Winning(
        decimal amount,
        string userId = UserId,
        int seasonId = CurrentSeasonId,
        PrizeType prizeType = PrizeType.Overall,
        int? roundNumber = null) =>
        new(userId, 1, "The League", seasonId, prizeType, Stage: null, roundNumber, Month: null, amount, Awarded);

    private Task<IEnumerable<UserDto>> HandleAsync() =>
        _handler.Handle(new GetAllUsersQuery(), CancellationToken.None);
}
