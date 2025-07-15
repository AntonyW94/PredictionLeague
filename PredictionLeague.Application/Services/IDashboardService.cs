
using PredictionLeague.Contracts.Dashboard;

namespace PredictionLeague.Application.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetDashboardDataAsync(string userId);
}