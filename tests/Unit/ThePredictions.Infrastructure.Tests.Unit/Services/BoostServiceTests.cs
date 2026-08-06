using FluentAssertions;
using NSubstitute;
using ThePredictions.Application.Repositories;
using ThePredictions.Contracts.Boosts;
using ThePredictions.Domain.Common;
using ThePredictions.Domain.Models;
using ThePredictions.Domain.Services.Boosts;
using ThePredictions.Infrastructure.Services;
using Xunit;

namespace ThePredictions.Infrastructure.Tests.Unit.Services;

/// <summary>
/// Boosts multiply a player's points for a round, so applying one that should not be allowed - or
/// refusing one that should - changes the standings and ultimately who gets paid.
/// </summary>
public class BoostServiceTests
{
    private const string UserId = "user-1";
    private const int LeagueId = 7;
    private const int RoundId = 42;
    private const int RoundNumber = 5;
    private const int SeasonId = 3;
    private const string BoostCode = "DOUBLE_UP";

    private static readonly DateTime NowUtc = new(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

    private readonly IBoostReadRepository _readRepository = Substitute.For<IBoostReadRepository>();
    private readonly IBoostWriteRepository _writeRepository = Substitute.For<IBoostWriteRepository>();
    private readonly ILeagueRepository _leagueRepository = Substitute.For<ILeagueRepository>();
    private readonly IDateTimeProvider _dateTimeProvider = Substitute.For<IDateTimeProvider>();

    private BoostService BuildService() => new(_readRepository, _writeRepository, _leagueRepository, _dateTimeProvider);

    private void GivenAnEligibleBoost(
        DateTime? deadlineUtc = null,
        int? leagueSeasonId = SeasonId,
        bool isMember = true,
        LeagueBoostRuleSnapshot? rule = null,
        BoostUsageSnapshot? usage = null)
    {
        _dateTimeProvider.UtcNow.Returns(NowUtc);

        _readRepository.GetRoundInfoAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns((SeasonId, RoundNumber, deadlineUtc ?? NowUtc.AddDays(1)));

        _readRepository.GetLeagueSeasonIdAsync(LeagueId, Arg.Any<CancellationToken>()).Returns(leagueSeasonId);
        _readRepository.IsUserMemberOfLeagueAsync(UserId, LeagueId, Arg.Any<CancellationToken>()).Returns(isMember);

        _readRepository.GetLeagueBoostRuleAsync(LeagueId, BoostCode, Arg.Any<CancellationToken>())
            .Returns(rule ?? new LeagueBoostRuleSnapshot { IsEnabled = true, TotalUsesPerSeason = 3, Windows = [] });

        _readRepository.GetUserBoostUsageSnapshotAsync(UserId, LeagueId, SeasonId, RoundId, BoostCode, Arg.Any<CancellationToken>())
            .Returns(usage ?? new BoostUsageSnapshot { SeasonUses = 0, WindowUses = 0, HasUsedThisRound = false });
    }

    private Task<BoostEligibilityDto> GetEligibilityAsync() =>
        BuildService().GetEligibilityAsync(UserId, LeagueId, RoundId, BoostCode, CancellationToken.None);

    private Task<ApplyBoostResultDto> ApplyBoostAsync() =>
        BuildService().ApplyBoostAsync(UserId, LeagueId, RoundId, BoostCode, CancellationToken.None);

    // ---------- eligibility ----------

    [Fact]
    public async Task GetEligibilityAsync_ShouldAllowAMemberWithUsesRemaining()
    {
        GivenAnEligibleBoost();

        var result = await GetEligibilityAsync();

        result.CanUse.Should().BeTrue();
        result.BoostCode.Should().Be(BoostCode);
        result.LeagueId.Should().Be(LeagueId);
        result.RoundId.Should().Be(RoundId);
        result.AlreadyUsedThisRound.Should().BeFalse();
    }

    [Fact]
    public async Task GetEligibilityAsync_ShouldRefuse_OnceTheRoundDeadlineHasPassed()
    {
        GivenAnEligibleBoost(deadlineUtc: NowUtc.AddMinutes(-1));

        var result = await GetEligibilityAsync();

        result.CanUse.Should().BeFalse();
        result.Reason.Should().Be("Cannot apply boost after round deadline has passed.");
        result.RemainingSeasonUses.Should().Be(0);
    }

    [Fact]
    public async Task GetEligibilityAsync_ShouldAllow_RightUpToTheDeadline()
    {
        GivenAnEligibleBoost(deadlineUtc: NowUtc);

        (await GetEligibilityAsync()).CanUse.Should().BeTrue();
    }

    [Fact]
    public async Task GetEligibilityAsync_ShouldRefuse_WhenTheLeagueDoesNotOfferTheBoost()
    {
        GivenAnEligibleBoost();
        _readRepository.GetLeagueBoostRuleAsync(LeagueId, BoostCode, Arg.Any<CancellationToken>())
            .Returns((LeagueBoostRuleSnapshot?)null);

        var result = await GetEligibilityAsync();

        result.CanUse.Should().BeFalse();
        result.Reason.Should().Be("Boost is not available in this league.");
    }

    [Fact]
    public async Task GetEligibilityAsync_ShouldRefuse_WhenTheUserIsNotAMemberOfTheLeague()
    {
        GivenAnEligibleBoost(isMember: false);

        (await GetEligibilityAsync()).CanUse.Should().BeFalse();
    }

    [Fact]
    public async Task GetEligibilityAsync_ShouldRefuse_WhenTheRoundBelongsToAnotherSeason()
    {
        GivenAnEligibleBoost(leagueSeasonId: SeasonId + 1);

        (await GetEligibilityAsync()).CanUse.Should().BeFalse();
    }

    [Fact]
    public async Task GetEligibilityAsync_ShouldRefuse_WhenTheLeagueHasNoSeason()
    {
        GivenAnEligibleBoost(leagueSeasonId: null);

        (await GetEligibilityAsync()).CanUse.Should().BeFalse();
    }

    [Fact]
    public async Task GetEligibilityAsync_ShouldRefuse_WhenTheBoostIsAlreadyUsedThisRound()
    {
        GivenAnEligibleBoost(usage: new BoostUsageSnapshot { SeasonUses = 1, WindowUses = 0, HasUsedThisRound = true });

        var result = await GetEligibilityAsync();

        result.CanUse.Should().BeFalse();
        result.AlreadyUsedThisRound.Should().BeTrue();
    }

    [Fact]
    public async Task GetEligibilityAsync_ShouldRefuse_WhenTheSeasonAllowanceIsSpent()
    {
        GivenAnEligibleBoost(usage: new BoostUsageSnapshot { SeasonUses = 3, WindowUses = 0, HasUsedThisRound = false });

        var result = await GetEligibilityAsync();

        result.CanUse.Should().BeFalse();
        result.RemainingSeasonUses.Should().Be(0);
    }

    [Fact]
    public async Task GetEligibilityAsync_ShouldRefuse_WhenTheLeagueHasDisabledTheBoost()
    {
        GivenAnEligibleBoost(rule: new LeagueBoostRuleSnapshot { IsEnabled = false, TotalUsesPerSeason = 3, Windows = [] });

        (await GetEligibilityAsync()).CanUse.Should().BeFalse();
    }

    // ---------- applying ----------

    [Fact]
    public async Task ApplyBoostAsync_ShouldRecordTheUsage_WhenEligible()
    {
        GivenAnEligibleBoost();
        _writeRepository.InsertUserBoostUsageAsync(UserId, LeagueId, SeasonId, RoundId, BoostCode, Arg.Any<CancellationToken>())
            .Returns((true, (string?)null));

        var result = await ApplyBoostAsync();

        result.Success.Should().BeTrue();
        result.Error.Should().BeNull();
        result.AlreadyUsedThisRound.Should().BeFalse();
    }

    [Fact]
    public async Task ApplyBoostAsync_ShouldNotWrite_WhenTheBoostIsNotEligible()
    {
        GivenAnEligibleBoost(deadlineUtc: NowUtc.AddMinutes(-1));

        var result = await ApplyBoostAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().Be("Cannot apply boost after round deadline has passed.");
        await _writeRepository.DidNotReceiveWithAnyArgs()
            .InsertUserBoostUsageAsync(default!, default, default, default, default!, CancellationToken.None);
    }

    [Theory]
    [InlineData("UnknownBoost", "Unknown boost type.")]
    [InlineData("NotConfigured", "This boost is not configured for the selected league.")]
    [InlineData("AlreadyUsedThisRound", "You have already used a boost for this league and round.")]
    [InlineData("SeasonLimitReached", "You have reached the season limit for this boost in this league.")]
    [InlineData("WindowLimitReached", "This boost is not available any more for this round (window limit reached).")]
    [InlineData("NotAvailable", "This boost is not available for this round.")]
    public async Task ApplyBoostAsync_ShouldTranslateTheDatabaseRefusalIntoSomethingReadable(string error, string expected)
    {
        // The database enforces these rules too, so a race between two tabs still comes back with
        // something a player can understand rather than an internal code.
        GivenAnEligibleBoost();
        _writeRepository.InsertUserBoostUsageAsync(UserId, LeagueId, SeasonId, RoundId, BoostCode, Arg.Any<CancellationToken>())
            .Returns((false, error));

        var result = await ApplyBoostAsync();

        result.Success.Should().BeFalse();
        result.Error.Should().Be(expected);
    }

    [Fact]
    public async Task ApplyBoostAsync_ShouldFlagAlreadyUsed_OnlyForThatRefusal()
    {
        GivenAnEligibleBoost();
        _writeRepository.InsertUserBoostUsageAsync(UserId, LeagueId, SeasonId, RoundId, BoostCode, Arg.Any<CancellationToken>())
            .Returns((false, "AlreadyUsedThisRound"));

        (await ApplyBoostAsync()).AlreadyUsedThisRound.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyBoostAsync_ShouldPassAnUnrecognisedRefusalStraightThrough()
    {
        GivenAnEligibleBoost();
        _writeRepository.InsertUserBoostUsageAsync(UserId, LeagueId, SeasonId, RoundId, BoostCode, Arg.Any<CancellationToken>())
            .Returns((false, "SomethingNewFromTheDatabase"));

        var result = await ApplyBoostAsync();

        result.Error.Should().Be("SomethingNewFromTheDatabase");
        result.AlreadyUsedThisRound.Should().BeFalse();
    }

    // ---------- deleting ----------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DeleteUserBoostUsageAsync_ShouldReportWhetherAnythingWasRemoved(bool deleted)
    {
        _writeRepository.DeleteUserBoostUsageAsync(UserId, LeagueId, RoundId, Arg.Any<CancellationToken>()).Returns(deleted);

        var result = await BuildService().DeleteUserBoostUsageAsync(UserId, LeagueId, RoundId, CancellationToken.None);

        result.Should().Be(deleted);
    }

    // ---------- scoring the round ----------

    private static LeagueRoundResult Result(int leagueId, string userId, int basePoints) =>
        new(leagueId, RoundId, userId, basePoints, basePoints, false, null, 0);

    [Fact]
    public async Task ApplyRoundBoostsAsync_ShouldDoNothing_WhenTheRoundHasNoResults()
    {
        _leagueRepository.GetLeagueRoundResultsAsync(RoundId, Arg.Any<CancellationToken>()).Returns([]);

        await BuildService().ApplyRoundBoostsAsync(RoundId, CancellationToken.None);

        await _readRepository.DidNotReceiveWithAnyArgs().GetBoostsForRoundAsync(default, CancellationToken.None);
        await _leagueRepository.DidNotReceiveWithAnyArgs().UpdateLeagueRoundBoostsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task ApplyRoundBoostsAsync_ShouldNotWrite_WhenNobodyPlayedABoost()
    {
        _leagueRepository.GetLeagueRoundResultsAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([Result(LeagueId, UserId, 10)]);
        _readRepository.GetBoostsForRoundAsync(RoundId, Arg.Any<CancellationToken>()).Returns([]);

        await BuildService().ApplyRoundBoostsAsync(RoundId, CancellationToken.None);

        await _leagueRepository.DidNotReceiveWithAnyArgs().UpdateLeagueRoundBoostsAsync(default!, CancellationToken.None);
    }

    [Fact]
    public async Task ApplyRoundBoostsAsync_ShouldBoostOnlyThePlayersWhoUsedOne()
    {
        var boosted = Result(LeagueId, UserId, 10);
        var untouched = Result(LeagueId, "user-2", 8);

        _leagueRepository.GetLeagueRoundResultsAsync(RoundId, Arg.Any<CancellationToken>()).Returns([boosted, untouched]);
        _readRepository.GetBoostsForRoundAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new UserRoundBoostDto(LeagueId, UserId, BoostCode)]);

        List<LeagueRoundBoostUpdate>? captured = null;
        await _leagueRepository.UpdateLeagueRoundBoostsAsync(
            Arg.Do<IEnumerable<LeagueRoundBoostUpdate>>(u => captured = u.ToList()), Arg.Any<CancellationToken>());

        await BuildService().ApplyRoundBoostsAsync(RoundId, CancellationToken.None);

        captured.Should().ContainSingle();
        captured![0].UserId.Should().Be(UserId);
        captured[0].LeagueId.Should().Be(LeagueId);
        captured[0].AppliedBoostCode.Should().Be(BoostCode);
        untouched.BoostedPoints.Should().Be(8);
    }

    [Fact]
    public async Task ApplyRoundBoostsAsync_ShouldMatchOnLeagueAsWellAsPlayer()
    {
        // The same player can be in several leagues and only have boosted one of them.
        var boostedLeague = Result(LeagueId, UserId, 10);
        var otherLeague = Result(LeagueId + 1, UserId, 10);

        _leagueRepository.GetLeagueRoundResultsAsync(RoundId, Arg.Any<CancellationToken>()).Returns([boostedLeague, otherLeague]);
        _readRepository.GetBoostsForRoundAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns([new UserRoundBoostDto(LeagueId, UserId, BoostCode)]);

        List<LeagueRoundBoostUpdate>? captured = null;
        await _leagueRepository.UpdateLeagueRoundBoostsAsync(
            Arg.Do<IEnumerable<LeagueRoundBoostUpdate>>(u => captured = u.ToList()), Arg.Any<CancellationToken>());

        await BuildService().ApplyRoundBoostsAsync(RoundId, CancellationToken.None);

        captured.Should().ContainSingle();
        captured![0].LeagueId.Should().Be(LeagueId);
        otherLeague.BoostedPoints.Should().Be(10);
    }

    // ---------- auto-apply on the final round ----------

    [Fact]
    public async Task AutoApplyUnusedBoostsForLastRoundAsync_ShouldApplyAgainstTheRoundsOwnSeason()
    {
        _readRepository.GetRoundInfoAsync(RoundId, Arg.Any<CancellationToken>())
            .Returns((SeasonId, RoundNumber, NowUtc));
        _writeRepository.AutoApplyUnusedBoostsForRoundAsync(SeasonId, RoundId, Arg.Any<CancellationToken>()).Returns(4);

        var applied = await BuildService().AutoApplyUnusedBoostsForLastRoundAsync(RoundId, CancellationToken.None);

        applied.Should().Be(4);
        await _writeRepository.Received(1).AutoApplyUnusedBoostsForRoundAsync(SeasonId, RoundId, Arg.Any<CancellationToken>());
    }
}
