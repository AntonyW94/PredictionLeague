using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Web.Client.Services.Dashboard;

public interface IDashboardStateService
{
    List<MyLeagueDto> MyLeagues { get; }
    List<AvailableLeagueDto> AvailableLeagues { get; }
    List<LeagueLeaderboardDto> Leaderboards { get; }
    List<UpcomingRoundDto> UpcomingRounds { get; }
    List<LeagueRequestDto> PendingRequests { get; }

    bool HasAvailablePrivateLeagues { get; }
    bool IsMyLeaguesLoading { get; }
    bool IsAvailableLeaguesLoading { get; }
    bool IsLeaderboardsLoading { get; }
    bool IsUpcomingRoundsLoading { get; }
    bool IsPendingRequestsLoading { get; }

    string? MyLeaguesErrorMessage { get; }
    string? AvailableLeaguesErrorMessage { get; }
    string? LeaderboardsErrorMessage { get; }
    string? UpcomingRoundsErrorMessage { get; }
    string? UpcomingRoundsSuccessMessage { get; }
    string? PendingRequestsErrorMessage { get; }

    event Action OnStateChange;
 
    Task LoadMyLeaguesAsync();
    Task LoadAvailableLeaguesAsync();
    Task LoadLeaderboardsAsync();
    Task LoadUpcomingRoundsAsync(); 
    Task LoadPendingRequestsAsync();

    Task JoinPublicLeagueAsync(int leagueId);
    Task CancelJoinRequestAsync(int leagueId);
    Task DismissAlertAsync(int leagueId);
}