using FluentAssertions;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Domain.Tests.Unit.Models;

/// <summary>
/// The rule for "a fixture a player can still act on", which two SQL predicates mirrored by comment for a
/// year - one in <c>GetRoundCompletionQueryHandler</c>, one in <c>ReminderService</c>, each telling the reader
/// to change both together and nothing enforcing it.
///
/// The pieces were always here. Only the composition was missing, which is why both call sites rewrote it in
/// T-SQL. These are the cases the integration suite had to spin up a SQL Server container to check.
/// </summary>
public class MatchOpenForPredictionTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsOpenForPrediction_ShouldBeTrue_WhenTheRoundDeadlineIsAheadAndThereIsNoCustomLock()
    {
        Match(customLock: null).IsOpenForPrediction(NowUtc, roundDeadline: NowUtc.AddHours(24))
            .Should().BeTrue();
    }

    [Fact]
    public void IsOpenForPrediction_ShouldBeFalse_WhenTheRoundDeadlineHasPassedAndThereIsNoCustomLock()
    {
        Match(customLock: null).IsOpenForPrediction(NowUtc, roundDeadline: NowUtc.AddHours(-24))
            .Should().BeFalse();
    }

    [Fact]
    public void IsOpenForPrediction_ShouldBeTrue_WhenACustomLockIsAheadEvenThoughTheRoundDeadlineHasPassed()
    {
        // The case that makes a combined round work: its final can still be open after the deadline that
        // locked the semi-finals.
        Match(customLock: NowUtc.AddHours(6)).IsOpenForPrediction(NowUtc, roundDeadline: NowUtc.AddHours(-24))
            .Should().BeTrue();
    }

    [Fact]
    public void IsOpenForPrediction_ShouldBeFalse_WhenACustomLockHasPassedEvenThoughTheRoundIsStillOpen()
    {
        // The override runs both ways: a per-match lock can close a fixture early.
        Match(customLock: NowUtc.AddHours(-6)).IsOpenForPrediction(NowUtc, roundDeadline: NowUtc.AddHours(24))
            .Should().BeFalse();
    }

    [Fact]
    public void IsOpenForPrediction_ShouldBeFalse_WhenTheEffectiveDeadlineIsExactlyNow()
    {
        // IsPredictionLocked uses >=, so a fixture locking exactly now has closed.
        Match(customLock: null).IsOpenForPrediction(NowUtc, roundDeadline: NowUtc).Should().BeFalse();
    }

    [Theory]
    [InlineData(MatchStatus.InProgress)]
    [InlineData(MatchStatus.Completed)]
    [InlineData(MatchStatus.Postponed)]
    public void IsOpenForPrediction_ShouldBeFalse_WhenTheMatchIsNoLongerScheduled(MatchStatus status)
    {
        Match(customLock: null, status: status).IsOpenForPrediction(NowUtc, roundDeadline: NowUtc.AddHours(24))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(null, 2)]
    [InlineData(1, null)]
    [InlineData(null, null)]
    public void IsOpenForPrediction_ShouldBeFalse_WhenTheTeamsAreNotBothConfirmed(int? homeTeamId, int? awayTeamId)
    {
        // A knockout tie whose participants are not yet known cannot be predicted, however open the round.
        Match(customLock: null, homeTeamId: homeTeamId, awayTeamId: awayTeamId)
            .IsOpenForPrediction(NowUtc, roundDeadline: NowUtc.AddHours(24))
            .Should().BeFalse();
    }

    private static Match Match(
        DateTime? customLock,
        MatchStatus status = MatchStatus.Scheduled,
        int? homeTeamId = 1,
        int? awayTeamId = 2) =>
        new(
            id: 1, roundId: 1, homeTeamId: homeTeamId, awayTeamId: awayTeamId,
            matchDateTimeUtc: NowUtc.AddDays(1), customLockTimeUtc: customLock, status: status,
            actualHomeTeamScore: null, actualAwayTeamScore: null, externalId: null, matchNumber: 1,
            placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);
}
