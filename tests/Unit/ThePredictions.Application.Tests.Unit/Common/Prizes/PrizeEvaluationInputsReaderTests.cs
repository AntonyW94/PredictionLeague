using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Prizes;

/// <summary>
/// Gathers everything needed to work out a league's prize pot: the entry cost and how many people
/// have joined, how long the season runs for, and the prize scheme the administrator has set up.
/// Reachable two ways - by league id from inside the app, and by entry code from the public
/// join-by-link page - which share a single build so both give identical answers.
/// </summary>
public class PrizeEvaluationInputsReaderTests
{
    private const int LeagueId = 7;
    private const string EntryCode = "ABC123";

    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime SeasonEnd = new(2027, 5, 31, 0, 0, 0, DateTimeKind.Utc);

    private readonly IPrizeEvaluationInputsQuery _inputsQuery = Substitute.For<IPrizeEvaluationInputsQuery>();
    private readonly PrizeEvaluationInputsReader _reader;

    public PrizeEvaluationInputsReaderTests()
    {
        _reader = new PrizeEvaluationInputsReader(_inputsQuery);
    }

    private PrizeLeagueRow _league = LeagueRow();
    private PrizeSchemeRow[] _schemes = [];
    private PrizeSchemeEntryRow[] _entries = [];

    private void GivenLeague(
        decimal entryCost = 20m,
        decimal? prizeFundOverride = null,
        int entrantCount = 12,
        string? entryCode = EntryCode,
        DateTime? seasonStartUtc = null,
        DateTime? seasonEndUtc = null)
    {
        _league = LeagueRow() with
        {
            EntryCost = entryCost,
            PrizeFundOverride = prizeFundOverride,
            EntrantCount = entrantCount,
            EntryCode = entryCode,
            SeasonStartDateUtc = seasonStartUtc ?? SeasonStart,
            SeasonEndDateUtc = seasonEndUtc ?? SeasonEnd
        };

        Arrange();
    }

    private void GivenScheme(bool exists = true)
    {
        _schemes = exists ? [new PrizeSchemeRow(99)] : [];
        Arrange();
    }

    private void GivenSchemeEntries(params PrizeSchemeEntryRow[] entries)
    {
        _entries = entries;
        Arrange();
    }

    private void GivenNoLeague()
    {
        _inputsQuery.GetByLeagueIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((PrizeEvaluationInputsData?)null);
        _inputsQuery.GetByEntryCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PrizeEvaluationInputsData?)null);
    }

    private void Arrange()
    {
        var data = new PrizeEvaluationInputsData(_league, _schemes, _entries);

        _inputsQuery.GetByLeagueIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(data);
        _inputsQuery.GetByEntryCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(data);
    }

    private static PrizeLeagueRow LeagueRow() =>
        new(LeagueId, "The Office League", "admin-1", "Alice", "Andrews", EntryCode,
            EntryCost: 20m, PrizeFundOverride: null, EntryDeadlineUtc: SeasonStart.AddDays(-1),
            "2026/27", SeasonStart, SeasonEnd, NumberOfRounds: 38, EntrantCount: 12);

    private static PrizeSchemeEntryRow Entry(PrizeType category, int perEntryPounds, string? rankTableJson = null) =>
        new(category, perEntryPounds, rankTableJson);

    [Fact]
    public async Task LoadAsync_ShouldReturnNothing_WhenTheLeagueDoesNotExist()
    {
        GivenNoLeague();

        var result = await _reader.LoadAsync(LeagueId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadByEntryCodeAsync_ShouldReturnNothing_WhenNoLeagueUsesThatCode()
    {
        GivenNoLeague();

        // A mistyped join link must come back empty rather than leaking another league's pot.
        var result = await _reader.LoadByEntryCodeAsync("NOPE", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_ShouldReportTheLeagueAndSeasonFacts()
    {
        GivenLeague(entryCost: 20m, entrantCount: 12);

        var result = await _reader.LoadAsync(LeagueId, CancellationToken.None);

        result!.LeagueId.Should().Be(LeagueId);
        result.LeagueName.Should().Be("The Office League");
        result.SeasonName.Should().Be("2026/27");
        result.AdministratorName.Should().Be("Alice A");
        result.AdministratorUserId.Should().Be("admin-1");
        result.EntryCode.Should().Be(EntryCode);
        result.EntryCost.Should().Be(20m);
        result.EntrantCount.Should().Be(12);
        result.NumberOfRounds.Should().Be(38);
        result.EntryDeadlineUtc.Should().Be(SeasonStart.AddDays(-1));
    }

    [Fact]
    public async Task LoadByEntryCodeAsync_ShouldBuildTheSameAnswerAsLoadingById()
    {
        GivenLeague();
        GivenScheme(exists: false);

        var byId = await _reader.LoadAsync(LeagueId, CancellationToken.None);
        var byCode = await _reader.LoadByEntryCodeAsync(EntryCode, CancellationToken.None);

        byCode!.LeagueId.Should().Be(byId!.LeagueId);
        byCode.EntryCost.Should().Be(byId.EntryCost);
        byCode.NumberOfMonths.Should().Be(byId.NumberOfMonths);
        byCode.HasScheme.Should().Be(byId.HasScheme);
    }

    [Theory]
    [InlineData(2026, 8, 1, 2027, 5, 31, 10)]
    [InlineData(2026, 8, 1, 2026, 8, 1, 1)]
    [InlineData(2026, 8, 15, 2026, 9, 14, 1)]
    [InlineData(2026, 8, 15, 2026, 9, 15, 2)]
    [InlineData(2026, 9, 1, 2026, 8, 1, 0)]
    public async Task LoadAsync_ShouldCountTheMonthsAMonthlyPrizeCouldBeWonIn(
        int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay, int expectedMonths)
    {
        // Monthly prizes are funded per month, so a season running mid-August to mid-September pays
        // out once, not twice - the second month only counts once the date is actually reached.
        GivenLeague(
            seasonStartUtc: new DateTime(startYear, startMonth, startDay, 0, 0, 0, DateTimeKind.Utc),
            seasonEndUtc: new DateTime(endYear, endMonth, endDay, 0, 0, 0, DateTimeKind.Utc));

        var result = await _reader.LoadAsync(LeagueId, CancellationToken.None);

        result!.NumberOfMonths.Should().Be(expectedMonths);
    }

    [Fact]
    public async Task LoadAsync_ShouldReportNoSchemeAndNoCategories_WhenTheAdministratorHasNotSetOneUp()
    {
        GivenLeague();
        GivenScheme(exists: false);

        var result = await _reader.LoadAsync(LeagueId, CancellationToken.None);

        result!.HasScheme.Should().BeFalse();
        result.Categories.Should().BeEmpty();
        // With no scheme there are no entries to read - the port returns them together, so there is no second trip to skip.
    }

    [Fact]
    public async Task LoadAsync_ShouldReportTheSchemesCategories()
    {
        GivenLeague();
        GivenScheme();
        GivenSchemeEntries(
            Entry(PrizeType.Overall, 5, rankTableJson: "[{\"Rank\":1,\"Share\":100}]"),
            Entry(PrizeType.Monthly, 2));

        var result = await _reader.LoadAsync(LeagueId, CancellationToken.None);

        result!.HasScheme.Should().BeTrue();
        result.Categories.Should().HaveCount(2);
        result.Categories[0].Category.Should().Be(PrizeType.Overall);
        result.Categories[0].PerEntryPounds.Should().Be(5);
        result.Categories[0].RankTableJson.Should().Be("[{\"Rank\":1,\"Share\":100}]");
        result.Categories[1].Category.Should().Be(PrizeType.Monthly);
        result.Categories[1].RankTableJson.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_ShouldReportASchemeWithNoEntriesAsStillBeingAScheme()
    {
        // A scheme row exists but no categories have been funded yet - that is different from
        // having no scheme at all, and the prize page says so.
        GivenLeague();
        GivenScheme();
        GivenSchemeEntries();

        var result = await _reader.LoadAsync(LeagueId, CancellationToken.None);

        result!.HasScheme.Should().BeTrue();
        result.Categories.Should().BeEmpty();
    }

    public static TheoryData<decimal?, int> TopUpCases => new()
    {
        { null, 0 },
        { 0m, 0 },
        { 50m, 50 },
        { 49.99m, 49 }
    };

    [Theory]
    [MemberData(nameof(TopUpCases))]
    public async Task LoadAsync_ShouldReportTheAdministratorsTopUpInWholePounds(decimal? prizeFundOverride, int expectedTopUp)
    {
        // The prize maths works in whole pounds, and part of a pound is dropped rather than rounded
        // up so the pot can never promise more than was actually put in.
        GivenLeague(prizeFundOverride: prizeFundOverride);

        var result = await _reader.LoadAsync(LeagueId, CancellationToken.None);

        result!.AdminTopUpPounds.Should().Be(expectedTopUp);
    }

    [Theory]
    [InlineData(EntryCode, true)]
    [InlineData(null, false)]
    public async Task LoadAsync_ShouldTreatALeagueWithAnEntryCodeAsPrivate(string? entryCode, bool expectedPrivate)
    {
        GivenLeague(entryCode: entryCode);

        var result = await _reader.LoadAsync(LeagueId, CancellationToken.None);

        result!.IsPrivate.Should().Be(expectedPrivate);
    }
}
