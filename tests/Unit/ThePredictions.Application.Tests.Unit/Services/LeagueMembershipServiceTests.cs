using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Services;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Services;

/// <summary>
/// The guard in front of eighteen league queries.
///
/// It used to live in Infrastructure holding its own two SQL statements, excluded from coverage as "repository
/// composition over SQL" - but the composition was the part worth testing: what happens when the answer is no.
/// </summary>
public class LeagueMembershipServiceTests
{
    private const int LeagueId = 42;
    private const string UserId = "user-me";

    private readonly ILeagueMembershipQuery _membershipQuery = Substitute.For<ILeagueMembershipQuery>();
    private readonly LeagueMembershipService _service;

    public LeagueMembershipServiceTests()
    {
        _service = new LeagueMembershipService(_membershipQuery);
    }

    [Fact]
    public async Task IsApprovedMemberAsync_ShouldReportWhatTheQuerySays()
    {
        // Arrange
        _membershipQuery.IsApprovedMemberAsync(LeagueId, UserId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var isMember = await _service.IsApprovedMemberAsync(LeagueId, UserId, CancellationToken.None);

        // Assert
        isMember.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureApprovedMemberAsync_ShouldPass_ForAnApprovedMember()
    {
        // Arrange
        _membershipQuery.IsApprovedMemberAsync(LeagueId, UserId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var act = async () => await _service.EnsureApprovedMemberAsync(LeagueId, UserId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureApprovedMemberAsync_ShouldRefuse_ForAnyoneElse()
    {
        // Arrange
        _membershipQuery.IsApprovedMemberAsync(LeagueId, UserId, Arg.Any<CancellationToken>()).Returns(false);

        // Act
        var act = async () => await _service.EnsureApprovedMemberAsync(LeagueId, UserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("You must be a member of this league to access this resource.");
    }

    [Fact]
    public async Task IsLeagueAdministratorAsync_ShouldReportWhatTheQuerySays()
    {
        // Arrange
        _membershipQuery.IsAdministratorAsync(LeagueId, UserId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var isAdmin = await _service.IsLeagueAdministratorAsync(LeagueId, UserId, CancellationToken.None);

        // Assert
        isAdmin.Should().BeTrue();
    }

    [Fact]
    public async Task EnsureLeagueAdministratorAsync_ShouldPass_ForTheAdministrator()
    {
        // Arrange
        _membershipQuery.IsAdministratorAsync(LeagueId, UserId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var act = async () => await _service.EnsureLeagueAdministratorAsync(LeagueId, UserId, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EnsureLeagueAdministratorAsync_ShouldRefuse_ForAnOrdinaryMember()
    {
        // Arrange - being in the league is not the same as running it.
        _membershipQuery.IsAdministratorAsync(LeagueId, UserId, Arg.Any<CancellationToken>()).Returns(false);
        _membershipQuery.IsApprovedMemberAsync(LeagueId, UserId, Arg.Any<CancellationToken>()).Returns(true);

        // Act
        var act = async () => await _service.EnsureLeagueAdministratorAsync(LeagueId, UserId, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("Only the league administrator can access this resource.");
    }
}
