using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Features.Admin.Seasons.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Application.Services;
using ThePredictions.Domain.Common.Exceptions;
using ThePredictions.Domain.Models;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.Admin.Seasons.Commands;

/// <summary>
/// Season housekeeping from the admin screens: deleting one, switching it active, and the scheduled
/// sweep that pulls fresh fixtures for every season still running.
/// </summary>
public class SeasonAdminCommandHandlerTests
{
    private const int SeasonId = 11;

    private static readonly DateTime SeasonStart = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly ISeasonRepository _seasons = Substitute.For<ISeasonRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();

    private readonly DeleteSeasonCommandHandler _delete;
    private readonly UpdateSeasonStatusCommandHandler _updateStatus;
    private readonly SyncAllActiveSeasonsCommandHandler _syncAll;

    public SeasonAdminCommandHandlerTests()
    {
        _delete = new DeleteSeasonCommandHandler(_seasons, _currentUser, Substitute.For<ILogger<DeleteSeasonCommandHandler>>());
        _updateStatus = new UpdateSeasonStatusCommandHandler(_seasons, _currentUser);
        _syncAll = new SyncAllActiveSeasonsCommandHandler(_seasons, _mediator);

        _seasons.GetActiveSeasonsAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    private static Season Season(int id = SeasonId, bool isActive = true) =>
        new(id: id, name: "2026/27", startDateUtc: SeasonStart, endDateUtc: SeasonStart.AddMonths(9),
            isActive: isActive, numberOfRounds: 38, competitionId: 3,
            passStandardPrice: null, passPremiumPrice: null);

    private Season GivenSeason(bool isActive = true)
    {
        var season = Season(isActive: isActive);
        _seasons.GetByIdAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(season);
        return season;
    }

    private Task DeleteAsync() => _delete.Handle(new DeleteSeasonCommand(SeasonId), CancellationToken.None);

    private Task UpdateStatusAsync(bool isActive) =>
        _updateStatus.Handle(new UpdateSeasonStatusCommand(SeasonId, isActive), CancellationToken.None);

    [Fact]
    public async Task Delete_ShouldRequireAnAdministrator()
    {
        GivenSeason();

        await DeleteAsync();

        _currentUser.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task Delete_ShouldThrow_WhenTheSeasonDoesNotExist()
    {
        var act = () => DeleteAsync();

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Fact]
    public async Task Delete_ShouldRefuse_WhenPlayersHaveAlreadyPredicted()
    {
        // Deleting would destroy their predictions, so the whole thing is refused rather than
        // cascading the delete.
        GivenSeason();
        _seasons.HasPredictionsAsync(SeasonId, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => DeleteAsync();

        (await act.Should().ThrowAsync<BusinessRuleViolationException>())
            .WithMessage("*has predictions*");
        await _seasons.DidNotReceiveWithAnyArgs().DeleteAsync(default, CancellationToken.None);
    }

    [Fact]
    public async Task Delete_ShouldRemoveASeasonNobodyHasPredictedIn()
    {
        GivenSeason();

        await DeleteAsync();

        await _seasons.Received(1).DeleteAsync(SeasonId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateStatus_ShouldRequireAnAdministrator()
    {
        GivenSeason();

        await UpdateStatusAsync(isActive: false);

        _currentUser.Received(1).EnsureAdministrator();
    }

    [Fact]
    public async Task UpdateStatus_ShouldThrow_WhenTheSeasonDoesNotExist()
    {
        var act = () => UpdateStatusAsync(isActive: true);

        await act.Should().ThrowAsync<EntityNotFoundException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UpdateStatus_ShouldSwitchTheSeasonEitherWay(bool isActive)
    {
        // Only active seasons get the per-minute score and fixture sweeps, so this switch decides
        // whether a season is being kept up to date.
        var season = GivenSeason(isActive: !isActive);

        await UpdateStatusAsync(isActive);

        season.IsActive.Should().Be(isActive);
        await _seasons.Received(1).UpdateAsync(season, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAll_ShouldDoNothing_WhenNoSeasonIsActive()
    {
        await _syncAll.Handle(new SyncAllActiveSeasonsCommand(), CancellationToken.None);

        await _mediator.DidNotReceive().Send(Arg.Any<SyncSeasonWithApiCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAll_ShouldRefreshEveryActiveSeason()
    {
        _seasons.GetActiveSeasonsAsync(Arg.Any<CancellationToken>()).Returns([Season(id: 11), Season(id: 12)]);

        await _syncAll.Handle(new SyncAllActiveSeasonsCommand(), CancellationToken.None);

        await _mediator.Received(1).Send(Arg.Is<SyncSeasonWithApiCommand>(c => c.SeasonId == 11), Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(Arg.Is<SyncSeasonWithApiCommand>(c => c.SeasonId == 12), Arg.Any<CancellationToken>());
    }
}
