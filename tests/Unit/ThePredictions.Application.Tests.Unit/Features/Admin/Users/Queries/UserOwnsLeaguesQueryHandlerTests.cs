using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Users.Queries;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Users.Queries;

/// <summary>
/// Whether a player administers any league. This is what stands between a site administrator and deleting an account that other
/// people's leagues depend on, so the answer being wrong either way matters.
/// </summary>
public class UserOwnsLeaguesQueryHandlerTests
{
    private const string UserId = "user-1";

    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly UserOwnsLeaguesQueryHandler _handler;

    public UserOwnsLeaguesQueryHandlerTests()
    {
        _handler = new UserOwnsLeaguesQueryHandler(_leagueRepository);
    }

    [Fact]
    public async Task Handle_ShouldReportTheyOwnLeagues_WhenTheyAdministerOne()
    {
        // Arrange
        GivenLeagues(TheirLeague());

        // Act
        var ownsLeagues = await HandleAsync();

        // Assert
        ownsLeagues.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldReportTheyOwnNoLeagues_WhenTheyAdministerNone()
    {
        // Arrange
        GivenLeagues();

        // Act
        var ownsLeagues = await HandleAsync();

        // Assert
        ownsLeagues.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldAskAboutThePlayerItWasGiven()
    {
        // Arrange
        GivenLeagues();

        // Act
        await HandleAsync();

        // Assert
        await _leagueRepository.Received(1).GetLeaguesByAdministratorIdAsync(UserId, Arg.Any<CancellationToken>());
    }

    private static League TheirLeague() =>
        new(id: 1, name: "The Office", seasonId: 7, administratorUserId: UserId, entryCode: "ABC123",
            createdAtUtc: new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
            entryDeadlineUtc: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            pointsForExactScore: 3, pointsForCorrectResult: 1, price: 10m, isFree: false, hasPrizes: true,
            prizeFundOverride: null, members: [], prizeSettings: []);

    private void GivenLeagues(params League[] leagues) =>
        _leagueRepository.GetLeaguesByAdministratorIdAsync(UserId, Arg.Any<CancellationToken>()).Returns(leagues);

    private Task<bool> HandleAsync() => _handler.Handle(new UserOwnsLeaguesQuery(UserId), CancellationToken.None);
}
