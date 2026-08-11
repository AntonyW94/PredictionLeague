using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Rounds.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Infrastructure.Services;
using ThePredictions.Persistence.SqlServer.Queries.Rounds;
using ThePredictions.Persistence.SqlServer.Tests.Integration.Harness;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Persistence.SqlServer.Tests.Integration.Queries;

/// <summary>
/// The rule for "a fixture a player can still act on" is written out twice - once as
/// <c>GetRoundCompletionQueryHandler.PredictableMatchPredicate</c>, once inside
/// <c>ReminderService.GetUsersMissingPredictionsAsync</c> - and each carries a comment telling the reader
/// to change both together. Nothing enforced that. Two copies held together by a comment diverge, and the
/// symptom would be a player chased for predictions they cannot make, or not chased for ones they can.
///
/// This is that enforcement. One theory drives both call sites over the same seeded fixture and asserts
/// they agree, case by case, on every clause the rule encodes: teams confirmed, status still
/// <c>Scheduled</c>, and <c>COALESCE(CustomLockTimeUtc, round deadline) &gt; now</c> so a per-match lock
/// overrides the round deadline in both directions.
///
/// <b>Half collapsed as of August 2026.</b> The round-completion side no longer has a SQL predicate at all -
/// it asks <c>Match.IsOpenForPrediction</c>, the domain rule both copies were only ever mirroring. So this
/// test now compares C# against the SQL copy that remains in <c>ReminderService</c>, which makes it more
/// valuable than when it compared two SQL strings: it is the thing that proves the move preserved behaviour.
/// It stops being a cross-language comparison when the reminder side moves too, and becomes a plain check
/// that both callers of one rule agree.
/// </summary>
[Trait(IntegrationTrait.Name, IntegrationTrait.Value)]
public class PredictableMatchPredicateTests(SqlServerDatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateTime NowUtc = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    // Deadline / lock offsets are hours either side of NowUtc. ReminderService takes the instant as an
    // argument and the query handler takes an IDateTimeProvider, so both can be pinned to it - unlike the
    // boost secrecy predicate, which reads GETUTCDATE().
    [Theory]
    // The round deadline governs when there is no per-match lock.
    [InlineData(+24, null, MatchStatus.Scheduled, true, true, "open round, no custom lock")]
    [InlineData(-24, null, MatchStatus.Scheduled, true, false, "round deadline passed, no custom lock")]
    // A per-match lock overrides the round deadline - in both directions. This is what makes a combined
    // round work: its final can still be open after the deadline that locked the semi-finals.
    [InlineData(-24, +6, MatchStatus.Scheduled, true, true, "round deadline passed but the match locks later")]
    [InlineData(+24, -6, MatchStatus.Scheduled, true, false, "round still open but the match has locked")]
    // Status: only a scheduled fixture can be predicted.
    [InlineData(+24, null, MatchStatus.InProgress, true, false, "match already kicked off")]
    [InlineData(+24, null, MatchStatus.Completed, true, false, "match finished")]
    [InlineData(+24, null, MatchStatus.Postponed, true, false, "match postponed")]
    // A knockout tie with no teams yet cannot be predicted, however open the round is.
    [InlineData(+24, null, MatchStatus.Scheduled, false, false, "teams not yet confirmed")]
    public async Task BothCallSites_ShouldAgreeOnWhetherTheFixtureIsPredictable(
        int deadlineOffsetHours,
        int? customLockOffsetHours,
        MatchStatus status,
        bool teamsConfirmed,
        bool expectedPredictable,
        string scenario)
    {
        // Arrange - one round, one fixture, one approved member who has predicted nothing.
        var world = await ArrangeSingleFixtureAsync(deadlineOffsetHours, customLockOffsetHours, status, teamsConfirmed);

        // Act
        var completion = await RoundCompletionForAsync(world);
        var chased = await ReminderService().GetUsersMissingPredictionsAsync(world.RoundId, NowUtc, CancellationToken.None);

        // Assert - the round-completion view counts the fixture...
        completion.PredictableMatchCount.Should().Be(expectedPredictable ? 1 : 0, scenario);

        // ...and the reminder job chases the player for it. Both or neither; never one of the two.
        chased.Select(c => c.UserId).Should().BeEquivalentTo(
            expectedPredictable ? new[] { world.UserId } : [],
            $"the reminder job must agree with the round-completion view - {scenario}");
    }

    [Fact]
    public async Task RoundCompletion_ShouldListTheMissingFixture_WhenItIsStillPredictable()
    {
        // Arrange - the predicate also drives the per-player list of what is outstanding, which is a
        // third use of it in the same handler and joins to Teams rather than counting.
        var world = await ArrangeSingleFixtureAsync(
            deadlineOffsetHours: +24, customLockOffsetHours: null, MatchStatus.Scheduled, teamsConfirmed: true);

        // Act
        var completion = await RoundCompletionForAsync(world);

        // Assert
        var player = completion.Players.Single(p => p.UserId == world.UserId);
        player.MissingFixtures.Select(f => f.MatchId).Should().BeEquivalentTo(new[] { world.MatchId });
        player.PredictedCount.Should().Be(0);
    }

    [Fact]
    public async Task BothCallSites_ShouldTreatThePlayerAsDone_WhenTheOnlyPredictableFixtureIsPredicted()
    {
        // Arrange - the same open fixture, now predicted.
        var world = await ArrangeSingleFixtureAsync(
            deadlineOffsetHours: +24, customLockOffsetHours: null, MatchStatus.Scheduled, teamsConfirmed: true);
        await Seed.AddPredictionAsync(world.MatchId, world.UserId);

        // Act
        var completion = await RoundCompletionForAsync(world);
        var chased = await ReminderService().GetUsersMissingPredictionsAsync(world.RoundId, NowUtc, CancellationToken.None);

        // Assert - the fixture still counts as predictable; it is the player who is finished.
        completion.PredictableMatchCount.Should().Be(1);

        var player = completion.Players.Single(p => p.UserId == world.UserId);
        player.PredictedCount.Should().Be(1);
        player.MissingFixtures.Should().BeEmpty();

        chased.Should().BeEmpty("there is nothing left to chase them for.");
    }

    [Fact]
    public async Task RoundCompletion_ShouldReportTheDeadlineAsPassed_WhenNothingIsLeftToPredict()
    {
        // Arrange - "passed" for chase purposes means nothing is predictable, not that the clock has run
        // out. The two come apart in a combined round, so the flag is derived from the count.
        var world = await ArrangeSingleFixtureAsync(
            deadlineOffsetHours: -24, customLockOffsetHours: +6, MatchStatus.Scheduled, teamsConfirmed: true);

        // Act
        var completion = await RoundCompletionForAsync(world);

        // Assert
        completion.DeadlinePassed.Should().BeFalse(
            "the round deadline has gone but this fixture locks later, so the round is still open.");
        completion.DeadlineUtc.Should().Be(NowUtc.AddHours(-24), "the reported deadline is still the round's own.");
    }

    #region Arrangement

    private async Task<PredicateWorld> ArrangeSingleFixtureAsync(
        int deadlineOffsetHours,
        int? customLockOffsetHours,
        MatchStatus status,
        bool teamsConfirmed)
    {
        var backdrop = await Seed.AddBackdropAsync();

        var leagueId = await Seed.AddLeagueAsync(backdrop.SeasonId, backdrop.UserId);
        await Seed.AddLeagueMemberAsync(leagueId, backdrop.UserId);

        var roundId = await Seed.AddRoundAsync(
            backdrop.SeasonId, roundNumber: 1, deadlineUtc: NowUtc.AddHours(deadlineOffsetHours));

        var matchId = await Seed.AddMatchAsync(
            roundId,
            homeTeamId: teamsConfirmed ? backdrop.HomeTeamId : null,
            awayTeamId: teamsConfirmed ? backdrop.AwayTeamId : null,
            matchDateTimeUtc: NowUtc.AddHours(deadlineOffsetHours + 1),
            customLockTimeUtc: customLockOffsetHours.HasValue ? NowUtc.AddHours(customLockOffsetHours.Value) : null,
            status: status,
            matchNumber: 1);

        return new PredicateWorld(leagueId, roundId, matchId, backdrop.UserId);
    }

    private async Task<Contracts.Rounds.RoundCompletionDto> RoundCompletionForAsync(PredicateWorld world)
    {
        // Membership and league-administrator checks are separate SQL in their own class; substituting
        // them keeps this test about the predicate. The read connection is the real one.
        var membershipService = Substitute.For<ILeagueMembershipService>();
        membershipService.IsLeagueAdministratorAsync(world.LeagueId, world.UserId, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = new GetRoundCompletionQueryHandler(
            new RoundCompletionQuery(ReadDbConnection), membershipService, new TestDateTimeProvider(NowUtc));

        return await handler.Handle(
            new GetRoundCompletionQuery(world.RoundId, world.LeagueId, world.UserId, IsSiteAdmin: false),
            CancellationToken.None);
    }

    private ReminderService ReminderService() => new(ReadDbConnection);

    private sealed record PredicateWorld(int LeagueId, int RoundId, int MatchId, string UserId);

    #endregion
}
