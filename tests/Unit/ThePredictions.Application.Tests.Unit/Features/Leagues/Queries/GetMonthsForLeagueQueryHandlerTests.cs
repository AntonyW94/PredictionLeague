using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// The months a league's leaderboard can be filtered by. The sibling of the stage picker - same rows, same progress
/// counts, a different grouping.
/// </summary>
public class GetMonthsForLeagueQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string UserId = "user-me";

    private readonly ILeagueSeasonRoundsQuery _seasonRoundsQuery = Substitute.For<ILeagueSeasonRoundsQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetMonthsForLeagueQueryHandler _handler;

    public GetMonthsForLeagueQueryHandlerTests()
    {
        _handler = new GetMonthsForLeagueQueryHandler(_seasonRoundsQuery, _membershipService);
    }

    [Fact]
    public async Task Handle_ShouldCheckMembership_BeforeReadingAnything()
    {
        // Arrange
        _membershipService
            .EnsureApprovedMemberAsync(LeagueId, UserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _seasonRoundsQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReturnNothing_ForASeasonWithNoRounds()
    {
        // Arrange
        Given();

        // Act
        var months = await HandleAsync();

        // Assert
        months.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldOfferOneEntryPerMonthWithRoundsInIt()
    {
        // Arrange
        Given(
            Round(1, month: 8),
            Round(2, month: 8),
            Round(3, month: 9));

        // Act
        var months = (await HandleAsync()).ToList();

        // Assert
        months.Select(month => month.Month).Should().Equal(8, 9);
    }

    [Fact]
    public async Task Handle_ShouldRunFromTheSeasonsFirstMonth()
    {
        // Arrange - an August-to-January season, listed out of order. January is in the following calendar year,
        // which is what makes August the earliest round and therefore the season's first month.
        Given(
            Round(5, month: 1, year: 2027),
            Round(1, month: 8),
            Round(3, month: 12));

        // Act
        var months = (await HandleAsync()).ToList();

        // Assert - January belongs at the end of this season, not the start.
        months.Select(month => month.Month).Should().Equal(8, 12, 1);
    }

    [Fact]
    public async Task Handle_ShouldListACalendarYearSeasonInCalendarOrder()
    {
        // Arrange - a season running January to March has nothing to wrap.
        Given(Round(3, month: 3), Round(1, month: 1), Round(2, month: 2));

        // Act
        var months = (await HandleAsync()).ToList();

        // Assert
        months.Select(month => month.Month).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task Handle_ShouldCountRoundsRemainingAndCompleted()
    {
        // Arrange
        Given(
            Round(1, month: 8, status: RoundStatus.Completed),
            Round(2, month: 8, status: RoundStatus.Completed),
            Round(3, month: 8, status: RoundStatus.Published));

        // Act
        var month = (await HandleAsync()).Single();

        // Assert
        month.RoundsCompleted.Should().Be(2);
        month.RoundsRemaining.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldNotOfferAMonthOfNothingButDrafts()
    {
        // Arrange
        Given(
            Round(1, month: 8, status: RoundStatus.Published),
            Round(2, month: 9, status: RoundStatus.Draft));

        // Act
        var months = (await HandleAsync()).ToList();

        // Assert
        months.Select(month => month.Month).Should().Equal(8);
    }

    [Fact]
    public async Task Handle_ShouldStillOfferAMonthThatHasOnePublishedRoundAmongDrafts()
    {
        // Arrange
        Given(
            Round(1, month: 8, status: RoundStatus.Published),
            Round(2, month: 8, status: RoundStatus.Draft));

        // Act
        var month = (await HandleAsync()).Single();

        // Assert - and the draft counts as still to come, as it always did.
        month.RoundsRemaining.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldNameTheMonthInEnglish()
    {
        // Arrange - the old handler used CultureInfo.CurrentCulture, so the name depended on the server's locale.
        Given(Round(1, month: 3));

        // Act
        var month = (await HandleAsync()).Single();

        // Assert
        month.Name.Should().Be("March");
    }

    private void Given(params LeagueSeasonRoundRow[] rounds)
    {
        _seasonRoundsQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(rounds);
    }

    private async Task<IEnumerable<MonthDto>> HandleAsync() =>
        await _handler.Handle(new GetMonthsForLeagueQuery(LeagueId, UserId), CancellationToken.None);

    private static LeagueSeasonRoundRow Round(
        int roundNumber,
        int month,
        RoundStatus status = RoundStatus.Published,
        int year = 2026) =>
        new(
            roundNumber,
            roundNumber,
            new DateTime(year, month, 10, 0, 0, 0, DateTimeKind.Utc),
            status,
            null);
}
