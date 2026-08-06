using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Contracts.Onboarding;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Web.Client.Services.Dashboard;
using ThePredictions.Web.Client.Services.Leagues;
using ThePredictions.Web.Client.Services.Onboarding;
using ThePredictions.Web.Client.Services.SeasonPasses;
using Xunit;

namespace ThePredictions.Web.Client.Tests.Unit.Services.Dashboard;

/// <summary>
/// Everything on the dashboard is driven from here. Each load has to raise a change notification so
/// the page redraws, and has to leave a readable message rather than a blank panel when the API is
/// unreachable.
/// </summary>
public class DashboardStateServiceTests
{
    private readonly ILeagueService _leagueService = Substitute.For<ILeagueService>();
    private readonly ISeasonPassService _seasonPassService = Substitute.For<ISeasonPassService>();
    private readonly IOnboardingService _onboardingService = Substitute.For<IOnboardingService>();

    private readonly DashboardStateService _service;
    private int _notifications;

    public DashboardStateServiceTests()
    {
        _service = new DashboardStateService(_leagueService, _seasonPassService, _onboardingService);
        _service.OnStateChange += () => _notifications++;
    }

    private static readonly Exception Offline = new HttpRequestException("offline");

    private static MyLeagueDto League(int id, bool archived = false) =>
        new(id, $"League {id}", "Season", CompetitionType.League, null, null, "R1", "Aug", null, 10,
            1, 1, 1, null, null, null, "InProgress", 0, 0, 0m, 0m, 0m, 0m, true, 0, 0, false, archived, null, null, null);

    private static LeagueLeaderboardDto Leaderboard(int leagueId, bool archived = false) =>
        new() { LeagueId = leagueId, LeagueName = $"League {leagueId}", IsArchivedByUser = archived };

    // ---------- My Leagues ----------

    [Fact]
    public async Task LoadMyLeaguesAsync_ShouldPublishTheLeaguesAndClearTheLoadingFlag()
    {
        _leagueService.GetMyLeaguesAsync().Returns([League(1)]);

        await _service.LoadMyLeaguesAsync();

        _service.MyLeagues.Should().ContainSingle();
        _service.IsMyLeaguesLoading.Should().BeFalse();
        _service.MyLeaguesErrorMessage.Should().BeNull();
        _notifications.Should().Be(2);
    }

    [Fact]
    public async Task LoadMyLeaguesAsync_ShouldReportAReadableError_WhenTheApiIsUnreachable()
    {
        _leagueService.GetMyLeaguesAsync().Throws(Offline);

        await _service.LoadMyLeaguesAsync();

        _service.MyLeaguesErrorMessage.Should().Be("Could not load your leagues.");
        _service.IsMyLeaguesLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadMyLeaguesAsync_ShouldClearAPreviousError_OnRetry()
    {
        var calls = 0;
        _leagueService.GetMyLeaguesAsync().Returns(_ =>
        {
            calls++;
            if (calls == 1)
                throw Offline;

            return new List<MyLeagueDto> { League(1) };
        });

        await _service.LoadMyLeaguesAsync();
        _service.MyLeaguesErrorMessage.Should().NotBeNull();

        await _service.LoadMyLeaguesAsync();

        _service.MyLeaguesErrorMessage.Should().BeNull();
        _service.MyLeagues.Should().ContainSingle();
    }

    // ---------- Available leagues ----------

    [Fact]
    public async Task LoadAvailableLeaguesAsync_ShouldPublishBothPublicAndPrivateAvailability()
    {
        _leagueService.GetAvailableLeaguesAsync().Returns([new AvailableLeagueDto(1, "Open", "Season", 10m, default, 8, 100m, false)]);
        _leagueService.CheckForAvailablePrivateLeaguesAsync().Returns(true);

        await _service.LoadAvailableLeaguesAsync();

        _service.AvailableLeagues.Should().ContainSingle();
        _service.HasAvailablePrivateLeagues.Should().BeTrue();
        _service.IsAvailableLeaguesLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadAvailableLeaguesAsync_ShouldResetToEmpty_WhenTheApiIsUnreachable()
    {
        _leagueService.GetAvailableLeaguesAsync().Throws(Offline);
        _leagueService.CheckForAvailablePrivateLeaguesAsync().Returns(true);

        await _service.LoadAvailableLeaguesAsync();

        _service.AvailableLeaguesErrorMessage.Should().Be("Could not load available leagues.");
        _service.AvailableLeagues.Should().BeEmpty();
        _service.HasAvailablePrivateLeagues.Should().BeFalse();
    }

    // ---------- Season passes ----------

    [Fact]
    public async Task LoadAvailableSeasonPassesAsync_ShouldPublishThePasses()
    {
        _seasonPassService.GetAvailablePassesAsync().Returns([Pass(1)]);

        await _service.LoadAvailableSeasonPassesAsync();

        _service.AvailableSeasonPasses.Should().ContainSingle();
    }

    [Fact]
    public async Task LoadAvailableSeasonPassesAsync_ShouldFallBackToEmpty_WhenTheApiIsUnreachable()
    {
        _seasonPassService.GetAvailablePassesAsync().Throws(Offline);

        await _service.LoadAvailableSeasonPassesAsync();

        _service.AvailableSeasonPasses.Should().BeEmpty();
    }

    private static AvailableSeasonPassDto Pass(int seasonId) =>
        new(seasonId, $"Season {seasonId}", null, true, 10m, null, false, 0, null);

    // ---------- Onboarding ----------

    [Fact]
    public async Task LoadOnboardingAsync_ShouldPublishTheChecklist()
    {
        var checklist = new OnboardingChecklistDto(false, true, []);
        _onboardingService.GetChecklistAsync().Returns(checklist);

        await _service.LoadOnboardingAsync();

        _service.OnboardingChecklist.Should().Be(checklist);
    }

    [Fact]
    public async Task LoadOnboardingAsync_ShouldHideTheChecklist_WhenTheApiIsUnreachable()
    {
        _onboardingService.GetChecklistAsync().Throws(Offline);

        await _service.LoadOnboardingAsync();

        _service.OnboardingChecklist.Should().BeNull();
    }

    [Fact]
    public async Task SkipOnboardingStepAsync_ShouldSkipThenReload()
    {
        _onboardingService.GetChecklistAsync().Returns(new OnboardingChecklistDto(false, true, []));

        await _service.SkipOnboardingStepAsync("add-mobile");

        await _onboardingService.Received(1).SkipAsync("add-mobile");
        await _onboardingService.Received(1).GetChecklistAsync();
    }

    [Fact]
    public async Task DismissOnboardingAsync_ShouldDismissThenReload()
    {
        _onboardingService.GetChecklistAsync().Returns(new OnboardingChecklistDto(true, false, []));

        await _service.DismissOnboardingAsync();

        await _onboardingService.Received(1).DismissAsync();
        await _onboardingService.Received(1).GetChecklistAsync();
    }

    // ---------- Leaderboards and rounds ----------

    [Fact]
    public async Task LoadLeaderboardsAsync_ShouldPublishTheStandings()
    {
        _leagueService.GetLeaderboardsAsync().Returns([Leaderboard(1)]);

        await _service.LoadLeaderboardsAsync();

        _service.Leaderboards.Should().ContainSingle();
        _service.IsLeaderboardsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadLeaderboardsAsync_ShouldReportAnError_WhenTheApiIsUnreachable()
    {
        _leagueService.GetLeaderboardsAsync().Throws(Offline);

        await _service.LoadLeaderboardsAsync();

        _service.LeaderboardsErrorMessage.Should().Be("Could not load leaderboards");
    }

    [Fact]
    public async Task LoadActiveRoundsAsync_ShouldPublishTheRounds()
    {
        _leagueService.GetActiveRoundsAsync().Returns([new ActiveRoundDto(1, "Round 1", 1, null, false, default, default, false, RoundStatus.InProgress, [], null)]);

        await _service.LoadActiveRoundsAsync();

        _service.ActiveRounds.Should().ContainSingle();
        _service.ActiveRoundsSuccessMessage.Should().BeNull();
    }

    [Fact]
    public async Task LoadActiveRoundsAsync_ShouldReportAnError_WhenTheApiIsUnreachable()
    {
        _leagueService.GetActiveRoundsAsync().Throws(Offline);

        await _service.LoadActiveRoundsAsync();

        _service.ActiveRoundsErrorMessage.Should().Be("Could not load active rounds");
    }

    [Fact]
    public async Task LoadPendingRequestsAsync_ShouldPublishTheRequests()
    {
        _leagueService.GetPendingRequestsAsync().Returns([new LeagueRequestDto(1, "League 1", "Season", LeagueMemberStatus.Pending, default, default, "Admin", 8, 10m, 100m)]);

        await _service.LoadPendingRequestsAsync();

        _service.PendingRequests.Should().ContainSingle();
    }

    [Fact]
    public async Task LoadPendingRequestsAsync_ShouldReportAnError_WhenTheApiIsUnreachable()
    {
        _leagueService.GetPendingRequestsAsync().Throws(Offline);

        await _service.LoadPendingRequestsAsync();

        _service.PendingRequestsErrorMessage.Should().Be("Could not load pending requests.");
    }

    // ---------- Joining and leaving ----------

    [Fact]
    public async Task JoinPublicLeagueAsync_ShouldRefreshEverythingAffected_OnSuccess()
    {
        _leagueService.JoinPublicLeagueAsync(5).Returns((true, (string?)null));

        await _service.JoinPublicLeagueAsync(5);

        await _leagueService.Received(1).GetMyLeaguesAsync();
        await _leagueService.Received(1).GetAvailableLeaguesAsync();
        await _leagueService.Received(1).GetPendingRequestsAsync();
        await _onboardingService.Received(1).GetChecklistAsync();
        _service.AvailableLeaguesErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task JoinPublicLeagueAsync_ShouldSurfaceTheReason_WhenTheJoinIsRefused()
    {
        _leagueService.JoinPublicLeagueAsync(5).Returns((false, "That league is full."));

        await _service.JoinPublicLeagueAsync(5);

        _service.AvailableLeaguesErrorMessage.Should().Be("That league is full.");
        await _leagueService.DidNotReceive().GetMyLeaguesAsync();
    }

    [Fact]
    public async Task CancelJoinRequestAsync_ShouldRefreshRequestsAndAvailability_OnSuccess()
    {
        _leagueService.CancelJoinRequestAsync(5).Returns((true, (string?)null));

        await _service.CancelJoinRequestAsync(5);

        await _leagueService.Received(1).GetPendingRequestsAsync();
        await _leagueService.Received(1).GetAvailableLeaguesAsync();
    }

    [Fact]
    public async Task CancelJoinRequestAsync_ShouldSurfaceTheReason_OnFailure()
    {
        _leagueService.CancelJoinRequestAsync(5).Returns((false, "Already approved."));

        await _service.CancelJoinRequestAsync(5);

        _service.PendingRequestsErrorMessage.Should().Be("Already approved.");
        await _leagueService.DidNotReceive().GetPendingRequestsAsync();
    }

    [Fact]
    public async Task DismissAlertAsync_ShouldRefreshRequests_OnSuccess()
    {
        _leagueService.DismissAlertAsync(5).Returns((true, (string?)null));

        await _service.DismissAlertAsync(5);

        await _leagueService.Received(1).GetPendingRequestsAsync();
    }

    [Fact]
    public async Task DismissAlertAsync_ShouldSurfaceTheReason_OnFailure()
    {
        _leagueService.DismissAlertAsync(5).Returns((false, "Nothing to dismiss."));

        await _service.DismissAlertAsync(5);

        _service.PendingRequestsErrorMessage.Should().Be("Nothing to dismiss.");
    }

    // ---------- Archiving ----------

    [Fact]
    public async Task SetLeagueArchivedAsync_ShouldDoNothing_WhenTheLeagueIsOnNeitherList()
    {
        await _service.SetLeagueArchivedAsync(99, isArchived: true);

        await _leagueService.DidNotReceiveWithAnyArgs().SetLeagueArchivedAsync(default, default);
    }

    [Fact]
    public async Task SetLeagueArchivedAsync_ShouldUpdateBothListsInLockstep()
    {
        await GivenLoadedListsAsync();
        _leagueService.SetLeagueArchivedAsync(1, true).Returns((true, (string?)null));

        await _service.SetLeagueArchivedAsync(1, isArchived: true);

        _service.MyLeagues[0].IsArchivedByUser.Should().BeTrue();
        _service.Leaderboards[0].IsArchivedByUser.Should().BeTrue();
    }

    [Fact]
    public async Task SetLeagueArchivedAsync_ShouldPutBothListsBack_WhenTheSaveFails()
    {
        // The change is applied optimistically so the tile flips immediately; a failure has to
        // undo it rather than leave the dashboard disagreeing with the server.
        await GivenLoadedListsAsync();
        _leagueService.SetLeagueArchivedAsync(1, true).Returns((false, "Could not archive."));

        await _service.SetLeagueArchivedAsync(1, isArchived: true);

        _service.MyLeagues[0].IsArchivedByUser.Should().BeFalse();
        _service.Leaderboards[0].IsArchivedByUser.Should().BeFalse();
        _service.MyLeaguesErrorMessage.Should().Be("Could not archive.");
        _service.LeaderboardsErrorMessage.Should().Be("Could not archive.");
    }

    [Fact]
    public async Task SetLeagueArchivedAsync_ShouldCope_WhenOnlyTheStandingsListIsLoaded()
    {
        _leagueService.GetLeaderboardsAsync().Returns([Leaderboard(1)]);
        await _service.LoadLeaderboardsAsync();
        _leagueService.SetLeagueArchivedAsync(1, true).Returns((true, (string?)null));

        await _service.SetLeagueArchivedAsync(1, isArchived: true);

        _service.Leaderboards[0].IsArchivedByUser.Should().BeTrue();
        _service.MyLeagues.Should().BeEmpty();
    }

    [Fact]
    public async Task SetLeagueArchivedAsync_ShouldCope_WhenOnlyMyLeaguesIsLoaded()
    {
        _leagueService.GetMyLeaguesAsync().Returns([League(1)]);
        await _service.LoadMyLeaguesAsync();
        _leagueService.SetLeagueArchivedAsync(1, false).Returns((false, "Nope."));

        await _service.SetLeagueArchivedAsync(1, isArchived: false);

        _service.MyLeagues[0].IsArchivedByUser.Should().BeFalse();
        _service.MyLeaguesErrorMessage.Should().Be("Nope.");
    }

    private async Task GivenLoadedListsAsync()
    {
        _leagueService.GetMyLeaguesAsync().Returns([League(1)]);
        _leagueService.GetLeaderboardsAsync().Returns([Leaderboard(1)]);
        await _service.LoadMyLeaguesAsync();
        await _service.LoadLeaderboardsAsync();
    }

    // ---------- Admin: pending members ----------

    [Fact]
    public async Task LoadPendingMembersAsync_ShouldPublishTheAdminView()
    {
        _leagueService.GetPendingMembersForAdminAsync().Returns(new PendingMembersResultDto
        {
            IsAdminOfOpenLeague = true,
            AdminLeagues = [new AdminLeagueSummaryDto(1, "League 1", default, 8, 1, 10m, true, null)],
            Members = [new PendingLeagueMemberDto(1, "League 1", "user-1", "Alex Player", default)]
        });

        await _service.LoadPendingMembersAsync();

        _service.IsAdminOfOpenLeague.Should().BeTrue();
        _service.AdminLeagues.Should().ContainSingle();
        _service.PendingMembers.Should().ContainSingle();
        _service.IsPendingMembersLoading.Should().BeFalse();
    }

    [Fact]
    public async Task LoadPendingMembersAsync_ShouldReportAnError_WhenTheApiIsUnreachable()
    {
        _leagueService.GetPendingMembersForAdminAsync().Throws(Offline);

        await _service.LoadPendingMembersAsync();

        _service.PendingMembersErrorMessage.Should().Be("Could not load pending members.");
    }

    [Fact]
    public async Task ApproveMemberAsync_ShouldApproveThenReload()
    {
        _leagueService.GetPendingMembersForAdminAsync().Returns(new PendingMembersResultDto());

        await _service.ApproveMemberAsync(5, "user-1");

        await _leagueService.Received(1).UpdateMemberStatusAsync(5, "user-1", LeagueMemberStatus.Approved);
        await _leagueService.Received(1).GetPendingMembersForAdminAsync();
    }

    [Fact]
    public async Task ApproveMemberAsync_ShouldReportAnError_WhenTheUpdateFails()
    {
        _leagueService.UpdateMemberStatusAsync(5, "user-1", LeagueMemberStatus.Approved).Throws(Offline);

        await _service.ApproveMemberAsync(5, "user-1");

        _service.PendingMembersErrorMessage.Should().Be("Could not approve member.");
    }

    [Fact]
    public async Task RejectMemberAsync_ShouldRejectThenReload()
    {
        _leagueService.GetPendingMembersForAdminAsync().Returns(new PendingMembersResultDto());

        await _service.RejectMemberAsync(5, "user-1");

        await _leagueService.Received(1).UpdateMemberStatusAsync(5, "user-1", LeagueMemberStatus.Rejected);
        await _leagueService.Received(1).GetPendingMembersForAdminAsync();
    }

    [Fact]
    public async Task RejectMemberAsync_ShouldReportAnError_WhenTheUpdateFails()
    {
        _leagueService.UpdateMemberStatusAsync(5, "user-1", LeagueMemberStatus.Rejected).Throws(Offline);

        await _service.RejectMemberAsync(5, "user-1");

        _service.PendingMembersErrorMessage.Should().Be("Could not reject member.");
    }
}
