using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Leagues.Queries;
using ThePredictions.Application.Services;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Queries;

/// <summary>
/// The administrator's member-management page for a league.
/// </summary>
public class FetchLeagueMembersQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string UserId = "user-admin";

    private static readonly DateTime Joined = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILeagueMembersQuery _membersQuery = Substitute.For<ILeagueMembersQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly FetchLeagueMembersQueryHandler _handler;

    public FetchLeagueMembersQueryHandlerTests()
    {
        _handler = new FetchLeagueMembersQueryHandler(_membersQuery, _membershipService);
    }

    [Fact]
    public async Task Handle_ShouldAllowOnlyTheLeagueAdministrator()
    {
        // Arrange
        _membershipService
            .EnsureLeagueAdministratorAsync(LeagueId, UserId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new UnauthorizedAccessException()));

        // Act
        var act = async () => await HandleAsync();

        // Assert - being in the league is not enough to manage it.
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _membersQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        _membersQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((LeagueMembersData?)null);

        // Act
        var act = async () => await HandleAsync();

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldReturnTheLeaguesName_EvenWithNobodyInIt()
    {
        // Arrange - the old handler reached this by reading the members, finding none, and running a second query.
        Given(new LeagueMembersData("Test League", []));

        // Act
        var page = await HandleAsync();

        // Assert
        page.LeagueName.Should().Be("Test League");
        page.Members.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldListEveryMembership_IncludingRejectedRequests()
    {
        // Arrange
        Given(
            Member("u1", "Ada", "Lovelace", LeagueMemberStatus.Approved),
            Member("u2", "Grace", "Hopper", LeagueMemberStatus.Pending),
            Member("u3", "Alan", "Turing", LeagueMemberStatus.Rejected));

        // Act
        var page = await HandleAsync();

        // Assert - this is where an administrator approves and rejects, so a rejection has to be visible. That differs
        // from the league dashboard, which hides them.
        page.Members.Select(member => member.Status).Should().BeEquivalentTo(
        [
            LeagueMemberStatus.Approved,
            LeagueMemberStatus.Pending,
            LeagueMemberStatus.Rejected
        ]);
    }

    [Fact]
    public async Task Handle_ShouldOrderMembersByFirstNameThenLast()
    {
        // Arrange
        Given(
            Member("u1", "Grace", "Hopper", LeagueMemberStatus.Approved),
            Member("u2", "Ada", "Turing", LeagueMemberStatus.Approved),
            Member("u3", "Ada", "Lovelace", LeagueMemberStatus.Approved));

        // Act
        var page = await HandleAsync();

        // Assert - the old ORDER BY was on the abbreviated name, so two members sharing a first name and an initial
        // came back in whatever order the engine chose. Ordering by both names makes that deterministic.
        page.Members.Select(member => member.FullName).Should().Equal("Ada L", "Ada T", "Grace H");
    }

    [Fact]
    public async Task Handle_ShouldAbbreviateEachMembersName()
    {
        // Arrange
        Given(Member("u1", "Ada", "Lovelace", LeagueMemberStatus.Approved));

        // Act
        var page = await HandleAsync();

        // Assert - the old query called this column FullName, which it never was.
        page.Members.Single().FullName.Should().Be("Ada L");
    }

    [Fact]
    public async Task Handle_ShouldCarryTheUserIdAndJoinDate()
    {
        // Arrange - the id is what the approve and reject buttons act on.
        Given(Member("u1", "Ada", "Lovelace", LeagueMemberStatus.Pending));

        // Act
        var member = (await HandleAsync()).Members.Single();

        // Assert
        member.UserId.Should().Be("u1");
        member.JoinedAtUtc.Should().Be(Joined);
    }

    private void Given(params LeagueMembershipRow[] members) =>
        Given(new LeagueMembersData("Test League", members));

    private void Given(LeagueMembersData data)
    {
        _membersQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(data);
    }

    private async Task<LeagueMembersPageDto> HandleAsync() =>
        await _handler.Handle(new FetchLeagueMembersQuery(LeagueId, UserId), CancellationToken.None);

    private static LeagueMembershipRow Member(
        string userId,
        string firstName,
        string lastName,
        LeagueMemberStatus status) =>
        new(userId, firstName, lastName, Joined, status);
}
