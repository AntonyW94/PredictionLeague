using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Data;
using ThePredictions.Application.Features.Dashboard.Queries;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Common.Enumerations;
using Xunit;
using static ThePredictions.Application.Features.Dashboard.Queries.GetActiveRoundsQueryHandler;

namespace ThePredictions.Application.Tests.Unit.Features.Dashboard.Queries;

/// <summary>
/// The dashboard's active-rounds tiles. The rule that matters here is secrecy: each match shows how the
/// league as a whole has predicted, and that split must stay hidden until the match itself has locked.
/// Revealing it early would let a player see the crowd's answer while they can still change their own,
/// so the counts are zeroed rather than merely hidden by the UI - a client that ignored the flag would
/// still learn nothing. In a combined round the earlier matches reveal at the round deadline while the
/// later ones stay hidden until their own custom lock time, so the decision is per match, not per round.
/// </summary>
public class GetActiveRoundsQueryHandlerTests
{
    private const string UserId = "user-1";
    private const int RoundId = 5;

    private static readonly DateTime NowUtc = new(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime PastDeadline = new(2026, 8, 9, 11, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime FutureDeadline = new(2026, 8, 9, 18, 0, 0, DateTimeKind.Utc);

    private readonly IApplicationReadDbConnection _dbConnection = Substitute.For<IApplicationReadDbConnection>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();
    private readonly GetActiveRoundsQueryHandler _handler;

    public GetActiveRoundsQueryHandlerTests()
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);
        _handler = new GetActiveRoundsQueryHandler(_dbConnection, _dateTimeProvider);
    }

    // ---------- arrange helpers ----------

    private void GivenRounds(params ActiveRoundQueryResult[] rounds) =>
        _dbConnection.QueryAsync<ActiveRoundQueryResult>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(rounds);

    private void GivenMatches(params ActiveRoundMatchQueryResult[] matches) =>
        _dbConnection.QueryAsync<ActiveRoundMatchQueryResult>(
                Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<object?>())
            .Returns(matches);

    private static ActiveRoundQueryResult Round(
        int id = RoundId,
        DateTime? deadlineUtc = null,
        DateTime? latestPredictionDeadlineUtc = null,
        bool hasUserPredicted = true,
        RoundStatus status = RoundStatus.InProgress,
        CompetitionType competitionType = CompetitionType.League) =>
        new(id,
            "2026/27",
            RoundNumber: 3,
            DeadlineUtc: deadlineUtc ?? FutureDeadline,
            Status: status.ToString(),
            HasUserPredicted: hasUserPredicted,
            RoundDisplayName: null,
            CompetitionType: (int)competitionType,
            LatestPredictionDeadlineUtc: latestPredictionDeadlineUtc ?? deadlineUtc ?? FutureDeadline);

    private static ActiveRoundMatchQueryResult Match(
        int roundId = RoundId,
        DateTime? customLockTimeUtc = null,
        PredictionOutcome? outcome = null,
        int homeCount = 7,
        int drawCount = 2,
        int awayCount = 1,
        MatchStatus status = MatchStatus.Scheduled) =>
        new(roundId,
            HomeTeamLogoUrl: "https://example.test/home.png",
            AwayTeamLogoUrl: "https://example.test/away.png",
            PredictedHomeScore: 2,
            PredictedAwayScore: 1,
            Outcome: outcome,
            Status: status.ToString(),
            ActualHomeScore: null,
            ActualAwayScore: null,
            MatchDateTimeUtc: new DateTime(2026, 8, 9, 19, 0, 0, DateTimeKind.Utc),
            MatchNumber: 1,
            AreTeamsConfirmed: true,
            PlaceholderHomeName: null,
            PlaceholderAwayName: null,
            HomeCount: homeCount,
            DrawCount: drawCount,
            AwayCount: awayCount,
            CustomLockTimeUtc: customLockTimeUtc);

    private async Task<List<ActiveRoundDto>> HandleAsync() =>
        (await _handler.Handle(new GetActiveRoundsQuery(UserId), CancellationToken.None)).ToList();

    // ---------- which rounds count as active ----------

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenThereAreNoActiveRounds()
    {
        GivenRounds();

        (await HandleAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldIncludeARoundStillOpenForPredictions()
    {
        GivenRounds(Round(latestPredictionDeadlineUtc: FutureDeadline, hasUserPredicted: false));
        GivenMatches(Match());

        (await HandleAsync()).Should().HaveCount(1);
    }

    // A closed round stays on the dashboard only if the player took part in it - that is what makes it
    // "theirs" to review.
    [Fact]
    public async Task Handle_ShouldIncludeAClosedRound_WhenThePlayerPredictedInIt()
    {
        GivenRounds(Round(latestPredictionDeadlineUtc: PastDeadline, hasUserPredicted: true));
        GivenMatches(Match());

        (await HandleAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_ShouldDropAClosedRound_WhenThePlayerDidNotPredictInIt()
    {
        GivenRounds(Round(latestPredictionDeadlineUtc: PastDeadline, hasUserPredicted: false));

        (await HandleAsync()).Should().BeEmpty();
    }

    // ---------- the prediction-split secrecy rule ----------

    [Fact]
    public async Task Handle_ShouldHideThePredictionSplit_WhileTheMatchIsStillOpen()
    {
        GivenRounds(Round(deadlineUtc: FutureDeadline));
        GivenMatches(Match(homeCount: 7, drawCount: 2, awayCount: 1));

        var match = (await HandleAsync()).Single().Matches.Single();

        match.IsPredictionRevealed.Should().BeFalse();
        match.HomeCount.Should().Be(0);
        match.DrawCount.Should().Be(0);
        match.AwayCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRevealThePredictionSplit_OnceTheRoundDeadlineHasPassed()
    {
        GivenRounds(Round(deadlineUtc: PastDeadline));
        GivenMatches(Match(homeCount: 7, drawCount: 2, awayCount: 1));

        var match = (await HandleAsync()).Single().Matches.Single();

        match.IsPredictionRevealed.Should().BeTrue();
        match.HomeCount.Should().Be(7);
        match.DrawCount.Should().Be(2);
        match.AwayCount.Should().Be(1);
    }

    // A per-match lock overrides the round deadline in both directions. This is the combined-round case:
    // the round deadline has passed, but this match locks later and must stay hidden.
    [Fact]
    public async Task Handle_ShouldKeepTheSplitHidden_WhenTheMatchLocksAfterThePassedRoundDeadline()
    {
        GivenRounds(Round(deadlineUtc: PastDeadline));
        GivenMatches(Match(customLockTimeUtc: FutureDeadline, homeCount: 7));

        var match = (await HandleAsync()).Single().Matches.Single();

        match.IsPredictionRevealed.Should().BeFalse();
        match.HomeCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRevealTheSplit_WhenTheMatchLockedBeforeTheStillFutureRoundDeadline()
    {
        GivenRounds(Round(deadlineUtc: FutureDeadline));
        GivenMatches(Match(customLockTimeUtc: PastDeadline, homeCount: 7));

        var match = (await HandleAsync()).Single().Matches.Single();

        match.IsPredictionRevealed.Should().BeTrue();
        match.HomeCount.Should().Be(7);
    }

    // Within one round the decision is taken per match, so a combined round can show one revealed and
    // one hidden match at the same moment.
    [Fact]
    public async Task Handle_ShouldDecideTheSplitPerMatch_WithinTheSameRound()
    {
        GivenRounds(Round(deadlineUtc: PastDeadline));
        GivenMatches(
            Match(homeCount: 7),
            Match(customLockTimeUtc: FutureDeadline, homeCount: 5));

        var matches = (await HandleAsync()).Single().Matches.ToList();

        matches.Should().HaveCount(2);
        matches.Count(m => m.IsPredictionRevealed).Should().Be(1);
        matches.Single(m => m.IsPredictionRevealed).HomeCount.Should().Be(7);
        matches.Single(m => !m.IsPredictionRevealed).HomeCount.Should().Be(0);
    }

    // ---------- outcome summary ----------

    [Fact]
    public async Task Handle_ShouldSummariseOutcomes_OnceTheDeadlineHasPassedForAPlayerWhoPredicted()
    {
        GivenRounds(Round(deadlineUtc: PastDeadline, hasUserPredicted: true));
        GivenMatches(
            Match(outcome: PredictionOutcome.ExactScore),
            Match(outcome: PredictionOutcome.ExactScore),
            Match(outcome: PredictionOutcome.CorrectResult),
            Match(outcome: PredictionOutcome.Incorrect));

        var summary = (await HandleAsync()).Single().OutcomeSummary;

        summary.Should().NotBeNull();
        summary!.ExactScoreCount.Should().Be(2);
        summary.CorrectResultCount.Should().Be(1);
        summary.IncorrectCount.Should().Be(1);
    }

    // Before the deadline there is nothing to summarise - scoring has not happened yet.
    [Fact]
    public async Task Handle_ShouldNotSummariseOutcomes_WhileTheRoundIsStillOpen()
    {
        GivenRounds(Round(deadlineUtc: FutureDeadline, hasUserPredicted: true));
        GivenMatches(Match(outcome: PredictionOutcome.ExactScore));

        (await HandleAsync()).Single().OutcomeSummary.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldNotSummariseOutcomes_ForAPlayerWhoDidNotPredict()
    {
        GivenRounds(Round(
            deadlineUtc: PastDeadline,
            latestPredictionDeadlineUtc: FutureDeadline,
            hasUserPredicted: false));
        GivenMatches(Match(outcome: PredictionOutcome.ExactScore));

        (await HandleAsync()).Single().OutcomeSummary.Should().BeNull();
    }

    // ---------- round shaping ----------

    [Fact]
    public async Task Handle_ShouldReturnNoMatches_WhenTheRoundHasNone()
    {
        GivenRounds(Round(deadlineUtc: PastDeadline, hasUserPredicted: true));
        GivenMatches(Match(roundId: 999));

        var round = (await HandleAsync()).Single();

        round.Matches.Should().BeEmpty();
        round.OutcomeSummary.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldGroupMatchesOntoTheirOwnRound()
    {
        GivenRounds(
            Round(id: 1, deadlineUtc: FutureDeadline),
            Round(id: 2, deadlineUtc: FutureDeadline));
        GivenMatches(
            Match(roundId: 1),
            Match(roundId: 2),
            Match(roundId: 2));

        var rounds = (await HandleAsync()).ToList();

        rounds.Single(r => r.Id == 1).Matches.Should().HaveCount(1);
        rounds.Single(r => r.Id == 2).Matches.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldFlagATournamentRound()
    {
        GivenRounds(Round(competitionType: CompetitionType.Tournament));
        GivenMatches(Match());

        (await HandleAsync()).Single().IsTournament.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldNotFlagALeagueRoundAsATournament()
    {
        GivenRounds(Round(competitionType: CompetitionType.League));
        GivenMatches(Match());

        (await HandleAsync()).Single().IsTournament.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldCarryTheRoundStatusThrough()
    {
        GivenRounds(Round(status: RoundStatus.Completed, deadlineUtc: PastDeadline));
        GivenMatches(Match());

        (await HandleAsync()).Single().Status.Should().Be(RoundStatus.Completed);
    }

    [Fact]
    public async Task Handle_ShouldCarryTheMatchStatusThrough()
    {
        GivenRounds(Round());
        GivenMatches(Match(status: MatchStatus.InProgress));

        (await HandleAsync()).Single().Matches.Single().Status.Should().Be(MatchStatus.InProgress);
    }
}
