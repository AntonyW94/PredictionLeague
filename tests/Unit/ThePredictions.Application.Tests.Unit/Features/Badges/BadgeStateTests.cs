using FluentAssertions;
using ThePredictions.Application.Features.Badges;
using ThePredictions.Application.Features.Badges.Queries;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Badges;

/// <summary>
/// What a player has earned, and how far along they are with the rest, worked out from rows.
///
/// All of this used to be six SQL statements, two of them gap-and-island streak queries. Progress is recomputed on
/// every read rather than stored, so it has to cope with an account that has done nothing at all without falling over.
/// </summary>
public class BadgeStateTests
{
    private static readonly DateTime AwardedUtc = new(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

    #region Earned badges

    [Fact]
    public void From_ShouldReportNothingEarnedAndNoProgress_ForABrandNewAccount()
    {
        var state = BadgeState.From(Data());

        state.Earned.Should().BeEmpty();
        state.Metrics.SeasonExactTotal.Should().Be(0);
        state.Metrics.BestExactsInRound.Should().Be(0);
        state.Metrics.BestStreak.Should().Be(0);
        state.Metrics.CurrentStreak.Should().Be(0);
        state.Metrics.LeaguesJoined.Should().Be(0);
        state.Metrics.EverPresent.Should().BeNull();
    }

    [Fact]
    public void From_ShouldKeyTheEarnedBadgesSoTheyCanBeLookedUp()
    {
        var state = BadgeState.From(Data(awards:
        [
            new BadgeAwardRow("banked", AwardedUtc),
            new BadgeAwardRow("round-winner", AwardedUtc)
        ]));

        state.Earned.Should().HaveCount(2);
        state.Earned.Keys.Should().BeEquivalentTo(["banked", "round-winner"]);
    }

    [Fact]
    public void From_ShouldCountEveryTimeARepeatableBadgeWasWon()
    {
        // The badges page shows "won 3 times", so every award counts here - the opposite of how the leaderboard
        // counts the same rows, which is why they arrive ungrouped.
        var state = BadgeState.From(Data(awards:
        [
            new BadgeAwardRow("round-winner", AwardedUtc),
            new BadgeAwardRow("round-winner", AwardedUtc.AddDays(7)),
            new BadgeAwardRow("round-winner", AwardedUtc.AddDays(14))
        ]));

        state.Earned["round-winner"].Count.Should().Be(3);
    }

    [Fact]
    public void From_ShouldReportTheLatestTimeABadgeWasWon()
    {
        var state = BadgeState.From(Data(awards:
        [
            new BadgeAwardRow("round-winner", AwardedUtc),
            new BadgeAwardRow("round-winner", AwardedUtc.AddDays(14)),
            new BadgeAwardRow("round-winner", AwardedUtc.AddDays(7))
        ]));

        state.Earned["round-winner"].LastAwardedUtc.Should().Be(AwardedUtc.AddDays(14));
    }

    #endregion

    #region Exact score totals

    [Fact]
    public void From_ShouldTotalTheirExactScoresInTheirLatestSeason()
    {
        var state = BadgeState.From(Data(rounds:
        [
            Round(seasonId: 1, roundNumber: 1, exactScoreCount: 9),
            Round(seasonId: 2, roundNumber: 1, exactScoreCount: 3),
            Round(seasonId: 2, roundNumber: 2, exactScoreCount: 4)
        ]));

        // Last season's nine are not carried over: the badge is a per-season tier.
        state.Metrics.SeasonExactTotal.Should().Be(7);
    }

    [Fact]
    public void From_ShouldTotalTheLatestSeasonTheyPlayedIn_NotTheLatestSeasonThatExists()
    {
        // A player who stopped playing still sees what they did, rather than a row of zeroes for a season they
        // never entered.
        var state = BadgeState.From(Data(rounds:
        [
            Round(seasonId: 1, roundNumber: 1, exactScoreCount: 5),
            Round(seasonId: 2, roundNumber: 1, exactScoreCount: null)
        ]));

        state.Metrics.SeasonExactTotal.Should().Be(5);
    }

    [Fact]
    public void From_ShouldReportTheirBestRoundAcrossEverySeason()
    {
        // Unlike the season total, this one is a lifetime best.
        var state = BadgeState.From(Data(rounds:
        [
            Round(seasonId: 1, roundNumber: 1, exactScoreCount: 6),
            Round(seasonId: 2, roundNumber: 1, exactScoreCount: 2)
        ]));

        state.Metrics.BestExactsInRound.Should().Be(6);
    }

    [Fact]
    public void From_ShouldIgnoreRoundsNobodyHasBeenScoredFor()
    {
        // A round with no results at all is not a round anybody could have got an exact score in, whatever the
        // row says.
        var state = BadgeState.From(Data(rounds:
        [
            Round(seasonId: 1, roundNumber: 1, exactScoreCount: 4) with { HasAnyResult = false }
        ]));

        state.Metrics.SeasonExactTotal.Should().Be(0);
        state.Metrics.BestExactsInRound.Should().Be(0);
    }

    #endregion

    #region Streaks

    [Fact]
    public void From_ShouldReportTheirBestRunOfRoundsWithAnExactScore()
    {
        var state = BadgeState.From(Data(rounds:
        [
            Round(seasonId: 1, roundNumber: 1, exactScoreCount: 1),
            Round(seasonId: 1, roundNumber: 2, exactScoreCount: 2),
            Round(seasonId: 1, roundNumber: 3, exactScoreCount: 0),
            Round(seasonId: 1, roundNumber: 4, exactScoreCount: 1)
        ]));

        state.Metrics.BestStreak.Should().Be(2);
    }

    [Fact]
    public void From_ShouldBreakARunOnARoundTheySatOut()
    {
        // Not scored at all is a miss, not a gap to be stepped over - which is why rounds they have no result for
        // have to arrive rather than being filtered out by the read.
        var state = BadgeState.From(Data(rounds:
        [
            Round(seasonId: 1, roundNumber: 1, exactScoreCount: 1),
            Round(seasonId: 1, roundNumber: 2, exactScoreCount: null),
            Round(seasonId: 1, roundNumber: 3, exactScoreCount: 1)
        ]));

        state.Metrics.BestStreak.Should().Be(1);
    }

    [Fact]
    public void From_ShouldOrderARunByRoundNumberRatherThanTheOrderTheRowsArrive()
    {
        var state = BadgeState.From(Data(rounds:
        [
            Round(seasonId: 1, roundNumber: 3, exactScoreCount: 1),
            Round(seasonId: 1, roundNumber: 1, exactScoreCount: 1),
            Round(seasonId: 1, roundNumber: 2, exactScoreCount: 0)
        ]));

        state.Metrics.BestStreak.Should().Be(1);
    }

    [Fact]
    public void From_ShouldNotCarryARunAcrossSeasons()
    {
        // Two rounds either side of the summer are not a run of two.
        var state = BadgeState.From(Data(rounds:
        [
            Round(seasonId: 1, roundNumber: 38, exactScoreCount: 1),
            Round(seasonId: 2, roundNumber: 1, exactScoreCount: 1)
        ]));

        state.Metrics.BestStreak.Should().Be(1);
    }

    [Fact]
    public void From_ShouldTakeTheBestRunOfAnySeason()
    {
        var state = BadgeState.From(Data(rounds:
        [
            Round(seasonId: 1, roundNumber: 1, exactScoreCount: 1),
            Round(seasonId: 1, roundNumber: 2, exactScoreCount: 1),
            Round(seasonId: 1, roundNumber: 3, exactScoreCount: 1),
            Round(seasonId: 2, roundNumber: 1, exactScoreCount: 1)
        ]));

        state.Metrics.BestStreak.Should().Be(3);
    }

    [Fact]
    public void From_ShouldReportACurrentRunOnlyInTheirLatestSeason()
    {
        // Last season's unbroken run is over, whatever it was. Only this season's counts as current.
        var state = BadgeState.From(Data(rounds:
        [
            Round(seasonId: 1, roundNumber: 1, exactScoreCount: 1),
            Round(seasonId: 1, roundNumber: 2, exactScoreCount: 1),
            Round(seasonId: 2, roundNumber: 1, exactScoreCount: 1)
        ]));

        state.Metrics.BestStreak.Should().Be(2);
        state.Metrics.CurrentStreak.Should().Be(1);
    }

    [Fact]
    public void From_ShouldReportNoCurrentRun_WhenTheirLatestScoredRoundHadNoExactScore()
    {
        var state = BadgeState.From(Data(rounds:
        [
            Round(seasonId: 1, roundNumber: 1, exactScoreCount: 2),
            Round(seasonId: 1, roundNumber: 2, exactScoreCount: 0)
        ]));

        state.Metrics.BestStreak.Should().Be(1);
        state.Metrics.CurrentStreak.Should().Be(0);
    }

    [Fact]
    public void From_ShouldReportNoCurrentRun_WhenTheLatestScoredRoundWasOneTheySatOut()
    {
        // Somebody else's round still ends their run: the season has moved on without them.
        var state = BadgeState.From(Data(rounds:
        [
            Round(seasonId: 1, roundNumber: 1, exactScoreCount: 2),
            Round(seasonId: 1, roundNumber: 2, exactScoreCount: null)
        ]));

        state.Metrics.CurrentStreak.Should().Be(0);
    }

    #endregion

    #region Leagues joined

    [Fact]
    public void From_ShouldReportHowManyLeaguesTheyAreIn()
    {
        BadgeState.From(Data(leaguesJoined: 3)).Metrics.LeaguesJoined.Should().Be(3);
    }

    [Fact]
    public void From_ShouldReportHowManyLeaguesTheyAreIn_EvenWithNoRoundsPlayed()
    {
        // The two halves are independent: joining leagues earns a badge before a ball is kicked.
        var state = BadgeState.From(Data(leaguesJoined: 2));

        state.Metrics.LeaguesJoined.Should().Be(2);
        state.Metrics.BestExactsInRound.Should().Be(0);
    }

    #endregion

    #region Ever-present

    [Fact]
    public void From_ShouldReportEverPresentProgressThroughTheirLatestSeason()
    {
        var state = BadgeState.From(Data(rounds:
        [
            Completed(seasonId: 1, roundNumber: 1, matchCount: 10, predictionCount: 10),
            Completed(seasonId: 1, roundNumber: 2, matchCount: 10, predictionCount: 10)
        ]));

        state.Metrics.EverPresent!.RoundsPredicted.Should().Be(2);
        state.Metrics.EverPresent.RoundsTotal.Should().Be(2);
        state.Metrics.EverPresent.Missed.Should().BeFalse();
    }

    [Fact]
    public void From_ShouldMarkEverPresentAsMissed_WhenARoundWasNotFullyPredicted()
    {
        // The badge is unreachable for the season the moment one round is short, and the page says so rather than
        // dangling progress they can no longer finish.
        var state = BadgeState.From(Data(rounds:
        [
            Completed(seasonId: 1, roundNumber: 1, matchCount: 10, predictionCount: 10),
            Completed(seasonId: 1, roundNumber: 2, matchCount: 10, predictionCount: 9)
        ]));

        state.Metrics.EverPresent!.RoundsPredicted.Should().Be(1);
        state.Metrics.EverPresent.RoundsTotal.Should().Be(2);
        state.Metrics.EverPresent.Missed.Should().BeTrue();
    }

    [Fact]
    public void From_ShouldCountARoundWithNoMatchesAgainstThem()
    {
        // A round whose fixtures were never loaded cannot have been fully predicted, so it counts as missed - which
        // matters because "predicted none of no matches" would otherwise look like a full house.
        var state = BadgeState.From(Data(rounds:
        [
            Completed(seasonId: 1, roundNumber: 1, matchCount: 10, predictionCount: 10),
            Completed(seasonId: 1, roundNumber: 2, matchCount: 0, predictionCount: 0)
        ]));

        state.Metrics.EverPresent!.RoundsPredicted.Should().Be(1);
        state.Metrics.EverPresent.Missed.Should().BeTrue();
    }

    [Fact]
    public void From_ShouldIgnoreRoundsThatHaveNotFinished()
    {
        // A round still in play cannot cost them the badge, however few of its matches they have predicted.
        var state = BadgeState.From(Data(rounds:
        [
            Completed(seasonId: 1, roundNumber: 1, matchCount: 10, predictionCount: 10),
            Completed(seasonId: 1, roundNumber: 2, matchCount: 10, predictionCount: 1) with { Status = RoundStatus.InProgress }
        ]));

        state.Metrics.EverPresent!.RoundsTotal.Should().Be(1);
        state.Metrics.EverPresent.Missed.Should().BeFalse();
    }

    [Fact]
    public void From_ShouldJudgeEverPresentOnTheLatestSeasonTheyPredictedIn()
    {
        // Not the latest season that exists: a season they have not entered says nothing about them.
        var state = BadgeState.From(Data(rounds:
        [
            Completed(seasonId: 1, roundNumber: 1, matchCount: 10, predictionCount: 10),
            Completed(seasonId: 2, roundNumber: 1, matchCount: 10, predictionCount: 0)
        ]));

        state.Metrics.EverPresent!.RoundsTotal.Should().Be(1);
        state.Metrics.EverPresent.RoundsPredicted.Should().Be(1);
    }

    [Fact]
    public void From_ShouldJudgeEverPresentOnTheLatestSeasonTheyPredictedIn_EvenPartially()
    {
        // Predicting is what puts them in a season for this badge, not being scored - so a season whose first round
        // has not been scored yet is still the one they are judged on.
        var state = BadgeState.From(Data(rounds:
        [
            Completed(seasonId: 1, roundNumber: 1, matchCount: 10, predictionCount: 10),
            Completed(seasonId: 2, roundNumber: 1, matchCount: 10, predictionCount: 5)
        ]));

        state.Metrics.EverPresent!.RoundsTotal.Should().Be(1);
        state.Metrics.EverPresent.RoundsPredicted.Should().Be(0);
        state.Metrics.EverPresent.Missed.Should().BeTrue();
    }

    [Fact]
    public void From_ShouldLeaveEverPresentUnset_WhenTheyHaveNeverPredicted()
    {
        BadgeState.From(Data(rounds: [Completed(seasonId: 1, roundNumber: 1, matchCount: 10, predictionCount: 0)]))
            .Metrics.EverPresent.Should().BeNull();
    }

    [Fact]
    public void From_ShouldLeaveEverPresentUnset_BeforeAnyRoundHasFinished()
    {
        // Nothing to be ever-present through yet, so there is no progress to show.
        var state = BadgeState.From(Data(rounds:
        [
            Completed(seasonId: 1, roundNumber: 1, matchCount: 10, predictionCount: 10) with { Status = RoundStatus.Published }
        ]));

        state.Metrics.EverPresent.Should().BeNull();
    }

    #endregion

    private static BadgeStateData Data(
        BadgeAwardRow[]? awards = null,
        BadgeRoundRow[]? rounds = null,
        int leaguesJoined = 0) =>
        new("Ada", "Lovelace", awards ?? [], rounds ?? [], leaguesJoined);

    /// <summary>A round somebody has been scored for, and what this player got in it.</summary>
    private static BadgeRoundRow Round(int seasonId, int roundNumber, int? exactScoreCount) =>
        new(seasonId, roundNumber, RoundStatus.Completed, HasAnyResult: true, exactScoreCount, MatchCount: 0, UserPredictionCount: 0);

    /// <summary>A finished round, and how much of it this player predicted.</summary>
    private static BadgeRoundRow Completed(int seasonId, int roundNumber, int matchCount, int predictionCount) =>
        new(seasonId, roundNumber, RoundStatus.Completed, HasAnyResult: false, UserExactScoreCount: null, matchCount, predictionCount);
}
