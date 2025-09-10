using PredictionLeague.Contracts.Dashboard;
using PredictionLeague.Contracts.Leaderboards;
using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Web.Client.Services.Leagues;
using PredictionLeague.Web.Client.Services.Round;

namespace PredictionLeague.Web.Client.Services.Dashboard;

public class DashboardStateService : IDashboardStateService
{
    public List<MyLeagueDto> MyLeagues { get; private set; } = new();
    public List<AvailableLeagueDto> AvailableLeagues { get; private set; } = new();
    public List<LeagueLeaderboardDto> Leaderboards { get; private set; } = new();
    public List<UpcomingRoundDto> UpcomingRounds { get; private set; } = new();

    public bool IsMyLeaguesLoading { get; private set; }
    public bool IsAvailableLeaguesLoading { get; private set; }
    public bool IsLeaderboardsLoading { get; private set; }
    public bool IsUpcomingRoundsLoading { get; private set; }

    public int? ChasingRoundId { get; private set; }

    public string? AvailableLeaguesErrorMessage { get; private set; }
    public string? MyLeaguesErrorMessage { get; private set; }
    public string? LeaderboardsErrorMessage { get; private set; }
    public string? UpcomingRoundsErrorMessage { get; private set; }
    public string? UpcomingRoundsSuccessMessage { get; private set; }


    public event Action? OnStateChange;

    private readonly ILeagueService _leagueService;
    private readonly IRoundService _roundService;


    public DashboardStateService(ILeagueService leagueService, IRoundService roundService)
    {
        _leagueService = leagueService;
        _roundService = roundService;
    }

    public async Task LoadMyLeaguesAsync()
    {
        IsMyLeaguesLoading = true;
        MyLeaguesErrorMessage = null;

        NotifyStateChanged();

        try
        {
            MyLeagues = await _leagueService.GetMyLeaguesAsync();
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
            AvailableLeagues = await _leagueService.GetAvailableLeaguesAsync();
        }
        catch
        {
            AvailableLeaguesErrorMessage = "Could not load available leagues.";
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
            Leaderboards = await _leagueService.GetLeaderboardsAsync();
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

    public async Task LoadUpcomingRoundsAsync()
    {
        IsUpcomingRoundsLoading = true;
        UpcomingRoundsErrorMessage = null;
        UpcomingRoundsSuccessMessage = null;

        NotifyStateChanged();

        try
        {
            UpcomingRounds = await _leagueService.GetUpcomingRoundsAsync();
        }
        catch
        {
            UpcomingRoundsErrorMessage = "Could not load upcoming rounds";
        }
        finally
        {
            IsUpcomingRoundsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task JoinPublicLeagueAsync(int leagueId)
    {
        AvailableLeaguesErrorMessage = null;

        NotifyStateChanged();

        var (success, errorMessage) = await _leagueService.JoinPublicLeagueAsync(leagueId);
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

    public async Task RemoveRejectedLeagueAsync(int leagueId)
    {
        MyLeaguesErrorMessage = null;

        NotifyStateChanged();

        var (success, errorMessage) = await _leagueService.RemoveMyLeagueMembershipAsync(leagueId);
        if (success)
        {
            await Task.WhenAll(LoadMyLeaguesAsync(), LoadAvailableLeaguesAsync());
        }
        else
        {
            MyLeaguesErrorMessage = errorMessage;
            NotifyStateChanged();
        }
    }

    public async Task SendChaseEmailsAsync(int roundId)
    {
        ChasingRoundId = roundId;

        UpcomingRoundsErrorMessage = null;

        NotifyStateChanged();

        var success = await _roundService.SendChaseEmailsAsync(roundId);
        if (success)
        {
            UpcomingRoundsSuccessMessage = "Chase emails sent successfully";
        }
        else
        {
            UpcomingRoundsErrorMessage = "Could not send chase emails";
        }

        ChasingRoundId = null;

        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnStateChange?.Invoke();
}