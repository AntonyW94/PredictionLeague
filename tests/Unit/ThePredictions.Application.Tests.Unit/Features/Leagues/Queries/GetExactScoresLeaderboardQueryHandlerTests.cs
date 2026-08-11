using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// A league's exact-scores leaderboard. Third of the nine handlers to adopt <c>Domain.Services.Ranking</c>, and
/// the first with no rank-change arrow - so four rules rather than five, and no pre-round condition to get wrong.
/// </summary>
public class GetExactScoresLeaderboardQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string ViewerId = "user-me";

    private readonly IExactScoresLeaderboardQuery _leaderboardQuery = Substitute.For<IExactScoresLeaderboardQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetExactScoresLeaderboardQueryHandler _handler;

    public GetExactScoresLeaderboardQueryHandlerTests()
    {
        _handler = new GetExactScoresLeaderboardQueryHandler(_leaderboardQuery, _membershipService);
    }

    [Fact]
    public async Task Handle_ShouldRequireApprovedMembership_BeforeReadingAnything()
    {
        Given();

        await HandleAsync();

        await _membershipService.Received(1)
            .EnsureApprovedMemberAsync(LeagueId, ViewerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldTotalEachMembersExactScoresAcrossRounds()
    {
        Given(
            members: [Member("u1")],
            exactScores: [Exact("u1", 2), Exact("u1", 3), Exact("u1", 1)]);

        Entries(await HandleAsync()).Single().ExactScoresCount.Should().Be(6);
    }

    [Fact]
    public async Task Handle_ShouldCountAMemberWithNoExactScoresAsZero_RatherThanLeavingThemOut()
    {
        Given(members: [Member("u1"), Member("u2", first: "Grace", last: "Hopper")], exactScores: [Exact("u1", 4)]);

        var entries = Entries(await HandleAsync());

        entries.Should().HaveCount(2);
        entries.Single(e => e.UserId == "u2").ExactScoresCount.Should().Be(0);
        entries.Single(e => e.UserId == "u2").Rank.Should().Be(2);
    }

    [Fact]
    public async Task Handle_ShouldOrderByExactScoresDescending()
    {
        Given(
            members: [Member("low"), Member("high", first: "Grace", last: "Hopper")],
            exactScores: [Exact("low", 1), Exact("high", 9)]);

        Entries(await HandleAsync()).Select(e => e.UserId).Should().Equal("high", "low");
    }

    [Fact]
    public async Task Handle_ShouldShareAPositionAndLeaveAGap_WhenMembersAreTied()
    {
        Given(
            members: [Member("a", first: "Ann"), Member("b", first: "Bob"), Member("c", first: "Cat"), Member("d", first: "Dan")],
            exactScores: [Exact("a", 10), Exact("b", 9), Exact("c", 9), Exact("d", 8)]);

        Entries(await HandleAsync()).Select(e => e.Rank).Should().Equal(1, 2, 2, 4);
    }

    [Fact]
    public async Task Handle_ShouldOrderTiedMembersAlphabeticallyByFullName()
    {
        Given(
            members: [Member("z", first: "Zoe", last: "Zeta"), Member("a", first: "Ada", last: "Lovelace")],
            exactScores: [Exact("z", 5), Exact("a", 5)]);

        Entries(await HandleAsync()).Select(e => e.UserId).Should().Equal("a", "z");
    }

    [Fact]
    public async Task Handle_ShouldShowTheAbbreviatedNameOnScreen()
    {
        Given(members: [Member("u1", first: "Ada", last: "Lovelace")]);

        Entries(await HandleAsync()).Single().PlayerName.Should().Be("Ada L");
    }

    [Fact]
    public async Task Handle_ShouldReturnNoEntries_WhenTheLeagueHasNoApprovedMembers()
    {
        Given(members: []);

        Entries(await HandleAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreCountsForSomeoneNoLongerAMember()
    {
        Given(members: [Member("u1")], exactScores: [Exact("u1", 3), Exact("departed", 99)]);

        Entries(await HandleAsync()).Select(e => e.UserId).Should().Equal("u1");
    }

    private void Given(
        IReadOnlyList<LeaderboardParticipantRow>? members = null,
        IReadOnlyList<MemberExactScoresRow>? exactScores = null) =>
        _leaderboardQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(new ExactScoresLeaderboardData(members ?? [Member("u1")], exactScores ?? []));

    private static LeaderboardParticipantRow Member(string userId, string first = "Ada", string last = "Lovelace") =>
        new(userId, first, last);

    private static MemberExactScoresRow Exact(string userId, int count) => new(userId, count);

    private static IReadOnlyList<Contracts.Leaderboards.ExactScoresLeaderboardEntryDto> Entries(
        Contracts.Leaderboards.ExactScoresLeaderboardDto dto) => dto.Entries;

    private Task<Contracts.Leaderboards.ExactScoresLeaderboardDto> HandleAsync() =>
        _handler.Handle(new GetExactScoresLeaderboardQuery(LeagueId, ViewerId), CancellationToken.None);
}
