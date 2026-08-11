using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.SeasonPasses.Queries;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.SeasonPasses.Queries;

/// <summary>
/// The four season-pass screens, which shared their conditions without sharing any code.
///
/// The available-passes and past-passes pages are complements of one another - same season, same "not already held", then
/// entry open somewhere versus closed everywhere - and these tests are written to hold both halves of that in place.
/// </summary>
public class SeasonPassPagesQueryHandlerTests
{
    private const string UserId = "user-me";
    private const int SeasonId = 7;

    private static readonly DateTime Now = new(2026, 8, 11, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISeasonPassPagesQuery _query = Substitute.For<ISeasonPassPagesQuery>();

    #region Available passes

    [Fact]
    public async Task Available_ShouldOfferASeasonWithAnOpenLeagueTheyDoNotHold()
    {
        // Arrange
        Given(Data(seasons: [Season()], leagues: [League(Now.AddDays(7))]));

        // Act
        var available = await AvailableAsync();

        // Assert
        available.Select(season => season.SeasonId).Should().Equal(SeasonId);
    }

    [Fact]
    public async Task Available_ShouldNotOfferASeasonTheyAlreadyHold()
    {
        // Arrange
        Given(Data(seasons: [Season()], leagues: [League(Now.AddDays(7))], heldPasses: [Pass()]));

        // Act
        var available = await AvailableAsync();

        // Assert
        available.Should().BeEmpty();
    }

    [Fact]
    public async Task Available_ShouldNotOfferASeasonNobodyCanJoinAnyMore()
    {
        // A pass buys entry to a league, so a season whose leagues have all closed is not worth selling.
        Given(Data(seasons: [Season()], leagues: [League(Now.AddDays(-1))]));

        // Act
        var available = await AvailableAsync();

        // Assert
        available.Should().BeEmpty();
    }

    [Fact]
    public async Task Available_ShouldNotOfferASeasonWithNoLeaguesAtAll()
    {
        // Arrange
        Given(Data(seasons: [Season()]));

        // Act
        var available = await AvailableAsync();

        // Assert
        available.Should().BeEmpty();
    }

    [Fact]
    public async Task Available_ShouldNotOfferASeasonThatHasBeenRetired()
    {
        // Arrange
        Given(Data(seasons: [Season() with { IsActive = false }], leagues: [League(Now.AddDays(7))]));

        // Act
        var available = await AvailableAsync();

        // Assert
        available.Should().BeEmpty();
    }

    [Fact]
    public async Task Available_ShouldNotTreatALeagueWithNoDeadlineAsOpen()
    {
        // The old statements got this right only because SQL drops a null from a comparison, rather than because anybody
        // decided it.
        Given(Data(seasons: [Season()], leagues: [League(null)]));

        // Act
        var available = await AvailableAsync();

        // Assert
        available.Should().BeEmpty();
    }

    [Fact]
    public async Task Available_ShouldReportThePricesAndWhetherPaymentIsNeeded()
    {
        // Arrange
        Given(Data(seasons: [Season() with { StandardPrice = 10m, PremiumPrice = 15m }], leagues: [League(Now.AddDays(7))]));

        // Act
        var season = (await AvailableAsync()).Single();

        // Assert
        season.RequiresPayment.Should().BeTrue();
        season.StandardPrice.Should().Be(10m);
        season.PremiumPrice.Should().Be(15m);
    }

    [Fact]
    public async Task Available_ShouldReportAFreeSeasonAsNeedingNoPayment()
    {
        // A season with no standard price is a free one.
        Given(Data(
            seasons: [Season() with { StandardPrice = null, PremiumPrice = null }],
            leagues: [League(Now.AddDays(7))]));

        // Act
        var season = (await AvailableAsync()).Single();

        // Assert
        season.RequiresPayment.Should().BeFalse();
    }

    [Fact]
    public async Task Available_ShouldOfferATrialToAPlayerWhoHasNeverHeldAPass()
    {
        // Arrange
        Given(Data(seasons: [Season()], leagues: [League(Now.AddDays(7))]));

        // Act
        var season = (await AvailableAsync()).Single();

        // Assert
        season.IsTrialEligible.Should().BeTrue();
    }

    [Fact]
    public async Task Available_ShouldNotOfferATrialToSomeoneWhoHeldAPassForAnotherSeason()
    {
        // The trial is the one-off way in, so it is about the player rather than the season.
        Given(Data(
            seasons: [Season(), Season(id: 8) with { StartDateUtc = SeasonStart.AddYears(-1) }],
            leagues: [League(Now.AddDays(7))],
            heldPasses: [Pass(seasonId: 8)]));

        // Act
        var season = (await AvailableAsync()).Single();

        // Assert
        season.SeasonId.Should().Be(SeasonId);
        season.IsTrialEligible.Should().BeFalse();
    }

    [Fact]
    public async Task Available_ShouldReportHowManyAreTakingPart()
    {
        // Arrange
        Given(Data(
            seasons: [Season()],
            leagues: [League(Now.AddDays(7))],
            holderCounts: [new SeasonPassHolderCountRow(SeasonId, 23)]));

        // Act
        var season = (await AvailableAsync()).Single();

        // Assert
        season.PlayerCount.Should().Be(23);
    }

    [Fact]
    public async Task Available_ShouldNotShowOneSeasonsPlayerCountAgainstAnother()
    {
        // Arrange - two seasons on offer with different numbers taking part.
        Given(Data(
            seasons: [Season(), Season(id: 8) with { StartDateUtc = SeasonStart.AddYears(-1) }],
            leagues: [League(Now.AddDays(7)), League(Now.AddDays(7), seasonId: 8)],
            holderCounts: [new SeasonPassHolderCountRow(SeasonId, 23), new SeasonPassHolderCountRow(8, 4)]));

        // Act
        var available = await AvailableAsync();

        // Assert
        available.Single(season => season.SeasonId == SeasonId).PlayerCount.Should().Be(23);
        available.Single(season => season.SeasonId == 8).PlayerCount.Should().Be(4);
    }

    [Fact]
    public async Task Available_ShouldReportNobodyTakingPart_WhenNoPassesHaveBeenTakenOut()
    {
        // Arrange
        Given(Data(seasons: [Season()], leagues: [League(Now.AddDays(7))]));

        // Act
        var season = (await AvailableAsync()).Single();

        // Assert
        season.PlayerCount.Should().Be(0);
    }

    [Fact]
    public async Task Available_ShouldReportTheSoonestDeadlineStillToCome()
    {
        // Arrange - one deadline already past, two still ahead.
        Given(Data(
            seasons: [Season()],
            leagues: [League(Now.AddDays(-1)), League(Now.AddDays(10)), League(Now.AddDays(3))]));

        // Act
        var season = (await AvailableAsync()).Single();

        // Assert
        season.NextEntryDeadlineUtc.Should().Be(Now.AddDays(3));
    }

    [Fact]
    public async Task Available_ShouldListTheNewestSeasonFirst()
    {
        // Arrange
        Given(Data(
            seasons:
            [
                Season() with { StartDateUtc = SeasonStart.AddYears(-1) },
                Season(id: 8) with { StartDateUtc = SeasonStart }
            ],
            leagues: [League(Now.AddDays(7)), League(Now.AddDays(7), seasonId: 8)]));

        // Act
        var available = await AvailableAsync();

        // Assert
        available.Select(season => season.SeasonId).Should().Equal(8, SeasonId);
    }

    #endregion

    #region Past passes

    [Fact]
    public async Task Past_ShouldShowASeasonThatRanAndCanNoLongerBeJoined()
    {
        // Arrange
        Given(Data(seasons: [Season()], leagues: [League(Now.AddDays(-1))]));

        // Act
        var past = await PastAsync();

        // Assert
        past.Select(season => season.SeasonId).Should().Equal(SeasonId);
    }

    [Fact]
    public async Task Past_ShouldNotShowASeasonThatCanStillBeJoined()
    {
        // The complement of the available-passes rule: a season cannot be on both pages.
        Given(Data(seasons: [Season()], leagues: [League(Now.AddDays(7))]));

        // Act
        var past = await PastAsync();

        // Assert
        past.Should().BeEmpty();
    }

    [Fact]
    public async Task Past_ShouldNotShowASeasonThatNeverRan()
    {
        // A season set up and never used was not missed - it never happened.
        Given(Data(seasons: [Season()]));

        // Act
        var past = await PastAsync();

        // Assert
        past.Should().BeEmpty();
    }

    [Fact]
    public async Task Past_ShouldNotShowASeasonTheyHeldAPassFor()
    {
        // Arrange
        Given(Data(seasons: [Season()], leagues: [League(Now.AddDays(-1))], heldPasses: [Pass()]));

        // Act
        var past = await PastAsync();

        // Assert
        past.Should().BeEmpty();
    }

    [Fact]
    public async Task Past_ShouldReportTheSeasonAndHowManyTookPart()
    {
        // Arrange
        Given(Data(
            seasons: [Season()],
            leagues: [League(Now.AddDays(-1))],
            holderCounts: [new SeasonPassHolderCountRow(SeasonId, 18)]));

        // Act
        var season = (await PastAsync()).Single();

        // Assert
        season.SeasonName.Should().Be("2026/27");
        season.CompetitionLogoUrl.Should().Be("pl.png");
        season.PlayerCount.Should().Be(18);
    }

    #endregion

    #region Passes they hold

    [Fact]
    public async Task Mine_ShouldReturnNothing_WhenTheyHoldNone()
    {
        // Arrange
        Given(Data(seasons: [Season()]));

        // Act
        var mine = await MineAsync();

        // Assert
        mine.Should().BeEmpty();
    }

    [Fact]
    public async Task Mine_ShouldListTheNewestPassFirst()
    {
        // Arrange
        Given(Data(
            seasons: [Season(), Season(id: 8)],
            heldPasses:
            [
                Pass() with { CreatedAtUtc = Now.AddYears(-1) },
                Pass(seasonId: 8) with { CreatedAtUtc = Now }
            ]));

        // Act
        var mine = await MineAsync();

        // Assert
        mine.Select(pass => pass.SeasonId).Should().Equal(8, SeasonId);
    }

    [Fact]
    public async Task Mine_ShouldReportThePassDetailsWithTheSeasonItIsFor()
    {
        // Arrange
        Given(Data(seasons: [Season()], heldPasses: [Pass() with { AmountPaid = 12m }]));

        // Act
        var pass = (await MineAsync()).Single();

        // Assert
        pass.SeasonName.Should().Be("2026/27");
        pass.CompetitionLogoUrl.Should().Be("pl.png");
        pass.Tier.Should().Be(nameof(SeasonPassTier.Standard));
        pass.Source.Should().Be(nameof(SeasonPassSource.Purchased));
        pass.AmountPaid.Should().Be(12m);
        pass.CreatedAtUtc.Should().Be(Now);
    }

    [Fact]
    public async Task Mine_ShouldReportTextMessageRemindersOnlyForThePremiumTier()
    {
        // Arrange
        Given(Data(
            seasons: [Season(), Season(id: 8)],
            heldPasses:
            [
                Pass() with { Tier = nameof(SeasonPassTier.Standard) },
                Pass(seasonId: 8) with { Tier = nameof(SeasonPassTier.Premium) }
            ]));

        // Act
        var mine = await MineAsync();

        // Assert
        mine.Single(pass => pass.SeasonId == SeasonId).HasSmsReminders.Should().BeFalse();
        mine.Single(pass => pass.SeasonId == 8).HasSmsReminders.Should().BeTrue();
    }

    #endregion

    #region One season's options

    [Fact]
    public async Task Options_ShouldReturnNothing_ForASeasonThatDoesNotExist()
    {
        // Arrange
        Given(Data(seasons: [Season()]));

        // Act
        var options = await OptionsAsync(99);

        // Assert
        options.Should().BeNull();
    }

    [Fact]
    public async Task Options_ShouldReportTheSeasonsStateRatherThanFilteringItOut()
    {
        // This page says what the state is - already held, entry closed - so the screen can explain itself. It is the one
        // of the four that does not filter.
        Given(Data(
            seasons: [Season()],
            leagues: [League(Now.AddDays(-1))],
            heldPasses: [Pass()]));

        // Act
        var options = await OptionsAsync(SeasonId);

        // Assert
        options.Should().NotBeNull();
        options!.AlreadyHeld.Should().BeTrue();
        options.EntryOpen.Should().BeFalse();
        options.IsTrialEligible.Should().BeFalse();
    }

    [Fact]
    public async Task Options_ShouldReportARetiredSeason()
    {
        // Arrange - reached by id, so it has to answer for a season that has been retired.
        Given(Data(seasons: [Season() with { IsActive = false }]));

        // Act
        var options = await OptionsAsync(SeasonId);

        // Assert
        options.Should().NotBeNull();
        options!.EntryOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Options_ShouldReportEverythingTheTakeUpPageNeeds()
    {
        // Arrange
        Given(Data(
            seasons: [Season() with { StandardPrice = 10m, PremiumPrice = 15m }],
            leagues: [League(Now.AddDays(5))],
            holderCounts: [new SeasonPassHolderCountRow(SeasonId, 9)]));

        // Act
        var options = await OptionsAsync(SeasonId);

        // Assert
        options!.SeasonName.Should().Be("2026/27");
        options.CompetitionLogoUrl.Should().Be("pl.png");
        options.CompetitionDescription.Should().Be("The big one");
        options.RequiresPayment.Should().BeTrue();
        options.StandardPrice.Should().Be(10m);
        options.PremiumPrice.Should().Be(15m);
        options.IsTrialEligible.Should().BeTrue();
        options.AlreadyHeld.Should().BeFalse();
        options.EntryOpen.Should().BeTrue();
        options.PlayerCount.Should().Be(9);
        options.NextEntryDeadlineUtc.Should().Be(Now.AddDays(5));
    }

    #endregion

    private void Given(SeasonPassPagesData data) =>
        _query.ExecuteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(data);

    private static SeasonPassPagesData Data(
        SeasonPassSeasonRow[]? seasons = null,
        SeasonLeagueEntryRow[]? leagues = null,
        SeasonPassHolderCountRow[]? holderCounts = null,
        HeldSeasonPassRow[]? heldPasses = null) =>
        new(seasons ?? [], leagues ?? [], holderCounts ?? [], heldPasses ?? []);

    private static SeasonPassSeasonRow Season(int id = SeasonId) =>
        new(id, id == SeasonId ? "2026/27" : $"Season {id}", SeasonStart, IsActive: true,
            "pl.png", "The big one", StandardPrice: 10m, PremiumPrice: 15m);

    private static SeasonLeagueEntryRow League(DateTime? entryDeadlineUtc, int seasonId = SeasonId) =>
        new(seasonId, LeagueId: seasonId * 100 + (entryDeadlineUtc?.Day ?? 0), entryDeadlineUtc);

    private static HeldSeasonPassRow Pass(int seasonId = SeasonId) =>
        new(seasonId, nameof(SeasonPassTier.Standard), nameof(SeasonPassSource.Purchased), AmountPaid: 10m, Now);

    private Task<IEnumerable<Contracts.SeasonPasses.AvailableSeasonPassDto>> AvailableAsync() =>
        new GetAvailableSeasonPassesQueryHandler(_query, new TestDateTimeProvider(Now))
            .Handle(new GetAvailableSeasonPassesQuery(UserId), CancellationToken.None);

    private Task<IEnumerable<Contracts.SeasonPasses.PastSeasonPassDto>> PastAsync() =>
        new GetPastSeasonPassesQueryHandler(_query, new TestDateTimeProvider(Now))
            .Handle(new GetPastSeasonPassesQuery(UserId), CancellationToken.None);

    private Task<IEnumerable<Contracts.SeasonPasses.MySeasonPassDto>> MineAsync() =>
        new GetMySeasonPassesQueryHandler(_query).Handle(new GetMySeasonPassesQuery(UserId), CancellationToken.None);

    private Task<Contracts.SeasonPasses.SeasonPassOptionsDto?> OptionsAsync(int seasonId) =>
        new GetSeasonPassOptionsQueryHandler(_query, new TestDateTimeProvider(Now))
            .Handle(new GetSeasonPassOptionsQuery(UserId, seasonId), CancellationToken.None);
}
