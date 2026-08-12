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
/// A league's dashboard.
///
/// The one query that answers "no such league" rather than "not allowed", which is a rule about what a stranger can
/// learn from a status code rather than about what they can read.
/// </summary>
public class GetLeagueDashboardQueryHandlerTests
{
    private const int LeagueId = 42;
    private const string UserId = "user-me";

    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ILeagueDashboardQuery _dashboardQuery = Substitute.For<ILeagueDashboardQuery>();
    private readonly ILeagueRoundsQuery _roundsQuery = Substitute.For<ILeagueRoundsQuery>();
    private readonly ILeagueMembershipService _membershipService = Substitute.For<ILeagueMembershipService>();
    private readonly GetLeagueDashboardQueryHandler _handler;

    public GetLeagueDashboardQueryHandlerTests()
    {
        _handler = new GetLeagueDashboardQueryHandler(_dashboardQuery, _roundsQuery, _membershipService);
    }

    #region Who may see the league

    [Fact]
    public async Task Handle_ShouldAnswerNotFound_ForSomeoneWhoIsNotAMember()
    {
        // Arrange
        _membershipService.IsApprovedMemberAsync(LeagueId, UserId, Arg.Any<CancellationToken>()).Returns(false);
        Given();

        // Act
        var act = async () => await HandleAsync();

        // Assert - deliberately indistinguishable from a league that does not exist, so a stranger cannot map out
        // which leagues do.
        await act.Should().ThrowAsync<EntityNotFoundException>();
        await _dashboardQuery.DidNotReceiveWithAnyArgs().ExecuteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldReadTheLeague_ForAnApprovedMember()
    {
        // Arrange
        _membershipService.IsApprovedMemberAsync(LeagueId, UserId, Arg.Any<CancellationToken>()).Returns(true);
        Given();

        // Act
        var dashboard = await HandleAsync();

        // Assert
        dashboard.LeagueName.Should().Be("Test League");
    }

    [Fact]
    public async Task Handle_ShouldNotCheckMembership_ForASiteAdministrator()
    {
        // Arrange
        Given();

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert - an administrator can open any league, and is never asked whether they joined it.
        dashboard.LeagueName.Should().Be("Test League");
        await _membershipService.DidNotReceiveWithAnyArgs().IsApprovedMemberAsync(default, default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldThrow_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        _dashboardQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((LeagueDashboardData?)null);

        // Act
        var act = async () => await HandleAsync(isAdmin: true);

        // Assert - the same answer a non-member gets, which is the point.
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    #endregion

    #region The header

    [Fact]
    public async Task Handle_ShouldWorkOutThePrizePot()
    {
        // Arrange
        Given(header: Header(price: 10m, memberCount: 12, prizeFundOverride: 50m));

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert
        dashboard.TotalPrizeFund.Should().Be(170m);
        dashboard.MemberCount.Should().Be(12);
    }

    [Fact]
    public async Task Handle_ShouldReportTheLeagueAsFinished_WhenEveryRoundHasCompleted()
    {
        // Arrange
        Given(header: Header(seasonRoundCount: 3, completedRoundCount: 3));

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert
        dashboard.IsFinished.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReportTheLeagueAsUnfinished_WhileRoundsRemain()
    {
        // Arrange
        Given(header: Header(seasonRoundCount: 3, completedRoundCount: 2));

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert
        dashboard.IsFinished.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldCarryTheSeasonAndCompetitionDetails()
    {
        // Arrange
        var deadline = SeasonStart.AddDays(-1);
        Given(header: Header(competitionType: CompetitionType.Tournament, entryDeadlineUtc: deadline, isFree: true));

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert
        dashboard.CompetitionType.Should().Be(CompetitionType.Tournament);
        dashboard.SeasonStartDateUtc.Should().Be(SeasonStart);
        dashboard.EntryDeadlineUtc.Should().Be(deadline);
        dashboard.IsFree.Should().BeTrue();
    }

    #endregion

    #region The members

    [Fact]
    public async Task Handle_ShouldListApprovedMembersAndPendingRequests()
    {
        // Arrange
        Given(members:
        [
            Member("Ada", "Lovelace", LeagueMemberStatus.Approved),
            Member("Grace", "Hopper", LeagueMemberStatus.Pending)
        ]);

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert - the administrator approves people from here, so requests have to show.
        dashboard.Members.Select(member => member.FullName).Should().Equal("Ada L", "Grace H");
        dashboard.Members.Select(member => member.Status)
            .Should().Equal(nameof(LeagueMemberStatus.Approved), nameof(LeagueMemberStatus.Pending));
    }

    [Fact]
    public async Task Handle_ShouldNotListSomeoneWhoWasTurnedAway()
    {
        // Arrange
        Given(members:
        [
            Member("Ada", "Lovelace", LeagueMemberStatus.Approved),
            Member("Alan", "Turing", LeagueMemberStatus.Rejected)
        ]);

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert - listing them would invite the same decision a second time.
        dashboard.Members.Select(member => member.FullName).Should().Equal("Ada L");
    }

    [Fact]
    public async Task Handle_ShouldOrderMembersByFirstNameThenLast()
    {
        // Arrange
        Given(members:
        [
            Member("Grace", "Hopper", LeagueMemberStatus.Approved),
            Member("Ada", "Turing", LeagueMemberStatus.Approved),
            Member("Ada", "Lovelace", LeagueMemberStatus.Approved)
        ]);

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert
        dashboard.Members.Select(member => member.FullName).Should().Equal("Ada L", "Ada T", "Grace H");
    }

    [Fact]
    public async Task Handle_ShouldAbbreviateTheMembersName()
    {
        // Arrange - the old query called this column FullName, which it never was.
        Given(members: [Member("Ada", "Lovelace", LeagueMemberStatus.Approved)]);

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert
        dashboard.Members.Single().FullName.Should().Be("Ada L");
    }

    [Fact]
    public async Task Handle_ShouldCarryWhenEachMemberJoined()
    {
        // Arrange
        var joinedAt = SeasonStart.AddDays(-3);
        Given(members: [Member("Ada", "Lovelace", LeagueMemberStatus.Approved, joinedAtUtc: joinedAt)]);

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert
        dashboard.Members.Single().JoinedAtUtc.Should().Be(joinedAt);
    }

    #endregion

    #region The rounds

    [Fact]
    public async Task Handle_ShouldListRoundsNewestFirst()
    {
        // Arrange
        Given(rounds: [Round(1), Round(3), Round(2)]);

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert
        dashboard.ViewableRounds.Select(round => round.RoundNumber).Should().Equal(3, 2, 1);
    }

    [Fact]
    public async Task Handle_ShouldNotListARoundThatHasNotBeenPublished()
    {
        // A draft is a round its administrator is still preparing, and no other screen shows one to players. This read
        // used to return them, so an unpublished round appeared on every member's dashboard.
        Given(rounds: [Round(1, status: RoundStatus.Draft), Round(2, status: RoundStatus.Completed)]);

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert
        dashboard.ViewableRounds.Select(round => round.RoundNumber).Should().Equal(2);
    }

    [Fact]
    public async Task Handle_ShouldListTheRoundBeingPlayed()
    {
        // Arrange - in play and finished are both selectable; it is only the unpublished ones that are held back.
        Given(rounds: [Round(1, status: RoundStatus.InProgress), Round(2, status: RoundStatus.Published)]);

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert
        dashboard.ViewableRounds.Select(round => round.RoundNumber).Should().Equal(2, 1);
    }

    [Fact]
    public async Task Handle_ShouldCarryEachRoundsDetails()
    {
        // Arrange
        Given(rounds: [Round(4, matchCount: 10, status: RoundStatus.InProgress)]);

        // Act
        var round = (await HandleAsync(isAdmin: true)).ViewableRounds.Single();

        // Assert
        round.MatchCount.Should().Be(10);
        round.Status.Should().Be(RoundStatus.InProgress);
        round.RoundNumber.Should().Be(4);
    }

    [Fact]
    public async Task Handle_ShouldReturnNoRounds_ForASeasonWithNoneYet()
    {
        // Arrange
        Given();

        // Act
        var dashboard = await HandleAsync(isAdmin: true);

        // Assert
        dashboard.ViewableRounds.Should().BeEmpty();
        dashboard.Members.Should().BeEmpty();
    }

    #endregion

    private void Given(
        LeagueDashboardHeaderRow? header = null,
        IReadOnlyList<LeagueRoundRow>? rounds = null,
        IReadOnlyList<LeagueDashboardMemberRow>? members = null)
    {
        _dashboardQuery
            .ExecuteAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(new LeagueDashboardData(header ?? Header(), members ?? []));

        _roundsQuery.ExecuteAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(rounds ?? []);
    }

    private async Task<LeagueDashboardDto> HandleAsync(bool isAdmin = false) =>
        await _handler.Handle(new GetLeagueDashboardQuery(LeagueId, UserId, isAdmin), CancellationToken.None);

    private static LeagueDashboardHeaderRow Header(
        decimal price = 10m,
        int memberCount = 5,
        decimal? prizeFundOverride = null,
        bool isFree = false,
        CompetitionType competitionType = CompetitionType.League,
        DateTime? entryDeadlineUtc = null,
        int seasonRoundCount = 38,
        int completedRoundCount = 0) =>
        new(
            "Test League",
            competitionType,
            SeasonStart,
            entryDeadlineUtc,
            price,
            prizeFundOverride,
            isFree,
            memberCount,
            seasonRoundCount,
            completedRoundCount);

    private static LeagueDashboardMemberRow Member(
        string firstName,
        string lastName,
        LeagueMemberStatus status,
        DateTime? joinedAtUtc = null) =>
        new(firstName, lastName, status, joinedAtUtc ?? SeasonStart);

    private static LeagueRoundRow Round(
        int roundNumber,
        int matchCount = 0,
        RoundStatus status = RoundStatus.Published) =>
        new(
            roundNumber,
            1,
            roundNumber,
            null,
            SeasonStart.AddDays(roundNumber),
            SeasonStart.AddDays(roundNumber),
            status,
            matchCount);
}
