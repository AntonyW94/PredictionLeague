using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ThePredictions.Application.Common.Prizes;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Prizes;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Domain.Models;
using ThePredictions.Tests.Shared.Helpers;
using Xunit;

namespace ThePredictions.Application.Tests.Unit.Common.Prizes;

public class PrizeSchemeFreezeServiceTests
{
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly ISeasonRepository _seasonRepository = Substitute.For<ISeasonRepository>();
    private readonly IPrizeEvaluator _prizeEvaluator = Substitute.For<IPrizeEvaluator>();
    private readonly TestDateTimeProvider _dateTimeProvider = new(new DateTime(2026, 6, 11, 10, 0, 0, DateTimeKind.Utc));
    private readonly PrizeSchemeFreezeService _service;

    public PrizeSchemeFreezeServiceTests()
    {
        _service = new PrizeSchemeFreezeService(
            _leagueRepository,
            _seasonRepository,
            _prizeEvaluator,
            _dateTimeProvider,
            Substitute.For<ILogger<PrizeSchemeFreezeService>>());
    }

    private Season CreateSeason() =>
        new(1, "World Cup 2026", _dateTimeProvider.UtcNow.AddDays(1), _dateTimeProvider.UtcNow.AddMonths(1), true, 7, 1, null, null);

    private LeaguePrizeScheme CreateScheme() =>
        LeaguePrizeScheme.Create(25, new[] { LeaguePrizeSchemeEntry.Create(PrizeType.Overall, 25) }, "admin-user", false, _dateTimeProvider);

    private LeagueMember CreateMember(string userId) =>
        new(leagueId: 1, userId: userId, status: LeagueMemberStatus.Approved, isAlertDismissed: false,
            isArchivedByUser: false, joinedAtUtc: _dateTimeProvider.UtcNow, approvedAtUtc: _dateTimeProvider.UtcNow,
            roundResults: []);

    private League CreateLeague(
        DateTime? entryDeadlineUtc = null,
        LeaguePrizeScheme? scheme = null,
        List<LeaguePrizeSetting>? prizeSettings = null) =>
        new(1, "Test League", 1, "admin-user", "ABC123", _dateTimeProvider.UtcNow.AddMonths(-1),
            entryDeadlineUtc ?? _dateTimeProvider.UtcNow.AddHours(-1), 5, 3, 25m, false, scheme is not null,
            null, members: [CreateMember("user-1"), CreateMember("user-2")], prizeSettings: prizeSettings,
            prizeScheme: scheme);

    private static PrizeBreakdownDto Breakdown() => new()
    {
        Pot = 50,
        EntrantCount = 2,
        Categories = new List<PrizeCategoryBreakdownDto>
        {
            new()
            {
                Category = PrizeType.Overall, Kind = PrizeCategoryKind.EndOfSeason, SubPot = 50,
                Slots = new List<PrizeSlotDto>
                {
                    new() { Label = "1st", Amount = 30, Rank = 1 },
                    new() { Label = "2nd", Amount = 20, Rank = 2 }
                }
            }
        }
    };

    [Fact]
    public async Task TryFreezeAsync_ShouldFreezeAndPersist_WhenSchemeIsDue()
    {
        var league = CreateLeague(scheme: CreateScheme());
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateSeason());
        _prizeEvaluator.Evaluate(Arg.Any<PrizeSchemeEvaluationRequest>()).Returns(Breakdown());

        var result = await _service.TryFreezeAsync(league, CancellationToken.None);

        result.Should().BeTrue();
        league.PrizeSettings.Should().HaveCount(2);
        await _leagueRepository.Received(1).UpdateAsync(league, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryFreezeAsync_ShouldReturnFalse_WhenSettingsAlreadyExist()
    {
        var existingSettings = new List<LeaguePrizeSetting> { LeaguePrizeSetting.Create(1, PrizeType.Overall, 1, 50m) };
        var league = CreateLeague(scheme: CreateScheme(), prizeSettings: existingSettings);

        var result = await _service.TryFreezeAsync(league, CancellationToken.None);

        result.Should().BeFalse();
        await _leagueRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task TryFreezeAsync_ShouldReturnFalse_WhenLeagueHasNoScheme()
    {
        var league = CreateLeague(scheme: null);

        var result = await _service.TryFreezeAsync(league, CancellationToken.None);

        result.Should().BeFalse();
        await _leagueRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task TryFreezeAsync_ShouldReturnFalse_WhenEntryDeadlineHasNotPassed()
    {
        var league = CreateLeague(entryDeadlineUtc: _dateTimeProvider.UtcNow.AddHours(1), scheme: CreateScheme());

        var result = await _service.TryFreezeAsync(league, CancellationToken.None);

        result.Should().BeFalse();
        await _leagueRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task TryFreezeAsync_ShouldReturnFalse_WhenSeasonIsMissing()
    {
        var league = CreateLeague(scheme: CreateScheme());
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns((Season?)null);

        var result = await _service.TryFreezeAsync(league, CancellationToken.None);

        result.Should().BeFalse();
        await _leagueRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task TryFreezeAsync_ShouldReturnFalse_WhenSchemeProducesNoPrizes()
    {
        var league = CreateLeague(scheme: CreateScheme());
        _seasonRepository.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(CreateSeason());
        _prizeEvaluator.Evaluate(Arg.Any<PrizeSchemeEvaluationRequest>()).Returns(new PrizeBreakdownDto());

        var result = await _service.TryFreezeAsync(league, CancellationToken.None);

        result.Should().BeFalse();
        await _leagueRepository.DidNotReceiveWithAnyArgs().UpdateAsync(default!, CancellationToken.None);
    }
}
