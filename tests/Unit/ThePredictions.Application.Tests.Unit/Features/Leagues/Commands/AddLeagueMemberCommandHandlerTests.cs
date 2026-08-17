using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ThePredictions.Application.Features.Leagues.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Leagues.Commands;

/// <summary>
/// A system administrator placing a player in a league, past its entry deadline if need be.
/// </summary>
public class AddLeagueMemberCommandHandlerTests
{
    private const int LeagueId = 5;
    private const int SeasonId = 1;
    private const string UserId = "paid-user";

    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ILeagueStatsRepository _leagueStatsRepository = Substitute.For<ILeagueStatsRepository>();
    private readonly ISeasonAccessService _seasonAccessService = Substitute.For<ISeasonAccessService>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<AddLeagueMemberCommandHandler> _logger = Substitute.For<ILogger<AddLeagueMemberCommandHandler>>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc));
    private readonly AddLeagueMemberCommandHandler _handler;

    public AddLeagueMemberCommandHandlerTests()
    {
        _handler = new AddLeagueMemberCommandHandler(
            _leagueRepository,
            _leagueStatsRepository,
            _seasonAccessService,
            _currentUserService,
            _mediator,
            _dateTimeProvider,
            _logger);
    }

    [Fact]
    public async Task Handle_ShouldRequireAnAdministrator()
    {
        // Arrange - the deadline waiver is the reason this is administrator-only, so the check comes before anything else
        _currentUserService.When(service => service.EnsureAdministrator())
            .Throw(new UnauthorizedAccessException());

        // Act
        var act = () => HandleAsync();

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _leagueRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldAddTheMemberApprovedAndSaveTheLeague()
    {
        // Arrange
        var league = GivenLeague();

        // Act
        await HandleAsync();

        // Assert
        league.Members.Should().ContainSingle(member => member.UserId == UserId
            && member.Status == LeagueMemberStatus.Approved);
        await _leagueRepository.Received(1).UpdateAsync(league, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldAddTheMember_WhenTheEntryDeadlineHasPassed()
    {
        // Arrange - the case the whole command exists for
        var league = GivenLeague(entryDeadlineUtc: _dateTimeProvider.UtcNow.AddDays(-2));

        // Act
        await HandleAsync();

        // Assert
        league.Members.Should().ContainSingle(member => member.UserId == UserId);
    }

    [Fact]
    public async Task Handle_ShouldThrowEntityNotFoundException_WhenTheLeagueDoesNotExist()
    {
        // Arrange
        _leagueRepository.GetByIdAsync(LeagueId, Arg.Any<CancellationToken>()).Returns((League?)null);

        // Act
        var act = () => HandleAsync();

        // Assert
        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenThePlayerHoldsNoSeasonPass()
    {
        // Arrange
        var league = GivenLeague();

        _seasonAccessService.EnsureCanParticipateAsync(UserId, SeasonId, Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new SeasonPassRequiredException(SeasonId)));

        // Act
        var act = () => HandleAsync();

        // Assert - an administrator overrides when somebody joined, not whether they may take part in the season at all
        await act.Should().ThrowAsync<SeasonPassRequiredException>();
        league.Members.Should().BeEmpty();
        await _leagueRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenThePlayerIsAlreadyAMember()
    {
        // Arrange
        var league = GivenLeague();
        league.AddMember(UserId, _dateTimeProvider);

        // Act
        var act = () => HandleAsync();

        // Assert
        await act.Should().ThrowAsync<BusinessRuleViolationException>();
        await _leagueRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldRefreshLeagueStats()
    {
        // Arrange
        GivenLeague();

        // Act
        await HandleAsync();

        // Assert - they go in approved, so they are ranked immediately and every other member's cached rank moves
        await _leagueStatsRepository.Received(1).RefreshLeagueAsync(LeagueId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNotifyTheMemberTheyCanTakePart()
    {
        // Arrange
        GivenLeague();

        // Act
        await HandleAsync();

        // Assert - from the player's side this is the same event as being approved, so it sends the same email
        await _mediator.Received(1).Send(
            Arg.Is<NotifyMemberOfLeagueApprovalCommand>(notification =>
                notification.MemberUserId == UserId &&
                notification.LeagueId == LeagueId &&
                notification.LeagueName == "Test League" &&
                notification.SeasonId == SeasonId),
            Arg.Any<CancellationToken>());
    }

    private League GivenLeague(DateTime? entryDeadlineUtc = null)
    {
        var league = new League(
            id: LeagueId, name: "Test League", seasonId: SeasonId,
            administratorUserId: "league-owner",
            entryCode: "ABC123",
            createdAtUtc: _dateTimeProvider.UtcNow.AddDays(-30),
            entryDeadlineUtc: entryDeadlineUtc ?? _dateTimeProvider.UtcNow.AddMonths(1),
            pointsForExactScore: 3, pointsForCorrectResult: 1,
            price: 0, isFree: true, hasPrizes: false,
            prizeFundOverride: null,
            members: null, prizeSettings: null);

        _leagueRepository.GetByIdAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(league);

        return league;
    }

    private async Task HandleAsync() =>
        await _handler.Handle(new AddLeagueMemberCommand(LeagueId, UserId), CancellationToken.None);
}
