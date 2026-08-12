using FluentAssertions;
using ThePredictions.Application.Features.Account.Queries;
using ThePredictions.Application.Features.Homepage.Queries;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Features.Onboarding.Queries;
using ThePredictions.Application.Features.Prizes.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What the last of the small reads must return: the homepage's seasons, a player's own account and bank details, the
/// onboarding checklist's state, the league management list, a league's stored bank details, and the season a prize scheme is
/// evaluated against.
///
/// Grouped into one suite because they promise the same thing - the rows as stored, with nothing decided. Between them they used
/// to hold three <c>GETUTCDATE()</c> calls, a prize-fund formula, two blank-string tests, a category <c>CASE</c> and an
/// <c>ISNULL</c> sentinel.
/// </summary>
public abstract class PlayerPagesQueryConformanceTests
{
    private static readonly DateTime Deadline = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    protected abstract IHomepageSeasonsQuery HomepageSeasons { get; }

    protected abstract IAccountProfileQuery AccountProfile { get; }

    protected abstract IMyPayoutDetailsQuery MyPayoutDetails { get; }

    protected abstract IOnboardingStateQuery OnboardingState { get; }

    protected abstract IManageLeaguesQuery ManageLeagues { get; }

    protected abstract ILeagueBankDetailsQuery LeagueBankDetails { get; }

    protected abstract ILeagueEmailRecipientQuery LeagueEmailRecipient { get; }

    protected abstract IPrizeSchemeSeasonQuery PrizeSchemeSeason { get; }

    protected abstract ITestDataSeeder Seed { get; }

    #region The homepage

    [Fact]
    public async Task HomepageSeasons_ShouldReturnNothing_WhenThereAreNoSeasons()
    {
        var data = await HomepageSeasons.ExecuteAsync(CancellationToken.None);

        data.Seasons.Should().BeEmpty();
        data.Leagues.Should().BeEmpty();
        data.Memberships.Should().BeEmpty();
    }

    [Fact]
    public async Task HomepageSeasons_ShouldReturnEverySeasonWithItsDatesAndCompetitionType()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var season = (await HomepageSeasons.ExecuteAsync(CancellationToken.None)).Seasons.Single();

        // Assert - which seasons are still advertised is measured against the injected clock, so nothing is filtered here.
        season.Id.Should().Be(backdrop.SeasonId);
        season.Name.Should().Be("2026/27");
        season.CompetitionType.Should().BeOneOf(CompetitionType.League, CompetitionType.Tournament);
        season.StartDateUtc.Should().NotBe(default);
        season.EndDateUtc.Should().NotBe(default);
    }

    [Fact]
    public async Task HomepageSeasons_ShouldReturnEachLeaguesPriceTopUpAndApprovedMemberCount()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, otherUserId, LeagueMemberStatus.Pending);

        // Act
        var league = (await HomepageSeasons.ExecuteAsync(CancellationToken.None)).Leagues.Single();

        // Assert - the pot is an entry fee times a head count, and only approved members have paid one.
        league.SeasonId.Should().Be(backdrop.SeasonId);
        league.LeagueId.Should().Be(leagueId);
        league.ApprovedMemberCount.Should().Be(1);
    }

    [Fact]
    public async Task HomepageSeasons_ShouldReturnOneRowPerApprovedMembershipRatherThanACount()
    {
        // Arrange - one player in two of the season's leagues.
        var backdrop = await Seed.AddBackdropAsync();

        var firstLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "First");
        var secondLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Second");

        await Seed.AddLeagueMemberAsync(firstLeagueId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(secondLeagueId, backdrop.UserId);

        // Act
        var memberships = (await HomepageSeasons.ExecuteAsync(CancellationToken.None)).Memberships;

        // Assert - collapsing them to one player is a rule, so both rows arrive.
        memberships.Should().HaveCount(2);
        memberships.Should().OnlyContain(membership => membership.UserId == backdrop.UserId);
    }

    #endregion

    #region A player's own account

    [Fact]
    public async Task AccountProfile_ShouldReturnNothing_ForAnIdThatMatchesNoPlayer()
    {
        (await AccountProfile.ExecuteAsync("no-such-user", CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task AccountProfile_ShouldReturnTheirDetailsAndTheOptInDateItself()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var profile = await AccountProfile.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - the date, not a yes-or-no. Reading it as consent is a rule, and storing the moment is what lets the consent
        // be evidenced later.
        profile.Should().NotBeNull();
        profile!.FirstName.Should().Be("Ada");
        profile.LastName.Should().Be("Lovelace");
        profile.Email.Should().NotBeNullOrWhiteSpace();
        profile.MarketingOptInAtUtc.Should().BeNull();
    }

    [Fact]
    public async Task MyPayoutDetails_ShouldReturnNothing_WhenNoneHaveBeenSaved()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var details = await MyPayoutDetails.GetDetailsAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        details.Should().BeNull();
    }

    [Fact]
    public async Task MyPayoutDetails_ShouldReturnTheStoredValuesUntouched()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddUserPayoutDetailsAsync(backdrop.UserId, "cipher-name", "cipher-sort", "cipher-number");

        // Act
        var details = await MyPayoutDetails.GetDetailsAsync(backdrop.UserId, CancellationToken.None);

        // Assert - as stored. Decrypting them, and judging whether a partial set counts, both happen after this.
        details!.EncryptedAccountName.Should().Be("cipher-name");
        details.EncryptedSortCode.Should().Be("cipher-sort");
        details.EncryptedAccountNumber.Should().Be("cipher-number");
    }

    [Fact]
    public async Task MyPayoutDetails_ShouldReturnAPartlyFilledSet()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddUserPayoutDetailsAsync(backdrop.UserId, "cipher-name", null, null);

        // Act
        var details = await MyPayoutDetails.GetDetailsAsync(backdrop.UserId, CancellationToken.None);

        // Assert - whether that counts as having details is a rule applied after decryption.
        details!.EncryptedAccountName.Should().Be("cipher-name");
        details.EncryptedSortCode.Should().BeNull();
    }

    [Fact]
    public async Task MyPayoutDetails_ShouldReturnTheAdministratorsOfPrizePayingLeaguesTheyAreIn()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var administratorId = await Seed.AddUserAsync("Grace", "Hopper");

        var payingLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, administratorId, "Pays Prizes", hasPrizes: true);
        await Seed.AddLeagueMemberAsync(payingLeagueId, backdrop.UserId);

        // Act
        var administrators = await MyPayoutDetails.GetPayingAdministratorsAsync(backdrop.UserId, CancellationToken.None);

        // Assert - both name parts, because composing and ordering them is a rule.
        var administrator = administrators.Single();
        administrator.UserId.Should().Be(administratorId);
        administrator.FirstName.Should().Be("Grace");
        administrator.LastName.Should().Be("Hopper");
    }

    [Fact]
    public async Task MyPayoutDetails_ShouldNotReturnTheAdministratorOfALeagueThatPaysNothing()
    {
        // Arrange - a league with no prizes will never send anybody money, so its administrator is not somebody to expect it
        // from. That is which rows to read rather than a rule, so it stays in the read.
        var backdrop = await Seed.AddBackdropAsync();
        var administratorId = await Seed.AddUserAsync("Grace", "Hopper");

        var freeLeagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, administratorId);
        await Seed.AddLeagueMemberAsync(freeLeagueId, backdrop.UserId);

        // Act
        var administrators = await MyPayoutDetails.GetPayingAdministratorsAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        administrators.Should().BeEmpty();
    }

    [Fact]
    public async Task MyPayoutDetails_ShouldNotReturnThePlayerThemselves()
    {
        // Arrange - a league they run and are in. Nobody needs telling they will be paying themselves.
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, hasPrizes: true);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        // Act
        var administrators = await MyPayoutDetails.GetPayingAdministratorsAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        administrators.Should().BeEmpty();
    }

    #endregion

    #region The onboarding checklist

    [Fact]
    public async Task OnboardingState_ShouldReturnZeroesForAnAccountThatHasDoneNothing()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var state = await OnboardingState.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        state.PassCount.Should().Be(0);
        state.LeagueCount.Should().Be(0);
        state.HasPayoutDetails.Should().BeFalse();
    }

    [Fact]
    public async Task OnboardingState_ShouldReturnTheStoredPhoneNumberRatherThanAJudgementOnIt()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var state = await OnboardingState.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert - a number made only of spaces does not count, and that decision is a rule rather than something the read
        // should have made with LEN(LTRIM(RTRIM(...))).
        state.PhoneNumber.Should().BeNullOrWhiteSpace();
    }

    [Fact]
    public async Task OnboardingState_ShouldCountAPendingMembershipAsALeagueJoined()
    {
        // Asking to join is the step. The checklist should not un-tick itself while an administrator decides.
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId, LeagueMemberStatus.Pending);

        // Act
        var state = await OnboardingState.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        state.LeagueCount.Should().Be(1);
    }

    [Fact]
    public async Task OnboardingState_ShouldCountTheirPassesAndSeeTheirBankDetails()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);
        await Seed.AddUserPayoutDetailsAsync(backdrop.UserId, "cipher-name", "cipher-sort", "cipher-number");

        // Act
        var state = await OnboardingState.ExecuteAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        state.PassCount.Should().Be(1);
        state.HasPayoutDetails.Should().BeTrue();
    }

    [Fact]
    public async Task OnboardingState_ShouldReturnNoSkippedSteps_WhenNothingHasBeenDismissed()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var skipped = await OnboardingState.GetSkippedStepKeysAsync(backdrop.UserId, CancellationToken.None);

        // Assert
        skipped.Should().BeEmpty();
    }

    #endregion

    #region Managing leagues

    [Fact]
    public async Task ManageLeagues_ShouldReturnNothing_WhenThereAreNoLeagues()
    {
        (await ManageLeagues.ExecuteAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ManageLeagues_ShouldReturnEveryLeagueOnTheSiteWithItsSeasonAndAdministrator()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var mine = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId, "Mine");
        var theirs = await Seed.AddLeagueAsync(backdrop.SeasonId, otherUserId, "Theirs");

        // Act
        var leagues = await ManageLeagues.ExecuteAsync(CancellationToken.None);

        // Assert - unfiltered on purpose: who may see which is the handler's rule, and it is the only thing standing between one
        // player and everybody else's private leagues.
        leagues.Select(league => league.Id).Should().BeEquivalentTo([mine, theirs]);
        leagues.Single(league => league.Id == theirs).AdministratorUserId.Should().Be(otherUserId);
        leagues.Should().OnlyContain(league => league.SeasonName == "2026/27");
    }

    [Fact]
    public async Task ManageLeagues_ShouldReturnAMissingEntryCodeAsNothing()
    {
        // Arrange - a seeded league is public, which is to say it has no entry code.
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);

        // Act
        var league = (await ManageLeagues.ExecuteAsync(CancellationToken.None)).Single();

        // Assert - null, not the word "Public". The statement this replaces substituted that label with an ISNULL, so a sentinel
        // travelled to the browser in a field named for a code.
        league.EntryCode.Should().BeNull();
    }

    [Fact]
    public async Task ManageLeagues_ShouldCountTheLeaguesMembers()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, otherUserId, LeagueMemberStatus.Pending);

        // Act
        var league = (await ManageLeagues.ExecuteAsync(CancellationToken.None)).Single();

        // Assert - every membership, whatever its status, which is what the screen has always shown.
        league.MemberCount.Should().Be(2);
    }

    [Fact]
    public async Task LeagueBankDetails_ShouldReturnNothing_ForALeagueThatDoesNotExist()
    {
        (await LeagueBankDetails.ExecuteAsync(-1, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task LeagueBankDetails_ShouldReturnTheAdministratorAlongsideTheCiphertext()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);

        // Act
        var row = await LeagueBankDetails.ExecuteAsync(leagueId, CancellationToken.None);

        // Assert - the administrator's id comes back so the handler can refuse the request before decrypting anything. That
        // ordering is the whole security of this read.
        row.Should().NotBeNull();
        row!.AdministratorUserId.Should().Be(backdrop.UserId);
    }

    #endregion

    #region Notification recipients and prize scheme seasons

    [Fact]
    public async Task LeagueEmailRecipient_ShouldReturnNothing_WhenThereIsNoSuchPlayer()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var recipient = await LeagueEmailRecipient.ExecuteAsync("no-such-user", backdrop.SeasonId, CancellationToken.None);

        // Assert
        recipient.Should().BeNull();
    }

    [Fact]
    public async Task LeagueEmailRecipient_ShouldReturnNothing_WhenThereIsNoSuchSeason()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var recipient = await LeagueEmailRecipient.ExecuteAsync(backdrop.UserId, -1, CancellationToken.None);

        // Assert
        recipient.Should().BeNull();
    }

    [Fact]
    public async Task LeagueEmailRecipient_ShouldReturnTheAddressNameAndSeason()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var recipient = await LeagueEmailRecipient.ExecuteAsync(backdrop.UserId, backdrop.SeasonId, CancellationToken.None);

        // Assert - the league's own name is not here: the caller already holds it, which is what keeps this read off a table an
        // in-flight join transaction may have locked.
        recipient.Should().NotBeNull();
        recipient!.Email.Should().NotBeNullOrWhiteSpace();
        recipient.FirstName.Should().Be("Ada");
        recipient.SeasonName.Should().Be("2026/27");
    }

    [Fact]
    public async Task PrizeSchemeSeason_ShouldReturnNothing_ForASeasonThatDoesNotExist()
    {
        (await PrizeSchemeSeason.ExecuteAsync(-1, CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task PrizeSchemeSeason_ShouldReturnTheSeasonsLengthAndDates()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var season = await PrizeSchemeSeason.ExecuteAsync(backdrop.SeasonId, CancellationToken.None);

        // Assert - how many months those dates span is a rule the evaluator applies.
        season.Should().NotBeNull();
        season!.NumberOfRounds.Should().Be(38);
        season.StartDateUtc.Should().NotBe(default);
        season.EndDateUtc.Should().NotBe(default);
        Deadline.Should().NotBe(default);
    }

    #endregion
}
