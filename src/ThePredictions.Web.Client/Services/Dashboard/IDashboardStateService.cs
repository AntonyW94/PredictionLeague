using ThePredictions.Contracts.Dashboard;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Contracts.Leagues;
using ThePredictions.Contracts.Onboarding;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Web.Client.Services.Dashboard;

public interface IDashboardStateService
{
    List<MyLeagueDto> MyLeagues { get; }
    List<AvailableLeagueDto> AvailableLeagues { get; }
    List<AvailableSeasonPassDto> AvailableSeasonPasses { get; }
    OnboardingChecklistDto? OnboardingChecklist { get; }
    IReadOnlyList<DashboardPrompt> Prompts { get; }
    List<LeagueLeaderboardDto> Leaderboards { get; }
    List<ActiveRoundDto> ActiveRounds { get; }
    List<LeagueRequestDto> PendingRequests { get; }
    List<PendingLeagueMemberDto> PendingMembers { get; }
    List<AdminLeagueSummaryDto> AdminLeagues { get; }
    bool IsAdminOfOpenLeague { get; }

    bool IsAnyRoundLive { get; }

    bool HasAvailablePrivateLeagues { get; }
    bool IsMyLeaguesLoading { get; }
    bool IsAvailableLeaguesLoading { get; }
    bool IsLeaderboardsLoading { get; }
    bool IsActiveRoundsLoading { get; }
    bool IsPendingRequestsLoading { get; }
    bool IsPendingMembersLoading { get; }

    string? MyLeaguesErrorMessage { get; }
    string? AvailableLeaguesErrorMessage { get; }
    string? LeaderboardsErrorMessage { get; }
    string? ActiveRoundsErrorMessage { get; }
    string? ActiveRoundsSuccessMessage { get; }
    string? PendingRequestsErrorMessage { get; }
    string? PendingMembersErrorMessage { get; }

    event Action OnStateChange;

    Task LoadMyLeaguesAsync();
    Task LoadAvailableLeaguesAsync();
    Task LoadAvailableSeasonPassesAsync();
    Task LoadOnboardingAsync();
    Task SkipOnboardingStepAsync(string stepKey);
    Task DismissOnboardingAsync();
    Task LoadLeaderboardsAsync();
    Task LoadActiveRoundsAsync();
    Task RefreshLiveDataAsync();
    Task LoadPendingRequestsAsync();
    Task LoadPendingMembersAsync();
    Task ApproveMemberAsync(int leagueId, string userId);
    Task RejectMemberAsync(int leagueId, string userId);

    Task JoinPublicLeagueAsync(int leagueId);

    /// <summary>
    /// The league somebody was heading for when they were told they needed a Season Pass, if there is one.
    /// </summary>
    PendingJoin? PendingJoin { get; }

    /// <summary>Remembers where they were heading, so buying a pass can offer to take them back.</summary>
    void RememberPendingJoin(PendingJoin pendingJoin);

    /// <summary>
    /// Returns what was remembered and forgets it, so the offer is made once. Coming back to the dashboard a second time
    /// must not reopen a join they have since abandoned.
    /// </summary>
    PendingJoin? TakePendingJoin();
    Task CancelJoinRequestAsync(int leagueId);
    Task DismissAlertAsync(int leagueId);
    Task SetLeagueArchivedAsync(int leagueId, bool isArchived);
}
