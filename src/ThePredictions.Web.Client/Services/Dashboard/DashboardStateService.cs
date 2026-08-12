using System.Text.Json;
using ThePredictions.Contracts.Dashboard;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Contracts.Onboarding;
using ThePredictions.Contracts.SeasonPasses;
using ThePredictions.Domain.Common.Enumerations;
using ThePredictions.Web.Client.Services.Leagues;
using ThePredictions.Web.Client.Services.Onboarding;
using ThePredictions.Web.Client.Services.SeasonPasses;

namespace ThePredictions.Web.Client.Services.Dashboard;

public class DashboardStateService(ILeagueService leagueService, ISeasonPassService seasonPassService, IOnboardingService onboardingService) : IDashboardStateService
{
    public List<MyLeagueDto> MyLeagues { get; private set; } = [];
    public List<AvailableLeagueDto> AvailableLeagues { get; private set; } = [];
    public List<AvailableSeasonPassDto> AvailableSeasonPasses { get; private set; } = [];
    public OnboardingChecklistDto? OnboardingChecklist { get; private set; }
    public List<LeagueLeaderboardDto> Leaderboards { get; private set; } = [];
    public List<ActiveRoundDto> ActiveRounds { get; private set; } = [];
    public List<LeagueRequestDto> PendingRequests { get; private set; } = [];
    public List<PendingLeagueMemberDto> PendingMembers { get; private set; } = [];
    public List<AdminLeagueSummaryDto> AdminLeagues { get; private set; } = [];
    public bool IsAdminOfOpenLeague { get; private set; }

    public bool HasAvailablePrivateLeagues { get; private set; }
    public bool IsMyLeaguesLoading { get; private set; }
    public bool IsAvailableLeaguesLoading { get; private set; }
    public bool IsLeaderboardsLoading { get; private set; }
    public bool IsActiveRoundsLoading { get; private set; }
    public bool IsPendingRequestsLoading { get; private set; }
    public bool IsPendingMembersLoading { get; private set; }

    public string? AvailableLeaguesErrorMessage { get; private set; }
    public string? MyLeaguesErrorMessage { get; private set; }
    public string? LeaderboardsErrorMessage { get; private set; }
    public string? ActiveRoundsErrorMessage { get; private set; }
    public string? ActiveRoundsSuccessMessage { get; private set; }
    public string? PendingRequestsErrorMessage { get; private set; }
    public string? PendingMembersErrorMessage { get; private set; }

    public event Action? OnStateChange;

    /// <summary>
    /// True only while one of the user's active rounds has a match actually being
    /// played. A round stays "in progress" for the whole gameweek (often days)
    /// even when no match is on, so we key off real match status - not round
    /// status - to avoid polling during the gaps between matches.
    /// </summary>
    public bool IsAnyRoundLive =>
        ActiveRounds.Any(r => r.Matches.Any(m => m.Status == MatchStatus.InProgress));

    /// <summary>
    /// Silently re-fetches the active rounds and standings, raising
    /// <see cref="OnStateChange"/> only when something changed (no flicker) and
    /// keeping the last-known values if a poll fails.
    /// </summary>
    public async Task RefreshLiveDataAsync()
    {
        try
        {
            var activeRoundsTask = leagueService.GetActiveRoundsAsync();
            var leaderboardsTask = leagueService.GetLeaderboardsAsync();

            await Task.WhenAll(activeRoundsTask, leaderboardsTask);

            var newActiveRounds = activeRoundsTask.Result;
            var newLeaderboards = leaderboardsTask.Result;

            var changed =
                HasChanged(ActiveRounds, newActiveRounds)
                | HasChanged(Leaderboards, newLeaderboards);

            if (!changed)
                return;

            ActiveRounds = newActiveRounds;
            Leaderboards = newLeaderboards;

            NotifyStateChanged();
        }
        catch
        {
            // Keep the last-known values on a failed poll; the dashboard must not crash.
        }
    }

    private static bool HasChanged<T>(List<T> current, List<T> updated) =>
        JsonSerializer.Serialize(current) != JsonSerializer.Serialize(updated);

    public async Task LoadMyLeaguesAsync()
    {
        IsMyLeaguesLoading = true;
        MyLeaguesErrorMessage = null;

        NotifyStateChanged();

        try
        {
            MyLeagues = await leagueService.GetMyLeaguesAsync();
        }
        catch
        {
            MyLeaguesErrorMessage = "Could not load your leagues.";
        }
        finally
        {
            IsMyLeaguesLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task LoadAvailableLeaguesAsync()
    {
        IsAvailableLeaguesLoading = true;
        AvailableLeaguesErrorMessage = null;

        NotifyStateChanged();

        try
        {
            var publicLeaguesTask = leagueService.GetAvailableLeaguesAsync();
            var privateLeaguesTask = leagueService.CheckForAvailablePrivateLeaguesAsync();

            await Task.WhenAll(publicLeaguesTask, privateLeaguesTask);

            AvailableLeagues = await publicLeaguesTask;
            HasAvailablePrivateLeagues = await privateLeaguesTask;
        }
        catch
        {
            AvailableLeaguesErrorMessage = "Could not load available leagues.";
            AvailableLeagues = [];
            HasAvailablePrivateLeagues = false;
        }
        finally
        {
            IsAvailableLeaguesLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task LoadAvailableSeasonPassesAsync()
    {
        NotifyStateChanged();

        try
        {
            AvailableSeasonPasses = await seasonPassService.GetAvailablePassesAsync();
        }
        catch
        {
            AvailableSeasonPasses = [];
        }
        finally
        {
            NotifyStateChanged();
        }
    }

    public async Task LoadOnboardingAsync()
    {
        NotifyStateChanged();

        try
        {
            OnboardingChecklist = await onboardingService.GetChecklistAsync();
        }
        catch
        {
            OnboardingChecklist = null;
        }
        finally
        {
            NotifyStateChanged();
        }
    }

    public async Task SkipOnboardingStepAsync(string stepKey)
    {
        await onboardingService.SkipAsync(stepKey);
        await LoadOnboardingAsync();
    }

    public async Task DismissOnboardingAsync()
    {
        await onboardingService.DismissAsync();
        await LoadOnboardingAsync();
    }

    // Dashboard prompt strip - condition-driven CTAs shown above the dashboard. Each acquirable
    // season becomes a "get your pass" prompt that self-dismisses once acquired. Append future
    // prompts (e.g. missing mobile number, no profile photo) to this list.
    public IReadOnlyList<DashboardPrompt> Prompts
    {
        get
        {
            var prompts = new List<DashboardPrompt>();

            prompts.AddRange(AvailableSeasonPasses.Select(pass => new DashboardPrompt(
                "bi-ticket-perforated-fill",
                $"Get your {pass.SeasonName} pass to join its leagues.",
                "Get pass",
                $"/season-passes?seasonId={pass.SeasonId}",
                Highlight: pass.SeasonName)));

            return prompts;
        }
    }

    public async Task LoadLeaderboardsAsync()
    {
        IsLeaderboardsLoading = true;
        LeaderboardsErrorMessage = null;

        NotifyStateChanged();

        try
        {
            Leaderboards = await leagueService.GetLeaderboardsAsync();
        }
        catch
        {
            LeaderboardsErrorMessage = "Could not load leaderboards";
        }
        finally
        {
            IsLeaderboardsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task LoadActiveRoundsAsync()
    {
        IsActiveRoundsLoading = true;
        ActiveRoundsErrorMessage = null;
        ActiveRoundsSuccessMessage = null;

        NotifyStateChanged();

        try
        {
            ActiveRounds = await leagueService.GetActiveRoundsAsync();
        }
        catch
        {
            ActiveRoundsErrorMessage = "Could not load active rounds";
        }
        finally
        {
            IsActiveRoundsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task LoadPendingRequestsAsync()
    {
        IsPendingRequestsLoading = true;
        PendingRequestsErrorMessage = null;
        NotifyStateChanged();

        try
        {
             PendingRequests = await leagueService.GetPendingRequestsAsync();
        }
        catch
        {
            PendingRequestsErrorMessage = "Could not load pending requests.";
        }
        finally
        {
            IsPendingRequestsLoading = false;
            NotifyStateChanged();
        }
    }

    public PendingJoin? PendingJoin { get; private set; }

    public void RememberPendingJoin(PendingJoin pendingJoin)
    {
        PendingJoin = pendingJoin;
    }

    public PendingJoin? TakePendingJoin()
    {
        var pendingJoin = PendingJoin;
        PendingJoin = null;

        return pendingJoin;
    }

    public const string NeedsSeasonPassMessage =
        "You need a Season Pass for this season before you can join a league in it. You can get one from the Season Passes page.";

    public async Task JoinPublicLeagueAsync(int leagueId)
    {
        AvailableLeaguesErrorMessage = null;

        NotifyStateChanged();

        var (success, errorMessage, needsSeasonPass) = await leagueService.JoinPublicLeagueAsync(leagueId);
        if (success)
        {
            await Task.WhenAll(LoadMyLeaguesAsync(), LoadAvailableLeaguesAsync(), LoadPendingRequestsAsync(), LoadOnboardingAsync());
        }
        else
        {
            // Needing a pass is not a mistake the player made, so it is worded as what to do next rather than as the
            // server's refusal.
            AvailableLeaguesErrorMessage = needsSeasonPass ? NeedsSeasonPassMessage : errorMessage;
            NotifyStateChanged();
        }
    }

    public async Task CancelJoinRequestAsync(int leagueId)
    {
        PendingRequestsErrorMessage = null;
        NotifyStateChanged();

        var (success, errorMessage) = await leagueService.CancelJoinRequestAsync(leagueId);
        if (success)
        {
            await LoadPendingRequestsAsync();
            await LoadAvailableLeaguesAsync();
        }
        else
        {
            PendingRequestsErrorMessage = errorMessage;
            NotifyStateChanged();
        }
    }

    public async Task DismissAlertAsync(int leagueId)
    {
        PendingRequestsErrorMessage = null;
        NotifyStateChanged();

        var (success, errorMessage) = await leagueService.DismissAlertAsync(leagueId);
        if (success)
        {
            await LoadPendingRequestsAsync();
        }
        else
        {
            PendingRequestsErrorMessage = errorMessage;
            NotifyStateChanged();
        }
    }

    public async Task SetLeagueArchivedAsync(int leagueId, bool isArchived)
    {
        MyLeaguesErrorMessage = null;
        LeaderboardsErrorMessage = null;

        // Archiving is a single per-user flag shared across the dashboard, so optimistically
        // update both the My Leagues and Standings lists (whichever are loaded) in lockstep.
        var myLeagueIndex = MyLeagues.FindIndex(l => l.Id == leagueId);
        var leaderboardIndex = Leaderboards.FindIndex(l => l.LeagueId == leagueId);

        if (myLeagueIndex < 0 && leaderboardIndex < 0)
            return;

        var originalMyLeague = myLeagueIndex >= 0 ? MyLeagues[myLeagueIndex] : null;
        var originalLeaderboard = leaderboardIndex >= 0 ? Leaderboards[leaderboardIndex] : null;

        ApplyArchivedFlag(myLeagueIndex, leaderboardIndex, isArchived);
        NotifyStateChanged();

        var (success, errorMessage) = await leagueService.SetLeagueArchivedAsync(leagueId, isArchived);
        if (success)
            return;

        RestoreAfterFailedArchive(myLeagueIndex, leaderboardIndex, originalMyLeague, originalLeaderboard, errorMessage);
    }

    /// <summary>
    /// Flips the flag in whichever lists are loaded, so the change shows immediately rather than
    /// after the round trip.
    /// </summary>
    private void ApplyArchivedFlag(int myLeagueIndex, int leaderboardIndex, bool isArchived)
    {
        if (myLeagueIndex >= 0)
            MyLeagues[myLeagueIndex] = MyLeagues[myLeagueIndex] with { IsArchivedByUser = isArchived };

        if (leaderboardIndex >= 0)
            Leaderboards[leaderboardIndex] = Leaderboards[leaderboardIndex] with { IsArchivedByUser = isArchived };
    }

    /// <summary>Puts the lists back as they were and surfaces why the change did not stick.</summary>
    private void RestoreAfterFailedArchive(
        int myLeagueIndex, int leaderboardIndex, MyLeagueDto? originalMyLeague, LeagueLeaderboardDto? originalLeaderboard, string? errorMessage)
    {
        if (originalMyLeague is not null)
            MyLeagues[myLeagueIndex] = originalMyLeague;

        if (originalLeaderboard is not null)
            Leaderboards[leaderboardIndex] = originalLeaderboard;

        MyLeaguesErrorMessage = errorMessage;
        LeaderboardsErrorMessage = errorMessage;
        NotifyStateChanged();
    }

    public async Task LoadPendingMembersAsync()
    {
        IsPendingMembersLoading = true;
        PendingMembersErrorMessage = null;
        NotifyStateChanged();

        try
        {
            var result = await leagueService.GetPendingMembersForAdminAsync();
            IsAdminOfOpenLeague = result.IsAdminOfOpenLeague;
            AdminLeagues = result.AdminLeagues;
            PendingMembers = result.Members;
        }
        catch
        {
            PendingMembersErrorMessage = "Could not load pending members.";
        }
        finally
        {
            IsPendingMembersLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task ApproveMemberAsync(int leagueId, string userId)
    {
        PendingMembersErrorMessage = null;
        NotifyStateChanged();

        try
        {
            await leagueService.UpdateMemberStatusAsync(leagueId, userId, LeagueMemberStatus.Approved);
            await LoadPendingMembersAsync();
        }
        catch
        {
            PendingMembersErrorMessage = "Could not approve member.";
            NotifyStateChanged();
        }
    }

    public async Task RejectMemberAsync(int leagueId, string userId)
    {
        PendingMembersErrorMessage = null;
        NotifyStateChanged();

        try
        {
            await leagueService.UpdateMemberStatusAsync(leagueId, userId, LeagueMemberStatus.Rejected);
            await LoadPendingMembersAsync();
        }
        catch
        {
            PendingMembersErrorMessage = "Could not reject member.";
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnStateChange?.Invoke();
}
