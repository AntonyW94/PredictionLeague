using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Services.Boosts;
using ThePredictions.Infrastructure.Services;
using Xunit;

namespace ThePredictions.Infrastructure.Tests.Unit.Services;

/// <summary>
/// Covers the boost window calculation through the public eligibility call. The window decides
/// whether a boost can be played in a given round and, when it cannot, which round the player has
/// to wait for - so getting it wrong either blocks a legitimate boost or leaks one early.
/// </summary>
public class BoostServiceWindowStatusTests
{
    private const string UserId = "user-1";
    private const int LeagueId = 7;
    private const int RoundId = 42;
    private const int SeasonId = 3;
    private const string BoostCode = "DOUBLE_UP";

    private static readonly DateTime NowUtc = new(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly IBoostReadRepository _readRepository = Substitute.For<IBoostReadRepository>();
    private readonly IBoostWriteRepository _writeRepository = Substitute.For<IBoostWriteRepository>();
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private BoostService BuildService() =>
        new(_readRepository, _writeRepository, _leagueRepository, _dateTimeProvider);

    private static BoostWindowSnapshot Window(int start, int end, int maxUses = 1) =>
        new() { StartRoundNumber = start, EndRoundNumber = end, MaxUsesInWindow = maxUses };

    private void GivenRound(int roundNumber, params BoostWindowSnapshot[] windows)
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);

        _readRepository.GetRoundInfoAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns((SeasonId, roundNumber, NowUtc.AddDays(1)));

        _readRepository.GetLeagueSeasonIdAsync(LeagueId, Arg.Any<CancellationToken>())
            .Returns(SeasonId);

        _readRepository.IsUserMemberOfLeagueAsync(UserId, LeagueId, Arg.Any<CancellationToken>())
            .Returns(true);

        _readRepository.GetLeagueBoostRuleAsync(LeagueId, BoostCode, Arg.Any<CancellationToken>())
            .Returns(new LeagueBoostRuleSnapshot
            {
                IsEnabled = true,
                TotalUsesPerSeason = 3,
                Windows = windows
            });

        _readRepository.GetUserBoostUsageSnapshotAsync(UserId, LeagueId, SeasonId, RoundId, BoostCode, Arg.Any<CancellationToken>())
            .Returns(new BoostUsageSnapshot { SeasonUses = 0, WindowUses = 0, HasUsedThisRound = false });
    }

    private async Task<BoostEligibilityDto> GetEligibilityAsync() =>
        await BuildService().GetEligibilityAsync(UserId, LeagueId, RoundId, BoostCode, CancellationToken.None);

    [Fact]
    public async Task Eligibility_ShouldTreatEveryRoundAsActive_WhenNoWindowsAreConfigured()
    {
        GivenRound(roundNumber: 5);

        var result = await GetEligibilityAsync();

        result.IsRoundInActiveWindow.Should().BeTrue();
        result.NextWindowStartRound.Should().BeNull();
    }

    [Fact]
    public async Task Eligibility_ShouldTreatEveryRoundAsActive_WhenTheWindowListIsMissingEntirely()
    {
        GivenRound(roundNumber: 5);
        _readRepository.GetLeagueBoostRuleAsync(LeagueId, BoostCode, Arg.Any<CancellationToken>())
            .Returns(new LeagueBoostRuleSnapshot { IsEnabled = true, TotalUsesPerSeason = 3, Windows = null! });

        var result = await GetEligibilityAsync();

        result.IsRoundInActiveWindow.Should().BeTrue();
        result.NextWindowStartRound.Should().BeNull();
    }

    [Fact]
    public async Task Eligibility_ShouldTreatEveryRoundAsActive_WhenTheWindowListIsEmpty()
    {
        GivenRound(roundNumber: 5, windows: []);

        var result = await GetEligibilityAsync();

        result.IsRoundInActiveWindow.Should().BeTrue();
        result.NextWindowStartRound.Should().BeNull();
    }

    [Theory]
    [InlineData(5)]
    [InlineData(7)]
    [InlineData(10)]
    public async Task Eligibility_ShouldBeInsideTheWindow_AtItsStartMiddleAndEnd(int roundNumber)
    {
        GivenRound(roundNumber, Window(5, 10));

        var result = await GetEligibilityAsync();

        result.IsRoundInActiveWindow.Should().BeTrue();
        result.NextWindowStartRound.Should().BeNull();
    }

    [Fact]
    public async Task Eligibility_ShouldPointAtTheWindowStart_WhenTheRoundIsBeforeIt()
    {
        GivenRound(roundNumber: 2, Window(5, 10));

        var result = await GetEligibilityAsync();

        result.IsRoundInActiveWindow.Should().BeFalse();
        result.NextWindowStartRound.Should().Be(5);
    }

    [Fact]
    public async Task Eligibility_ShouldReportNoNextWindow_WhenEveryWindowHasPassed()
    {
        GivenRound(roundNumber: 20, Window(5, 10));

        var result = await GetEligibilityAsync();

        result.IsRoundInActiveWindow.Should().BeFalse();
        result.NextWindowStartRound.Should().BeNull();
    }

    [Fact]
    public async Task Eligibility_ShouldPickTheEarliestUpcomingWindow_WhenSeveralLieAhead()
    {
        GivenRound(roundNumber: 2, Window(20, 25), Window(8, 12), Window(30, 35));

        var result = await GetEligibilityAsync();

        result.IsRoundInActiveWindow.Should().BeFalse();
        result.NextWindowStartRound.Should().Be(8);
    }

    [Fact]
    public async Task Eligibility_ShouldIgnoreWindowsAlreadyPassed_WhenChoosingTheNextOne()
    {
        GivenRound(roundNumber: 15, Window(1, 5), Window(8, 12), Window(20, 25));

        var result = await GetEligibilityAsync();

        result.IsRoundInActiveWindow.Should().BeFalse();
        result.NextWindowStartRound.Should().Be(20);
    }

    [Fact]
    public async Task Eligibility_ShouldBeActive_WhenTheRoundFallsInTheSecondOfSeveralWindows()
    {
        GivenRound(roundNumber: 9, Window(1, 5), Window(8, 12), Window(20, 25));

        var result = await GetEligibilityAsync();

        result.IsRoundInActiveWindow.Should().BeTrue();
        result.NextWindowStartRound.Should().BeNull();
    }

    [Fact]
    public async Task Eligibility_ShouldHandleASingleRoundWindow()
    {
        GivenRound(roundNumber: 7, Window(7, 7));

        var result = await GetEligibilityAsync();

        result.IsRoundInActiveWindow.Should().BeTrue();
    }

    [Fact]
    public async Task Eligibility_ShouldFallJustOutside_TheRoundAfterAWindowCloses()
    {
        GivenRound(roundNumber: 11, Window(5, 10), Window(14, 18));

        var result = await GetEligibilityAsync();

        result.IsRoundInActiveWindow.Should().BeFalse();
        result.NextWindowStartRound.Should().Be(14);
    }
}
