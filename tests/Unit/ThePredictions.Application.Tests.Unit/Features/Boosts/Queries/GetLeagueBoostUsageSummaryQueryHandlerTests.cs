using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Boosts.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Boosts.Queries;

/// <summary>
/// This handler was excluded from coverage while it held SQL. It is now measured, and the reason that
/// matters is <see cref="Handle_ShouldCensorBeforeShaping_SoAnotherPlayersOpenRoundBoostNeverReachesThePage"/>:
/// moving a rule out of SQL creates a failure mode SQL could not have, where the rule exists, is correct,
/// is unit tested - and the handler forgets to call it. Nothing but a test at this level catches that.
/// </summary>
public class GetLeagueBoostUsageSummaryQueryHandlerTests
{
    private static readonly DateTime NowUtc = new(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc);

    private const int LeagueId = 42;
    private const string Me = "user-me";
    private const string Opponent = "user-opponent";

    private readonly ILeagueBoostUsageQuery _usageQuery = Substitute.For<ILeagueBoostUsageQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetLeagueBoostUsageSummaryQueryHandler _handler;

    public GetLeagueBoostUsageSummaryQueryHandlerTests()
    {
        _handler = new GetLeagueBoostUsageSummaryQueryHandler(
            _usageQuery, _membershipService, new TestDateTimeProvider(NowUtc));
    }

    [Fact]
    public async Task Handle_ShouldEnsureTheCallerIsAnApprovedMember_BeforeReadingAnything()
    {
        // Arrange
        Returns(null);

        // Act
        await _handler.Handle(Query(Me), CancellationToken.None);

        // Assert
        await _membershipService.Received(1).EnsureApprovedMemberAsync(LeagueId, Me, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldCensorBeforeShaping_SoAnotherPlayersOpenRoundBoostNeverReachesThePage()
    {
        // Arrange - both players used the boost in an open round and a closed one.
        Returns(Data(
            usages:
            [
                Usage(Me, 8, NowUtc.AddDays(3)),
                Usage(Opponent, 8, NowUtc.AddDays(3)),
                Usage(Me, 7, NowUtc.AddDays(-7)),
                Usage(Opponent, 7, NowUtc.AddDays(-7))
            ]));

        // Act
        var summary = await _handler.Handle(Query(Me), CancellationToken.None);

        // Assert - the rule is wired in, not merely present.
        var window = summary.Single().Windows.Single();
        RoundsFor(window, Me).Should().BeEquivalentTo([7, 8]);
        RoundsFor(window, Opponent).Should().BeEquivalentTo([7],
            "round 8 has not closed, so the opponent's boost in it must not reach the page at all.");
    }

    [Fact]
    public async Task Handle_ShouldCountOnlyVisibleBoostsAgainstTheAllowance_WhenOneIsHidden()
    {
        // Arrange - the remaining-uses figure is computed from censored rows, so a leak would show up here
        // even if the usage list itself looked right.
        Returns(Data(usages: [Usage(Me, 8, NowUtc.AddDays(3)), Usage(Opponent, 8, NowUtc.AddDays(3))]));

        // Act
        var window = (await _handler.Handle(Query(Me), CancellationToken.None)).Single().Windows.Single();

        // Assert - two uses per season.
        window.PlayerUsages.Single(p => p.UserId == Me).Remaining.Should().Be(1);
        window.PlayerUsages.Single(p => p.UserId == Opponent).Remaining.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenTheLeagueDoesNotExist()
    {
        // Arrange - the port returns null for an unknown league.
        Returns(null);

        // Act
        var summary = await _handler.Handle(Query(Me), CancellationToken.None);

        // Assert
        summary.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldReturnEmpty_WhenTheLeagueHasNoBoostsEnabled()
    {
        // Arrange
        Returns(Data(rules: []));

        // Act
        var summary = await _handler.Handle(Query(Me), CancellationToken.None);

        // Assert
        summary.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldFormatPlayerNamesFromTheirParts_RatherThanExpectingThemPreformatted()
    {
        // Arrange - the query returns name parts; the display format is a C# rule now.
        Returns(Data(members: [new BoostMemberRow(Me, "Ada", "Lovelace")]));

        // Act
        var window = (await _handler.Handle(Query(Me), CancellationToken.None)).Single().Windows.Single();

        // Assert
        window.PlayerUsages.Single().PlayerName.Should().Be("Ada L");
    }

    private static IEnumerable<int> RoundsFor(Contracts.Boosts.WindowUsageSummaryDto window, string userId) =>
        window.PlayerUsages.Single(p => p.UserId == userId).Usages.Select(u => u.RoundNumber);

    private void Returns(LeagueBoostUsageData? data) =>
        _usageQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(data);

    private static GetLeagueBoostUsageSummaryQuery Query(string currentUserId) => new(LeagueId, currentUserId);

    private static LeagueBoostUsageData Data(
        IReadOnlyList<BoostRuleRow>? rules = null,
        IReadOnlyList<BoostMemberRow>? members = null,
        IReadOnlyList<BoostUsageRow>? usages = null) =>
        new(
            SeasonId: 1,
            BoostRules: rules ?? [new BoostRuleRow(1, "DOUBLE_UP", "Double Up", null, TotalUsesPerSeason: 2)],
            Windows: [],
            Members: members ?? [new BoostMemberRow(Me, "Me", "Myself"), new BoostMemberRow(Opponent, "Op", "Ponent")],
            Usages: usages ?? [],
            RoundRange: new BoostRoundRangeRow(1, 38),
            InProgressRoundNumber: null,
            LastCompletedRoundNumber: null);

    private static BoostUsageRow Usage(string userId, int roundNumber, DateTime deadline) =>
        new(userId, "DOUBLE_UP", roundNumber, deadline, HasBoost: true, BasePoints: 9, BoostedPoints: 18);
}
