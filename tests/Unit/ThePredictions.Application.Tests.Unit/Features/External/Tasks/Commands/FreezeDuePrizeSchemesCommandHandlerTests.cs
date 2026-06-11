using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Features.External.Tasks.Commands;
using ThePredictions.Application.Repositories;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Features.External.Tasks.Commands;

public class FreezeDuePrizeSchemesCommandHandlerTests
{
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly IPrizeSchemeFreezeService _freezeService = Substitute.For<IPrizeSchemeFreezeService>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc));
    private readonly FreezeDuePrizeSchemesCommandHandler _handler;

    public FreezeDuePrizeSchemesCommandHandlerTests()
    {
        _handler = new FreezeDuePrizeSchemesCommandHandler(
            _leagueRepository,
            _freezeService,
            _dateTimeProvider,
            Substitute.For<ILogger<FreezeDuePrizeSchemesCommandHandler>>());
    }

    private League CreateLeague(int id) =>
        new(id, $"League {id}", 1, "admin-user", "ABC123", _dateTimeProvider.UtcNow.AddMonths(-1),
            _dateTimeProvider.UtcNow.AddHours(-1), 5, 3, 25m, false, true, null,
            members: null, prizeSettings: null);

    [Fact]
    public async Task Handle_ShouldReturnZeroes_WhenNoLeaguesAreDue()
    {
        _leagueRepository.GetLeagueIdsDueForPrizeFreezeAsync(_dateTimeProvider.UtcNow, Arg.Any<CancellationToken>())
            .Returns([]);

        var result = await _handler.Handle(new FreezeDuePrizeSchemesCommand(), CancellationToken.None);

        result.Should().Be(new FreezeDuePrizeSchemesResult(LeaguesDue: 0, LeaguesFrozen: 0));
        await _freezeService.DidNotReceiveWithAnyArgs().TryFreezeAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldFreezeEachDueLeague_WhenLeaguesAreDue()
    {
        var league1 = CreateLeague(1);
        var league2 = CreateLeague(2);

        _leagueRepository.GetLeagueIdsDueForPrizeFreezeAsync(_dateTimeProvider.UtcNow, Arg.Any<CancellationToken>())
            .Returns([1, 2]);
        _leagueRepository.GetByIdWithAllDataAsync(1, Arg.Any<CancellationToken>()).Returns(league1);
        _leagueRepository.GetByIdWithAllDataAsync(2, Arg.Any<CancellationToken>()).Returns(league2);
        _freezeService.TryFreezeAsync(Arg.Any<League>(), Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(new FreezeDuePrizeSchemesCommand(), CancellationToken.None);

        result.Should().Be(new FreezeDuePrizeSchemesResult(LeaguesDue: 2, LeaguesFrozen: 2));
        await _freezeService.Received(1).TryFreezeAsync(league1, Arg.Any<CancellationToken>());
        await _freezeService.Received(1).TryFreezeAsync(league2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldSkipLeague_WhenLeagueCannotBeLoaded()
    {
        _leagueRepository.GetLeagueIdsDueForPrizeFreezeAsync(_dateTimeProvider.UtcNow, Arg.Any<CancellationToken>())
            .Returns([1]);
        _leagueRepository.GetByIdWithAllDataAsync(1, Arg.Any<CancellationToken>()).Returns((League?)null);

        var result = await _handler.Handle(new FreezeDuePrizeSchemesCommand(), CancellationToken.None);

        result.Should().Be(new FreezeDuePrizeSchemesResult(LeaguesDue: 1, LeaguesFrozen: 0));
        await _freezeService.DidNotReceiveWithAnyArgs().TryFreezeAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task Handle_ShouldCountOnlySuccessfulFreezes_WhenSomeLeaguesAreNotEligible()
    {
        var league1 = CreateLeague(1);
        var league2 = CreateLeague(2);

        _leagueRepository.GetLeagueIdsDueForPrizeFreezeAsync(_dateTimeProvider.UtcNow, Arg.Any<CancellationToken>())
            .Returns([1, 2]);
        _leagueRepository.GetByIdWithAllDataAsync(1, Arg.Any<CancellationToken>()).Returns(league1);
        _leagueRepository.GetByIdWithAllDataAsync(2, Arg.Any<CancellationToken>()).Returns(league2);
        _freezeService.TryFreezeAsync(league1, Arg.Any<CancellationToken>()).Returns(true);
        _freezeService.TryFreezeAsync(league2, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(new FreezeDuePrizeSchemesCommand(), CancellationToken.None);

        result.Should().Be(new FreezeDuePrizeSchemesResult(LeaguesDue: 2, LeaguesFrozen: 1));
    }
}
