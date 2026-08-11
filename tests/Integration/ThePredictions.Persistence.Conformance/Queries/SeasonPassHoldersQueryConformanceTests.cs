using FluentAssertions;
using ThePredictions.Application.Features.Admin.Seasons.Queries;
using ThePredictions.Contracts.Admin.Seasons;
using ThePredictions.Contracts.Common;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Persistence.Conformance.Queries;

/// <summary>
/// What any <see cref="ISeasonPassHoldersQuery"/> implementation must do: filter, sort and page a season's pass holders, and
/// report the totals for everybody the filters match rather than for the page.
///
/// This is the one read where the filtering and paging stay in the adapter, so unlike every other suite here these tests
/// assert behaviour rather than the absence of it - including that a name containing a wildcard is searched for literally,
/// which is the adapter's job because what needs escaping depends on how it searches.
/// </summary>
public abstract class SeasonPassHoldersQueryConformanceTests
{
    private static readonly DateTime AcquiredAt = new(2026, 8, 1, 12, 0, 0, DateTimeKind.Utc);

    protected abstract ISeasonPassHoldersQuery Query { get; }

    protected abstract ITestDataSeeder Seed { get; }

    #region The summary

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnNothing_ForASeasonThatDoesNotExist()
    {
        // Act
        var summary = await Query.GetSummaryAsync(Criteria(-1), CancellationToken.None);

        // Assert - whether that is a client mistake is the handler's to decide.
        summary.Should().BeNull();
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldReturnTheSeasonWithNothingMatching_WhenItHasNoHolders()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();

        // Act
        var summary = await Query.GetSummaryAsync(Criteria(backdrop.SeasonId), CancellationToken.None);

        // Assert - the season is still there to be named, which is how the screen shows an empty page with a title.
        summary.Should().NotBeNull();
        summary!.SeasonName.Should().Be("2026/27");
        summary.MatchingCount.Should().Be(0);
        summary.TotalCollected.Should().Be(0m);
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldCountAndTotalEverybodyMatching()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);
        await Seed.AddSeasonPassAsync(otherUserId, backdrop.SeasonId);

        // Act
        var summary = await Query.GetSummaryAsync(Criteria(backdrop.SeasonId), CancellationToken.None);

        // Assert
        summary!.MatchingCount.Should().Be(2);
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldNotCountAnotherSeasonsHolders()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherSeasonId = await Seed.AddSeasonAsync(backdrop.CompetitionId, "2027/28");

        await Seed.AddSeasonPassAsync(backdrop.UserId, otherSeasonId);

        // Act
        var summary = await Query.GetSummaryAsync(Criteria(backdrop.SeasonId), CancellationToken.None);

        // Assert
        summary!.MatchingCount.Should().Be(0);
    }

    #endregion

    #region Filtering

    [Fact]
    public async Task GetPageAsync_ShouldMatchOnPartOfAName()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);
        await Seed.AddSeasonPassAsync(otherUserId, backdrop.SeasonId);

        // Act
        var holders = await Query.GetPageAsync(
            Criteria(backdrop.SeasonId) with { NameFilter = "Lovel" }, Paging(), CancellationToken.None);

        // Assert - the filter searches the full name, which is also what is displayed and sorted on.
        holders.Select(holder => holder.FullName).Should().Equal("Ada Lovelace");
    }

    [Fact]
    public async Task GetPageAsync_ShouldSearchAWildcardLiterally()
    {
        // Arrange - a name containing a character the adapter's search treats as a wildcard.
        var backdrop = await Seed.AddBackdropAsync();
        var percentUserId = await Seed.AddUserAsync("100%", "Ada");

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);
        await Seed.AddSeasonPassAsync(percentUserId, backdrop.SeasonId);

        // Act
        var holders = await Query.GetPageAsync(
            Criteria(backdrop.SeasonId) with { NameFilter = "100%" }, Paging(), CancellationToken.None);

        // Assert - one match, not everybody. Escaping is the adapter's to do, because what needs escaping depends on how it
        // searches - the handler passes on what the administrator typed.
        holders.Select(holder => holder.FullName).Should().Equal("100% Ada");
    }

    [Fact]
    public async Task GetPageAsync_ShouldIgnoreSurroundingSpaceInTheNameFilter()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);

        // Act
        var holders = await Query.GetPageAsync(
            Criteria(backdrop.SeasonId) with { NameFilter = "  Lovelace  " }, Paging(), CancellationToken.None);

        // Assert
        holders.Should().HaveCount(1);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetPageAsync_ShouldNotFilterByName_WhenNoNameIsGiven(string? nameFilter)
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);

        // Act
        var holders = await Query.GetPageAsync(
            Criteria(backdrop.SeasonId) with { NameFilter = nameFilter }, Paging(), CancellationToken.None);

        // Assert
        holders.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetPageAsync_ShouldFilterOnTheTotalPaidIncludingAnyFee()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);

        // Act - the seeded pass is free, so a minimum of anything above zero excludes it.
        var holders = await Query.GetPageAsync(
            Criteria(backdrop.SeasonId) with { MinimumPaid = 0.01m }, Paging(), CancellationToken.None);

        // Assert
        holders.Should().BeEmpty();
    }

    [Fact]
    public async Task GetPageAsync_ShouldTreatTheLaterDateBoundAsExclusive()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);

        // Act - a bound of the distant past excludes everything; one in the future includes it. Whoever is asking decides
        // where their day starts, because only they know their time zone.
        var excluded = await Query.GetPageAsync(
            Criteria(backdrop.SeasonId) with { AcquiredBeforeUtc = AcquiredAt.AddYears(-10) }, Paging(), CancellationToken.None);

        var included = await Query.GetPageAsync(
            Criteria(backdrop.SeasonId) with { AcquiredBeforeUtc = DateTime.UtcNow.AddYears(1) }, Paging(), CancellationToken.None);

        // Assert
        excluded.Should().BeEmpty();
        included.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSummaryAsync_ShouldApplyTheSameFiltersAsThePage()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);
        await Seed.AddSeasonPassAsync(otherUserId, backdrop.SeasonId);

        var criteria = Criteria(backdrop.SeasonId) with { NameFilter = "Hopper" };

        // Act
        var summary = await Query.GetSummaryAsync(criteria, CancellationToken.None);
        var holders = await Query.GetPageAsync(criteria, Paging(), CancellationToken.None);

        // Assert - if the two disagreed, the screen would show a count its own rows contradict.
        summary!.MatchingCount.Should().Be(1);
        holders.Should().HaveCount(1);
    }

    #endregion

    #region Sorting and paging

    [Fact]
    public async Task GetPageAsync_ShouldSortByNameInBothDirections()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);
        await Seed.AddSeasonPassAsync(otherUserId, backdrop.SeasonId);

        // Act
        var ascending = await Query.GetPageAsync(Criteria(backdrop.SeasonId), Paging(), CancellationToken.None);
        var descending = await Query.GetPageAsync(
            Criteria(backdrop.SeasonId),
            Paging() with { SortDirection = SortDirection.Descending },
            CancellationToken.None);

        // Assert
        ascending.Select(holder => holder.FullName).Should().Equal("Ada Lovelace", "Grace Hopper");
        descending.Select(holder => holder.FullName).Should().Equal("Grace Hopper", "Ada Lovelace");
    }

    [Theory]
    [InlineData(SeasonPassHolderSortField.AcquiredAt)]
    [InlineData(SeasonPassHolderSortField.TotalPaid)]
    public async Task GetPageAsync_ShouldSortByEveryFieldTheScreenOffers(SeasonPassHolderSortField sortField)
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);
        await Seed.AddSeasonPassAsync(otherUserId, backdrop.SeasonId);

        // Act
        var holders = await Query.GetPageAsync(
            Criteria(backdrop.SeasonId), Paging() with { SortField = sortField }, CancellationToken.None);

        // Assert - the order is pinned by the row id when the sort column ties, so a page boundary cannot shuffle.
        holders.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetPageAsync_ShouldReturnOnlyTheRowsAskedFor()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        var otherUserId = await Seed.AddUserAsync("Grace", "Hopper");

        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId);
        await Seed.AddSeasonPassAsync(otherUserId, backdrop.SeasonId);

        // Act
        var firstPage = await Query.GetPageAsync(
            Criteria(backdrop.SeasonId), Paging() with { Take = 1 }, CancellationToken.None);

        var secondPage = await Query.GetPageAsync(
            Criteria(backdrop.SeasonId), Paging() with { Skip = 1, Take = 1 }, CancellationToken.None);

        // Assert
        firstPage.Select(holder => holder.FullName).Should().Equal("Ada Lovelace");
        secondPage.Select(holder => holder.FullName).Should().Equal("Grace Hopper");
    }

    [Fact]
    public async Task GetPageAsync_ShouldReturnEachHoldersDetails()
    {
        // Arrange
        var backdrop = await Seed.AddBackdropAsync();
        await Seed.AddSeasonPassAsync(backdrop.UserId, backdrop.SeasonId, SeasonPassTier.Premium, SeasonPassSource.Trial);

        // Act
        var holder = (await Query.GetPageAsync(Criteria(backdrop.SeasonId), Paging(), CancellationToken.None)).Single();

        // Assert
        holder.UserId.Should().Be(backdrop.UserId);
        holder.FullName.Should().Be("Ada Lovelace");
        holder.Email.Should().NotBeNullOrWhiteSpace();
        holder.Tier.Should().Be(SeasonPassTier.Premium);
        holder.Source.Should().Be(SeasonPassSource.Trial);
        holder.CreatedAtUtc.Should().NotBe(default);
    }

    #endregion

    private static SeasonPassHoldersCriteria Criteria(int seasonId) =>
        new(seasonId, NameFilter: null, AcquiredFromUtc: null, AcquiredBeforeUtc: null, MinimumPaid: null, MaximumPaid: null);

    private static SeasonPassHoldersPaging Paging() =>
        new(SeasonPassHolderSortField.Name, SortDirection.Ascending, Skip: 0, Take: 25);
}
