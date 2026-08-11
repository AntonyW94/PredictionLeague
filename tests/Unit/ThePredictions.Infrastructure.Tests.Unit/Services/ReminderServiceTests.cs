using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Infrastructure.Services;
using Xunit;

namespace ThePredictions.Infrastructure.Tests.Unit.Services;

/// <summary>
/// When a prediction reminder is due, and who gets one.
///
/// This class was excluded from coverage while it held SQL - two reads carrying three rules the domain
/// already owned. Both reads are now ports, so the milestone schedule and the chase decision are ordinary
/// code and measured. The chase tests in particular used to be impossible without a database.
/// </summary>
public class ReminderServiceTests
{
    private static readonly DateTime DeadlineUtc = new(2026, 8, 20, 18, 0, 0, DateTimeKind.Utc);

    private readonly IRoundCompletionQuery _completionQuery = Substitute.For<IRoundCompletionQuery>();
    private readonly IEarlierRoundStatusesQuery _earlierStatuses = Substitute.For<IEarlierRoundStatusesQuery>();
    private readonly ReminderService _service;

    public ReminderServiceTests()
    {
        _service = new ReminderService(_completionQuery, _earlierStatuses);
        GivenEarlierRounds(RoundStatus.Completed);
    }

    #region ShouldSendReminderAsync

    [Fact]
    public async Task ShouldSendReminderAsync_ShouldBeFalse_WhenNoFixtureIsStillOpen()
    {
        // No next lock, so there is no milestone to measure against. Postponed rather than Completed
        // deliberately: Round.GetNextPredictionDeadline skips only postponed fixtures, whereas
        // Match.IsOpenForPrediction requires Scheduled. The two rules disagree about a Completed fixture
        // whose lock is still ahead - see the note in the persistence-split plan. Practically unreachable
        // (a match is not completed before it has kicked off, which is after its lock) but the asymmetry is
        // real, and this test stays on the case both rules agree about.
        var round = Round(matches: [Fixture(1, status: MatchStatus.Postponed)]);

        (await _service.ShouldSendReminderAsync(round, DeadlineUtc.AddDays(-1), CancellationToken.None))
            .Should().BeFalse();
    }

    [Fact]
    public async Task ShouldSendReminderAsync_ShouldBeFalse_BeforeTheEarliestMilestone()
    {
        var round = Round();

        (await _service.ShouldSendReminderAsync(round, DeadlineUtc.AddDays(-10), CancellationToken.None))
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(-3)]
    [InlineData(-1)]
    public async Task ShouldSendReminderAsync_ShouldBeTrue_AtEachMilestone_WhenNothingHasBeenSentYet(int daysOut)
    {
        var round = Round();

        (await _service.ShouldSendReminderAsync(round, DeadlineUtc.AddDays(daysOut), CancellationToken.None))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ShouldSendReminderAsync_ShouldBeTrue_AtTheOneHourMilestone()
    {
        var round = Round();

        (await _service.ShouldSendReminderAsync(round, DeadlineUtc.AddHours(-1), CancellationToken.None))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ShouldSendReminderAsync_ShouldBeFalse_WhenOneWasAlreadySentForThatMilestone()
    {
        var round = Round(lastReminderSentUtc: DeadlineUtc.AddHours(-5));

        (await _service.ShouldSendReminderAsync(round, DeadlineUtc.AddHours(-4), CancellationToken.None))
            .Should().BeFalse("the six-hour milestone has already been served.");
    }

    [Fact]
    public async Task ShouldSendReminderAsync_ShouldBeTrue_WhenTheLastOnePredatesTheCurrentMilestone()
    {
        var round = Round(lastReminderSentUtc: DeadlineUtc.AddDays(-4));

        (await _service.ShouldSendReminderAsync(round, DeadlineUtc.AddHours(-1), CancellationToken.None))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ShouldSendReminderAsync_ShouldHoldBackTheEarlyMilestones_WhileAnEarlierRoundIsUnfinished()
    {
        // Tournament rounds sit close together, so a five-day reminder can land before the previous round
        // has even been scored - and players want those results first.
        GivenEarlierRounds(RoundStatus.Completed, RoundStatus.InProgress);
        var round = Round();

        (await _service.ShouldSendReminderAsync(round, DeadlineUtc.AddDays(-5), CancellationToken.None))
            .Should().BeFalse();
    }

    [Fact]
    public async Task ShouldSendReminderAsync_ShouldStillSendTheLateMilestones_WhenAnEarlierRoundIsUnfinished()
    {
        // The deadline is imminent regardless of how late the previous round finished.
        GivenEarlierRounds(RoundStatus.InProgress);
        var round = Round();

        (await _service.ShouldSendReminderAsync(round, DeadlineUtc.AddHours(-6), CancellationToken.None))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ShouldSendReminderAsync_ShouldAllowEarlyMilestones_WhenThisIsTheFirstRound()
    {
        // No earlier rounds at all, so there is nothing to wait for.
        GivenEarlierRounds();
        var round = Round();

        (await _service.ShouldSendReminderAsync(round, DeadlineUtc.AddDays(-5), CancellationToken.None))
            .Should().BeTrue();
    }

    [Fact]
    public async Task ShouldSendReminderAsync_ShouldMeasureFromTheNextLock_NotTheRoundDeadline()
    {
        // A combined round: the deadline has gone but the final locks later, so the milestone schedule
        // restarts against that later lock.
        var laterLock = DeadlineUtc.AddDays(3);
        var round = Round(
            matches: [Fixture(1, customLock: laterLock)],
            deadlineUtc: DeadlineUtc);

        (await _service.ShouldSendReminderAsync(round, laterLock.AddHours(-1), CancellationToken.None))
            .Should().BeTrue("the one-hour milestone of the later lock has passed.");

        // Four days before the round's own deadline discriminates between the two anchorings: measured from
        // the round deadline the five-day milestone has passed and a reminder is due; measured from the
        // later lock the earliest milestone is still two days away.
        (await _service.ShouldSendReminderAsync(round, DeadlineUtc.AddDays(-4), CancellationToken.None))
            .Should().BeFalse("nothing is due yet relative to the later lock.");
    }

    #endregion

    #region GetUsersMissingPredictionsAsync

    [Fact]
    public async Task GetUsersMissingPredictionsAsync_ShouldBeEmpty_WhenTheRoundDoesNotExist()
    {
        _completionQuery.ExecuteAsync(7, null, Arg.Any<CancellationToken>()).Returns((RoundCompletionData?)null);

        (await _service.GetUsersMissingPredictionsAsync(7, DeadlineUtc.AddDays(-1), CancellationToken.None))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsersMissingPredictionsAsync_ShouldBeEmpty_WhenNoFixtureIsStillOpen()
    {
        GivenData(fixtures: [Fixture(1, status: MatchStatus.Postponed)]);

        (await _service.GetUsersMissingPredictionsAsync(7, DeadlineUtc.AddDays(-1), CancellationToken.None))
            .Should().BeEmpty("nobody should be nagged about a fixture they cannot change.");
    }

    [Fact]
    public async Task GetUsersMissingPredictionsAsync_ShouldChaseAPlayerMissingOneOfTwoOpenFixtures()
    {
        GivenData(
            fixtures: [Fixture(1), Fixture(2)],
            participants: [Participant("u1")],
            predictions: [new RoundPredictionRow("u1", 1)]);

        var chased = await _service.GetUsersMissingPredictionsAsync(7, DeadlineUtc.AddDays(-1), CancellationToken.None);

        chased.Select(c => c.UserId).Should().Equal("u1");
    }

    [Fact]
    public async Task GetUsersMissingPredictionsAsync_ShouldNotChaseAPlayerWhoHasEnteredEveryOpenFixture()
    {
        GivenData(
            fixtures: [Fixture(1), Fixture(2)],
            participants: [Participant("u1")],
            predictions: [new RoundPredictionRow("u1", 1), new RoundPredictionRow("u1", 2)]);

        (await _service.GetUsersMissingPredictionsAsync(7, DeadlineUtc.AddDays(-1), CancellationToken.None))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsersMissingPredictionsAsync_ShouldIgnoreAPredictionForALockedFixture()
    {
        // Predicting a fixture that has since locked must not count as being up to date on the open one.
        GivenData(
            fixtures: [Fixture(1), Fixture(2, status: MatchStatus.Completed)],
            participants: [Participant("u1")],
            predictions: [new RoundPredictionRow("u1", 2)]);

        (await _service.GetUsersMissingPredictionsAsync(7, DeadlineUtc.AddDays(-1), CancellationToken.None))
            .Should().HaveCount(1);
    }

    [Fact]
    public async Task GetUsersMissingPredictionsAsync_ShouldCarryTheEmailFirstNameAndRoundName()
    {
        GivenData(displayName: "Quarter Finals", participants: [Participant("u1")]);

        var chased = (await _service.GetUsersMissingPredictionsAsync(7, DeadlineUtc.AddDays(-1), CancellationToken.None)).Single();

        chased.Email.Should().Be("u1@example.com");
        chased.FirstName.Should().Be("Ada");
        chased.RoundName.Should().Be("Quarter Finals");
    }

    [Fact]
    public async Task GetUsersMissingPredictionsAsync_ShouldFallBackToTheRoundNumberForItsName()
    {
        GivenData(displayName: "  ", participants: [Participant("u1")]);

        (await _service.GetUsersMissingPredictionsAsync(7, DeadlineUtc.AddDays(-1), CancellationToken.None))
            .Single().RoundName.Should().Be("Round 5");
    }

    [Fact]
    public async Task GetUsersMissingPredictionsAsync_ShouldReportTheNextLockAsTheDeadline_NotTheRounds()
    {
        // The same rule the milestone schedule uses, so the email and the send decision agree.
        var laterLock = DeadlineUtc.AddDays(3);
        GivenData(fixtures: [Fixture(1, customLock: laterLock)], participants: [Participant("u1")]);

        (await _service.GetUsersMissingPredictionsAsync(7, DeadlineUtc.AddDays(-1), CancellationToken.None))
            .Single().DeadlineUtc.Should().Be(laterLock);
    }

    [Fact]
    public async Task GetUsersMissingPredictionsAsync_ShouldChaseEveryMemberWhoIsBehind()
    {
        GivenData(
            fixtures: [Fixture(1)],
            participants: [Participant("u1"), Participant("u2"), Participant("u3")],
            predictions: [new RoundPredictionRow("u2", 1)]);

        var chased = await _service.GetUsersMissingPredictionsAsync(7, DeadlineUtc.AddDays(-1), CancellationToken.None);

        chased.Select(c => c.UserId).Should().BeEquivalentTo(["u1", "u3"]);
    }

    #endregion

    #region Helpers

    private void GivenEarlierRounds(params RoundStatus[] statuses) =>
        _earlierStatuses.ExecuteAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(statuses);

    private void GivenData(
        string displayName = "Round 5",
        IReadOnlyList<Match>? fixtures = null,
        IReadOnlyList<RoundParticipantRow>? participants = null,
        IReadOnlyList<RoundPredictionRow>? predictions = null)
    {
        var matches = fixtures ?? [Fixture(1)];

        var data = new RoundCompletionData(
            Round(matches: matches, displayName: displayName),
            matches.ToDictionary(m => m.Id, _ => new RoundFixtureTeams("Home", "Away")),
            participants ?? [Participant("u1")],
            predictions ?? []);

        _completionQuery.ExecuteAsync(7, null, Arg.Any<CancellationToken>()).Returns(data);
    }

    private static Round Round(
        IEnumerable<Match>? matches = null,
        DateTime? deadlineUtc = null,
        DateTime? lastReminderSentUtc = null,
        string displayName = "Round 5") =>
        new(
            id: 7, seasonId: 1, roundNumber: 5, displayName: displayName,
            startDateUtc: (deadlineUtc ?? DeadlineUtc).AddDays(-1),
            deadlineUtc: deadlineUtc ?? DeadlineUtc,
            status: RoundStatus.Published, apiRoundName: null,
            lastReminderSentUtc: lastReminderSentUtc,
            matches: matches ?? [Fixture(1)], resultsDigestSentUtc: null);

    private static Match Fixture(int id, DateTime? customLock = null, MatchStatus status = MatchStatus.Scheduled) =>
        new(
            id: id, roundId: 7, homeTeamId: 1, awayTeamId: 2,
            matchDateTimeUtc: DeadlineUtc.AddHours(1), customLockTimeUtc: customLock, status: status,
            actualHomeTeamScore: null, actualAwayTeamScore: null, externalId: null, matchNumber: id,
            placeholderHomeName: null, placeholderAwayName: null, apiRoundName: null);

    private static RoundParticipantRow Participant(string userId) =>
        new(userId, "Ada", "Lovelace", $"{userId}@example.com", LastRemindedUtc: null);

    #endregion
}
