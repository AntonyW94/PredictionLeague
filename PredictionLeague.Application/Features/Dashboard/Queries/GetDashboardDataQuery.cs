using MediatR;
using PredictionLeague.Contracts.Dashboard;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public class GetDashboardDataQuery : IRequest<DashboardDto>
{
    public string UserId { get; }

    public GetDashboardDataQuery(string userId)
    {
        UserId = userId;
    }
}