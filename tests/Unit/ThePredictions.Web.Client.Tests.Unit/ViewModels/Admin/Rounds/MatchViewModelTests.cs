using FluentAssertions;
using ThePredictions.Contracts.Admin.Rounds;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Web.Client.ViewModels.Admin.Rounds;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.ViewModels.Admin.Rounds;

/// <summary>
/// The admin enter-results screen changes scores only through the stepper buttons, so this clamp is the
/// only thing keeping the posted result inside 0-9. Without the bounds an admin holding the down arrow
/// would submit a negative score, which no server-side validator rejects.
/// </summary>
public class MatchViewModelTests
{
    private const int MinimumScore = 0;
    private const int MaximumScore = 9;

    private static MatchViewModel BuildViewModel(int? homeScore = null, int? awayScore = null) =>
        new(new MatchInRoundDto(
            Id: 7,
            MatchDateTimeUtc: new DateTime(2026, 8, 15, 15, 0, 0, DateTimeKind.Utc),
            MatchNumber: 1,
            HomeTeamId: 10,
            HomeTeamName: "Arsenal",
            HomeTeamShortName: "ARS",
            HomeTeamAbbreviation: "ARS",
            HomeTeamLogoUrl: "https://example.test/arsenal.png",
            AwayTeamId: 11,
            AwayTeamName: "Chelsea",
            AwayTeamShortName: "CHE",
            AwayTeamAbbreviation: "CHE",
            AwayTeamLogoUrl: "https://example.test/chelsea.png",
            ActualHomeTeamScore: homeScore,
            ActualAwayTeamScore: awayScore,
            Status: MatchStatus.Scheduled));

    // ---------- mapping from the fixture ----------

    [Fact]
    public void Constructor_ShouldCopyTheFixtureDetails()
    {
        var viewModel = BuildViewModel();

        viewModel.MatchId.Should().Be(7);
        viewModel.HomeTeamName.Should().Be("Arsenal");
        viewModel.AwayTeamName.Should().Be("Chelsea");
        viewModel.Status.Should().Be(MatchStatus.Scheduled);
    }

    // A fixture whose teams are not yet drawn shows a placeholder rather than a blank row.
    [Fact]
    public void Constructor_ShouldFallBackToTbc_WhenATeamIsNotYetKnown()
    {
        var viewModel = new MatchViewModel(new MatchInRoundDto(
            Id: 7,
            MatchDateTimeUtc: new DateTime(2026, 8, 15, 15, 0, 0, DateTimeKind.Utc),
            MatchNumber: 1,
            HomeTeamId: null,
            HomeTeamName: null,
            HomeTeamShortName: null,
            HomeTeamAbbreviation: null,
            HomeTeamLogoUrl: null,
            AwayTeamId: null,
            AwayTeamName: null,
            AwayTeamShortName: null,
            AwayTeamAbbreviation: null,
            AwayTeamLogoUrl: null,
            ActualHomeTeamScore: null,
            ActualAwayTeamScore: null,
            Status: MatchStatus.Scheduled));

        viewModel.HomeTeamName.Should().Be("TBC");
        viewModel.AwayTeamName.Should().Be("TBC");
    }

    [Fact]
    public void Constructor_ShouldStartBothScoresAtZero_WhenNoResultHasBeenEntered()
    {
        var viewModel = BuildViewModel();

        viewModel.HomeScore.Should().Be(MinimumScore);
        viewModel.AwayScore.Should().Be(MinimumScore);
    }

    [Fact]
    public void Constructor_ShouldPreserveAnAlreadyEnteredResult()
    {
        var viewModel = BuildViewModel(homeScore: 3, awayScore: 2);

        viewModel.HomeScore.Should().Be(3);
        viewModel.AwayScore.Should().Be(2);
    }

    // ---------- stepping a score within bounds ----------

    [Fact]
    public void UpdateScore_ShouldIncrementTheHomeScore_AndLeaveTheAwayScoreAlone()
    {
        var viewModel = BuildViewModel(homeScore: 1, awayScore: 1);

        viewModel.UpdateScore(isHomeTeam: true, delta: 1);

        viewModel.HomeScore.Should().Be(2);
        viewModel.AwayScore.Should().Be(1);
    }

    [Fact]
    public void UpdateScore_ShouldIncrementTheAwayScore_AndLeaveTheHomeScoreAlone()
    {
        var viewModel = BuildViewModel(homeScore: 1, awayScore: 1);

        viewModel.UpdateScore(isHomeTeam: false, delta: 1);

        viewModel.AwayScore.Should().Be(2);
        viewModel.HomeScore.Should().Be(1);
    }

    [Fact]
    public void UpdateScore_ShouldDecrementTheScore_WhenTheDeltaIsNegative()
    {
        var viewModel = BuildViewModel(homeScore: 4, awayScore: 4);

        viewModel.UpdateScore(isHomeTeam: true, delta: -1);
        viewModel.UpdateScore(isHomeTeam: false, delta: -1);

        viewModel.HomeScore.Should().Be(3);
        viewModel.AwayScore.Should().Be(3);
    }

    // ---------- the clamp ----------

    [Fact]
    public void UpdateScore_ShouldNotTakeTheHomeScoreBelowZero()
    {
        var viewModel = BuildViewModel(homeScore: MinimumScore);

        viewModel.UpdateScore(isHomeTeam: true, delta: -1);

        viewModel.HomeScore.Should().Be(MinimumScore);
    }

    [Fact]
    public void UpdateScore_ShouldNotTakeTheAwayScoreBelowZero()
    {
        var viewModel = BuildViewModel(awayScore: MinimumScore);

        viewModel.UpdateScore(isHomeTeam: false, delta: -1);

        viewModel.AwayScore.Should().Be(MinimumScore);
    }

    [Fact]
    public void UpdateScore_ShouldNotTakeTheHomeScoreAboveNine()
    {
        var viewModel = BuildViewModel(homeScore: MaximumScore);

        viewModel.UpdateScore(isHomeTeam: true, delta: 1);

        viewModel.HomeScore.Should().Be(MaximumScore);
    }

    [Fact]
    public void UpdateScore_ShouldNotTakeTheAwayScoreAboveNine()
    {
        var viewModel = BuildViewModel(awayScore: MaximumScore);

        viewModel.UpdateScore(isHomeTeam: false, delta: 1);

        viewModel.AwayScore.Should().Be(MaximumScore);
    }

    // A jump that would overshoot the bound is rejected outright rather than clamped to the edge,
    // which is what keeps the stepper's own +1/-1 the only way to reach 9.
    [Fact]
    public void UpdateScore_ShouldIgnoreADeltaThatWouldOvershootTheUpperBound()
    {
        var viewModel = BuildViewModel(homeScore: 8);

        viewModel.UpdateScore(isHomeTeam: true, delta: 5);

        viewModel.HomeScore.Should().Be(8);
    }

    [Fact]
    public void UpdateScore_ShouldIgnoreADeltaThatWouldUndershootTheLowerBound()
    {
        var viewModel = BuildViewModel(homeScore: 1);

        viewModel.UpdateScore(isHomeTeam: true, delta: -5);

        viewModel.HomeScore.Should().Be(1);
    }
}
