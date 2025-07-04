using PredictionLeague.Shared.Dashboard;

namespace PredictionLeague.Core.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardDataAsync(string userId);
}