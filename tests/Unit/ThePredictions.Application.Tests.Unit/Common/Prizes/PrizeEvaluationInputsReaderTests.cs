using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Data;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;
using static ThePredictions.Application.Common.Prizes.PrizeEvaluationInputsReader;

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

    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly PrizeEvaluationInputsReader _reader;

    public PrizeEvaluationInputsReaderTests()
    {
        _reader = new PrizeEvaluationInputsReader(_dbConnection);
    }

    private void GivenLeague(
        decimal entryCost = 20m,
        decimal? prizeFundOverride = null,
        int entrantCount = 12,
        string? entryCode = EntryCode,
        DateTime? seasonStartUtc = null,
        DateTime? seasonEndUtc = null) =>
        _dbConnection.QuerySingleOrDefaultAsync<LeagueRow>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(new LeagueRow
            {
                LeagueId = LeagueId,
                LeagueName = "The Office League",
                SeasonName = "2026/27",
                AdministratorUserId = "admin-1",
                AdministratorName = "Alice A",
                EntryCode = entryCode,
                EntryCost = entryCost,
                PrizeFundOverride = prizeFundOverride,
                EntryDeadlineUtc = SeasonStart.AddDays(-1),
                SeasonStartDateUtc = seasonStartUtc ?? SeasonStart,
                SeasonEndDateUtc = seasonEndUtc ?? SeasonEnd,
                NumberOfRounds = 38,
                EntrantCount = entrantCount
            });

    private void GivenScheme(bool exists = true) =>
        _dbConnection.QuerySingleOrDefaultAsync<SchemeRow>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(exists ? new SchemeRow { Id = 99 } : null);

    private void GivenSchemeEntries(params EntryRow[] entries) =>
        _dbConnection.QueryAsync<EntryRow>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(entries);

    private static EntryRow Entry(PrizeType category, int perEntryPounds, string? rankTableJson = null) =>
        new() { Category = category, PerEntryPounds = perEntryPounds, RankTableJson = rankTableJson };

    [Fact]
    public async Task LoadAsync_ShouldReturnNothing_WhenTheLeagueDoesNotExist()
    {
        var result = await _reader.LoadAsync(LeagueId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadByEntryCodeAsync_ShouldReturnNothing_WhenNoLeagueUsesThatCode()
    {
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
        await _dbConnection.DidNotReceiveWithAnyArgs().QueryAsync<EntryRow>(default!, CancellationToken.None);
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
