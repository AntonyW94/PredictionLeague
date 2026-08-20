using FluentAssertions;
using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Contracts.Onboarding;
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
        data.Seasons.Should().BeEmpty();
        data.UserIdsWithPayoutDetails.Should().BeEmpty();
        data.OnboardingSkips.Should().BeEmpty();
        data.Badges.Should().BeEmpty();
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

        // The name and season come back so the popup behind the count can group and label the leagues without a second read.
        membership.LeagueName.Should().NotBeNullOrWhiteSpace();
        membership.SeasonId.Should().Be(backdrop.SeasonId);
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
        pass.SeasonId.Should().Be(backdrop.SeasonId);
        pass.Tier.Should().Be(SeasonPassTier.Standard);
        pass.CreatedAtUtc.Should().NotBe(default);
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
        winnings.Should().OnlyContain(winning => winning.LeagueId == leagueId);
        winnings.Should().OnlyContain(winning => winning.SeasonId == backdrop.SeasonId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnWhatScopesEachPrizeSoItCanBeNamed()
    {
        // Naming a prize is a rule with several cases, and the pieces live in two tables: the type and the tournament stage
        // on the prize setting, the round number and the month on the winning itself. Both are joined so the handler can
        // name it without a second read.
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        var prizeSettingId = await Seed.AddLeaguePrizeSettingAsync(leagueId, PrizeType.Monthly, 10m);

        await Seed.AddWinningAsync(backdrop.UserId, prizeSettingId, 12.50m, month: 11);

        // Act
        var winning = (await Query.ExecuteAsync(CancellationToken.None)).Winnings.Single();

        // Assert
        winning.PrizeType.Should().Be(PrizeType.Monthly);
        winning.Month.Should().Be(11);
        winning.RoundNumber.Should().BeNull();
        winning.LeagueName.Should().NotBeNullOrWhiteSpace();
        winning.AwardedDateUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnWhatWasActuallyPaidForAPurchasedPass()
    {
        // What an account has spent on passes counts purchased rows only, and the text-message uplift is a separate column
        // because it is priced separately. Both arrive; adding them is the handler's job.
        var backdrop = await Seed.AddBackdropAsync();

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId,
            SeasonPassTier.Premium, SeasonPassSource.Purchased, amountPaid: 10m, smsFeePaid: 2m);

        // Act
        var pass = (await Query.ExecuteAsync(CancellationToken.None)).SeasonPasses.Single();

        // Assert
        pass.Tier.Should().Be(SeasonPassTier.Premium);
        pass.Source.Should().Be(SeasonPassSource.Purchased);
        pass.AmountPaid.Should().Be(10m);
        pass.SmsFeePaid.Should().Be(2m);
    }

    #endregion

    #region Seasons

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachSeasonWithHowManyRoundsItHoldsAndHowManyAreComplete()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow.AddDays(-7), RoundStatus.Completed);
        await Seed.AddRoundAsync(backdrop.SeasonId, 2, DateTime.UtcNow.AddDays(-1), RoundStatus.Completed);
        await Seed.AddRoundAsync(backdrop.SeasonId, 3, DateTime.UtcNow.AddDays(7));

        // Act
        var season = (await Query.ExecuteAsync(CancellationToken.None)).Seasons.Single(s => s.SeasonId == backdrop.SeasonId);

        // Assert - both counts, never a finished flag. Whether a season has run its course is a rule the whole application
        // shares, and settling it in the read would make a second definition of it.
        season.RoundCount.Should().Be(3);
        season.CompletedRoundCount.Should().Be(2);
        season.Name.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnASeasonThatHasNoRoundsYet()
    {
        // A season an administrator has created before the fixture provider has filled it in. It must come back with a
        // round count of nought rather than being dropped - a pass for it is a pass for a season still to come, and an
        // inner join here would hide everybody who has just signed up for it.
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var season = (await Query.ExecuteAsync(CancellationToken.None)).Seasons.Single(s => s.SeasonId == backdrop.SeasonId);

        // Assert
        season.RoundCount.Should().Be(0);
        season.CompletedRoundCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotCountOneSeasonsRoundsAgainstAnother()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow, RoundStatus.Completed);

        // Act
        var seasons = (await Query.ExecuteAsync(CancellationToken.None)).Seasons;

        // Assert
        seasons.Single(s => s.SeasonId == backdrop.SeasonId).RoundCount.Should().Be(1);
        seasons.Single(s => s.SeasonId == otherSeasonId).RoundCount.Should().Be(0);
    }

    #endregion

    #region Consent

    [Fact]
    public async Task ExecuteAsync_ShouldReturnWhenTheAccountAcceptedTheTermsAndOptedInToMarketing()
    {
        // Arrange
        var acceptedAt = new DateTime(2026, 4, 28, 9, 30, 0, DateTimeKind.Utc);
        var optedInAt = new DateTime(2026, 5, 1, 11, 0, 0, DateTimeKind.Utc);

        var userId = await Seed.AddUserAsync("Ada", "Lovelace",
            termsAcceptedAtUtc: acceptedAt, marketingOptInAtUtc: optedInAt);

        // Act
        var user = (await Query.ExecuteAsync(CancellationToken.None)).Users.Single(u => u.Id == userId);

        // Assert - the dates, not flags. Turning them into a tick is the handler's job, and the date is the part that would
        // answer a subject access request.
        user.TermsAcceptedAtUtc.Should().Be(acceptedAt);
        user.MarketingOptInAtUtc.Should().Be(optedInAt);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoConsentDates_ForAnAccountThatHasNeitherRecord()
    {
        // Arrange
        var userId = await Seed.AddUserAsync("Grace", "Hopper");

        // Act
        var user = (await Query.ExecuteAsync(CancellationToken.None)).Users.Single(u => u.Id == userId);

        // Assert - a real state, not an unset default: accounts predating the click-wrap wording have no stored proof.
        user.TermsAcceptedAtUtc.Should().BeNull();
        user.MarketingOptInAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheMobileNumberAsStored()
    {
        // Arrange
        var userId = await Seed.AddUserAsync("Ada", "Lovelace", phoneNumber: "07700900000");

        // Act
        var user = (await Query.ExecuteAsync(CancellationToken.None)).Users.Single(u => u.Id == userId);

        // Assert
        user.PhoneNumber.Should().Be("07700900000");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnWhenTheAccountWasCreated()
    {
        // Arrange
        var createdAt = new DateTime(2026, 8, 20, 21, 54, 37, DateTimeKind.Utc);
        var userId = await Seed.AddUserAsync("Ada", "Lovelace", createdAtUtc: createdAt);

        // Act
        var user = (await Query.ExecuteAsync(CancellationToken.None)).Users.Single(u => u.Id == userId);

        // Assert - to the second, because the screen shows a time and not only a date.
        user.CreatedAtUtc.Should().Be(createdAt);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoCreationDate_ForAnAccountThatPredatesTheColumn()
    {
        // Arrange
        var userId = await Seed.AddUserAsync("Grace", "Hopper");

        // Act
        var user = (await Query.ExecuteAsync(CancellationToken.None)).Users.Single(u => u.Id == userId);

        // Assert - a real state rather than an unset default. The one-off dating script filled in what it could prove
        // and left the rest null, and the screen has to say "unknown" rather than invent a date.
        user.CreatedAtUtc.Should().BeNull();
    }

    #endregion

    #region Payout details

    [Fact]
    public async Task ExecuteAsync_ShouldReturnTheIdOfAnAccountThatHasSavedPayoutDetails()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddUserPayoutDetailsAsync(backdrop.UserId, "Ada Lovelace", "112233", "12345678");

        // Act
        var userIds = (await Query.ExecuteAsync(CancellationToken.None)).UserIdsWithPayoutDetails;

        // Assert
        userIds.Should().Equal(backdrop.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotReturnThePayoutAccountDetailsThemselves()
    {
        // The three account columns are encrypted at rest and are decrypted only for the player and for the administrators
        // of prize leagues they belong to. An administrator reading a list of accounts is neither, so the read asks whether
        // a row exists and nothing else - and the shape of what comes back is what enforces that.
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddUserPayoutDetailsAsync(backdrop.UserId, "Ada Lovelace", "112233", "12345678");

        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert
        data.UserIdsWithPayoutDetails.Should().AllSatisfy(id => id.Should().Be(backdrop.UserId));
        typeof(AdminUsersData).GetProperty(nameof(AdminUsersData.UserIdsWithPayoutDetails))!
            .PropertyType.Should().Be<IReadOnlyList<string>>();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnARowThatExistsWithNoAccountDetailsInIt()
    {
        // What a refreshed dev database looks like: the refresh tool blanks the three encrypted columns and keeps the row.
        // "Has payout details" therefore means a row exists, which is the same question the dashboard checklist asks.
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddUserPayoutDetailsAsync(backdrop.UserId, null, null, null);

        // Act
        var userIds = (await Query.ExecuteAsync(CancellationToken.None)).UserIdsWithPayoutDetails;

        // Assert
        userIds.Should().Equal(backdrop.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoIds_WhenNobodyHasSavedPayoutDetails()
    {
        // Arrange
        await Seed.AddBackdropAsync();

        // Act
        var userIds = (await Query.ExecuteAsync(CancellationToken.None)).UserIdsWithPayoutDetails;

        // Assert
        userIds.Should().BeEmpty();
    }

    #endregion

    #region Onboarding skips

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachStepTheAccountHasDismissed()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        await Seed.AddOnboardingSkipAsync(backdrop.UserId, OnboardingStepKeys.AddMobile);
        await Seed.AddOnboardingSkipAsync(backdrop.UserId, OnboardingStepKeys.AddPayoutDetails);

        // Act
        var skips = (await Query.ExecuteAsync(CancellationToken.None)).OnboardingSkips;

        // Assert - the keys as stored. Which of a skip and the underlying data wins is the step registry to settle.
        skips.Should().HaveCount(2);
        skips.Select(skip => skip.StepKey)
            .Should().BeEquivalentTo([OnboardingStepKeys.AddMobile, OnboardingStepKeys.AddPayoutDetails]);
        skips.Should().OnlyContain(skip => skip.UserId == backdrop.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnASkipEvenWhenTheStepWasDoneAnyway()
    {
        // Three dev accounts dismissed the payout-details prompt and then added payout details. Both rows have to arrive,
        // because deciding that the data beats the skip is not this read's call.
        var backdrop = await Seed.AddBackdropAsync();

        await Seed.AddOnboardingSkipAsync(backdrop.UserId, OnboardingStepKeys.AddPayoutDetails);
        await Seed.AddUserPayoutDetailsAsync(backdrop.UserId, "Ada Lovelace", "112233", "12345678");

        // Act
        var data = await Query.ExecuteAsync(CancellationToken.None);

        // Assert
        data.OnboardingSkips.Should().ContainSingle(skip => skip.StepKey == OnboardingStepKeys.AddPayoutDetails);
        data.UserIdsWithPayoutDetails.Should().Equal(backdrop.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNoSkips_ForAnAccountThatHasDismissedNothing()
    {
        // Arrange
        await Seed.AddBackdropAsync();

        // Act
        var skips = (await Query.ExecuteAsync(CancellationToken.None)).OnboardingSkips;

        // Assert
        skips.Should().BeEmpty();
    }

    #endregion

    #region Badges

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEachBadgeWithItsKeyRatherThanAName()
    {
        // Badge names live in the catalogue in code, so the database has never stored one and this read must not invent one.
        var backdrop = await Seed.AddBackdropAsync();
        var awardedUtc = new DateTime(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

        await Seed.AddUserBadgeAsync(backdrop.UserId, "marksman-2", awardedUtc,
            seasonId: backdrop.SeasonId, detail: "10 exact scores");

        // Act
        var badge = (await Query.ExecuteAsync(CancellationToken.None)).Badges.Single();

        // Assert
        badge.UserId.Should().Be(backdrop.UserId);
        badge.BadgeKey.Should().Be("marksman-2");
        badge.Detail.Should().Be("10 exact scores");
        badge.AwardedUtc.Should().Be(awardedUtc);
        badge.SeasonId.Should().Be(backdrop.SeasonId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnALifetimeBadgeWithNoSeasonAgainstIt()
    {
        // A lifetime badge is not scoped to a season, so there is nothing to show it under - which the reader has to cope
        // with rather than the read papering over.
        var backdrop = await Seed.AddBackdropAsync();

        await Seed.AddUserBadgeAsync(backdrop.UserId, "champion", DateTime.UtcNow);

        // Act
        var badge = (await Query.ExecuteAsync(CancellationToken.None)).Badges.Single();

        // Assert
        badge.SeasonId.Should().BeNull();
        badge.Detail.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnEveryBadgeAnAccountHasEarned()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var roundId = await Seed.AddRoundAsync(backdrop.SeasonId, 1, DateTime.UtcNow);

        // One badge earned twice needs a round to be scoped to - the write path dedupes on the badge plus its round and
        // season, and two awards with no scope at all are the same award.
        await Seed.AddUserBadgeAsync(backdrop.UserId, "sharpshooter-1", DateTime.UtcNow);
        await Seed.AddUserBadgeAsync(backdrop.UserId, "sharpshooter-1", DateTime.UtcNow, roundId: roundId);

        // Act
        var badges = (await Query.ExecuteAsync(CancellationToken.None)).Badges;

        // Assert - counting them is the handler's job, so both rows arrive.
        badges.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotGiveOneAccountsBadgesToAnother()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        await Seed.AddUserBadgeAsync(otherUserId, "champion", DateTime.UtcNow);

        // Act
        var badges = (await Query.ExecuteAsync(CancellationToken.None)).Badges;

        // Assert
        badges.Should().OnlyContain(badge => badge.UserId == otherUserId);
        badges.Should().NotContain(badge => badge.UserId == backdrop.UserId);
    }

    #endregion
}
