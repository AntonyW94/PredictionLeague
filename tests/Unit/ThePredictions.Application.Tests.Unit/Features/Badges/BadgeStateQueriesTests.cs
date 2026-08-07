using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Badges;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Badges;

/// <summary>
/// Loads the badges someone has earned plus the live progress behind the ones they have not.
/// Progress is always worked out fresh rather than stored, so this has to cope with an account that
/// has done nothing yet without falling over.
/// </summary>
public class BadgeStateQueriesTests
{
    private const string UserId = "user-1";

    private static readonly DateTime AwardedUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();

    private void GivenEarnedBadges(params EarnedBadge[] badges) =>
        _dbConnection.QueryAsync<EarnedBadge>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(badges);

    private void GivenScalars(int seasonExactTotal, int bestExactsInRound, int leaguesJoined) =>
        _dbConnection.QuerySingleOrDefaultAsync<BadgeStateQueries.MetricScalars>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(new BadgeStateQueries.MetricScalars(seasonExactTotal, bestExactsInRound, leaguesJoined));

    private void GivenEverPresent(int roundsTotal, int roundsPredicted) =>
        _dbConnection.QuerySingleOrDefaultAsync<BadgeStateQueries.EverPresentRow>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(new BadgeStateQueries.EverPresentRow(roundsTotal, roundsPredicted));

    private void GivenStreaks(int best, int current)
    {
        var call = 0;
        _dbConnection.QuerySingleOrDefaultAsync<int>(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(_ => call++ == 0 ? best : current);
    }

    private Task<BadgeUserState> LoadAsync() =>
        BadgeStateQueries.LoadAsync(_dbConnection, UserId, CancellationToken.None);

    [Fact]
    public async Task LoadAsync_ShouldReportNothingEarnedAndNoProgress_ForABrandNewAccount()
    {
        // Every query comes back empty, and the page still has to render rather than throw.
        var state = await LoadAsync();

        state.Earned.Should().BeEmpty();
        state.Metrics.SeasonExactTotal.Should().Be(0);
        state.Metrics.BestExactsInRound.Should().Be(0);
        state.Metrics.LeaguesJoined.Should().Be(0);
        state.Metrics.EverPresent.Should().BeNull();
    }

    [Fact]
    public async Task LoadAsync_ShouldKeyTheEarnedBadgesSoTheyCanBeLookedUp()
    {
        GivenEarnedBadges(
            new EarnedBadge("banked", 1, AwardedUtc, null),
            new EarnedBadge("round-winner", 3, AwardedUtc, "Gameweek 5"));

        var state = await LoadAsync();

        state.Earned.Should().HaveCount(2);
        state.Earned["banked"].Count.Should().Be(1);
        state.Earned["round-winner"].Count.Should().Be(3);
        state.Earned["round-winner"].Detail.Should().Be("Gameweek 5");
    }

    [Fact]
    public async Task LoadAsync_ShouldReportTheProgressCounts()
    {
        GivenScalars(seasonExactTotal: 14, bestExactsInRound: 4, leaguesJoined: 2);

        var state = await LoadAsync();

        state.Metrics.SeasonExactTotal.Should().Be(14);
        state.Metrics.BestExactsInRound.Should().Be(4);
        state.Metrics.LeaguesJoined.Should().Be(2);
    }

    [Fact]
    public async Task LoadAsync_ShouldReportBothTheBestAndCurrentRuns()
    {
        // They are different badges: one for the best run ever, one for the run still going.
        GivenStreaks(best: 6, current: 2);

        var state = await LoadAsync();

        state.Metrics.BestStreak.Should().Be(6);
        state.Metrics.CurrentStreak.Should().Be(2);
    }

    [Fact]
    public async Task LoadAsync_ShouldReportEverPresentProgress()
    {
        GivenEverPresent(roundsTotal: 10, roundsPredicted: 8);

        var state = await LoadAsync();

        state.Metrics.EverPresent!.RoundsTotal.Should().Be(10);
        state.Metrics.EverPresent.RoundsPredicted.Should().Be(8);
    }

    [Fact]
    public async Task LoadAsync_ShouldMarkEverPresentAsMissed_WhenARoundWasNotFullyPredicted()
    {
        // The badge is unreachable for this season the moment one round is incomplete, and the page
        // shows that rather than dangling progress they can no longer finish.
        GivenEverPresent(roundsTotal: 10, roundsPredicted: 8);

        var state = await LoadAsync();

        state.Metrics.EverPresent!.Missed.Should().BeTrue();
    }

    [Fact]
    public async Task LoadAsync_ShouldNotMarkEverPresentAsMissed_WhileTheyAreStillOnTrack()
    {
        GivenEverPresent(roundsTotal: 10, roundsPredicted: 10);

        var state = await LoadAsync();

        state.Metrics.EverPresent!.Missed.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAsync_ShouldLeaveEverPresentUnset_BeforeAnyRoundHasFinished()
    {
        // Nothing to be ever-present through yet, so there is no progress to show.
        GivenEverPresent(roundsTotal: 0, roundsPredicted: 0);

        var state = await LoadAsync();

        state.Metrics.EverPresent.Should().BeNull();
    }
}
