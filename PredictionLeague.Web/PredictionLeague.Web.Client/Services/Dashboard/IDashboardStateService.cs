using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Web.Client.Services.Dashboard;

public interface IDashboardStateService
{
    List<MyLeagueDto> MyLeagues { get; }
    List<AvailableLeagueDto> AvailableLeagues { get; }
    bool IsLoading { get; }
    string? ErrorMessage { get; }
    string? AvailableLeaguesErrorMessage { get; }

    event Action OnStateChange;

    Task InitializeAsync();
    Task JoinPublicLeagueAsync(int leagueId);
}