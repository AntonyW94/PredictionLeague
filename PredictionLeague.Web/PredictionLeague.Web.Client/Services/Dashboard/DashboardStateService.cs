using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Web.Client.Services.Leagues;

namespace PredictionLeague.Web.Client.Services.Dashboard;

public class DashboardStateService(ILeagueService leagueService) : IDashboardStateService
{
    public List<MyLeagueDto> MyLeagues { get; private set; } = [];
    public List<AvailableLeagueDto> AvailableLeagues { get; private set; } = [];
    public List<LeagueLeaderboardDto> Leaderboards { get; private set; } = [];
    public List<ActiveRoundDto> ActiveRounds { get; private set; } = [];
    public List<LeagueRequestDto> PendingRequests { get; private set; } = [];

    public bool HasAvailablePrivateLeagues { get; private set; }
    public bool IsMyLeaguesLoading { get; private set; }
    public bool IsAvailableLeaguesLoading { get; private set; }
    public bool IsLeaderboardsLoading { get; private set; }
    public bool IsActiveRoundsLoading { get; private set; }
    public bool IsPendingRequestsLoading { get; private set; }

    public string? AvailableLeaguesErrorMessage { get; private set; }
    public string? MyLeaguesErrorMessage { get; private set; }
    public string? LeaderboardsErrorMessage { get; private set; }
    public string? ActiveRoundsErrorMessage { get; private set; }
    public string? ActiveRoundsSuccessMessage { get; private set; }
    public string? PendingRequestsErrorMessage { get; private set; }

    public event Action? OnStateChange;

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

    public async Task JoinPublicLeagueAsync(int leagueId)
    {
        AvailableLeaguesErrorMessage = null;

        NotifyStateChanged();

        var (success, errorMessage) = await leagueService.JoinPublicLeagueAsync(leagueId);
        if (success)
        {
            await Task.WhenAll(LoadMyLeaguesAsync(), LoadAvailableLeaguesAsync());
        }
        else
        {
            AvailableLeaguesErrorMessage = errorMessage;
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

    private void NotifyStateChanged() => OnStateChange?.Invoke();
}
