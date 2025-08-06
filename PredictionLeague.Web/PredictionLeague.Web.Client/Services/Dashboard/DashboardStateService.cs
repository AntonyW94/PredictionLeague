using PredictionLeague.Contracts.Leagues;
using PredictionLeague.Web.Client.Services.Leagues;

namespace PredictionLeague.Web.Client.Services.Dashboard;

public class DashboardStateService : IDashboardStateService
{
    public List<MyLeagueDto> MyLeagues { get; private set; } = new();
    public List<AvailableLeagueDto> AvailableLeagues { get; private set; } = new();

    public bool IsLoading { get; private set; }

    public string? ErrorMessage { get; private set; }
    public string? AvailableLeaguesErrorMessage { get; private set; }
    public string? MyLeaguesErrorMessage { get; private set; }


    public event Action? OnStateChange;

    private readonly ILeagueService _leagueService;

    public DashboardStateService(ILeagueService leagueService)
    {
        _leagueService = leagueService;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            var myLeaguesTask = _leagueService.GetMyLeaguesAsync();
            var availableLeaguesTask = _leagueService.GetAvailableLeaguesAsync();

            await Task.WhenAll(myLeaguesTask, availableLeaguesTask);

            MyLeagues = await myLeaguesTask;
            AvailableLeagues = await availableLeaguesTask;
        }
        catch (Exception)
        {
            ErrorMessage = "Could not load dashboard data. Please try again later.";
        }
        finally
        {
            IsLoading = false;
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
            await InitializeAsync();
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
            await InitializeAsync();
        }
        else
        {
            MyLeaguesErrorMessage = errorMessage;
            NotifyStateChanged();
        }
    }

    private void NotifyStateChanged() => OnStateChange?.Invoke();
}