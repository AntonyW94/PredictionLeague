using MediatR;
using PredictionLeague.Contracts.Dashboard;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public record GetPendingRequestsQuery(string UserId) : IRequest<IEnumerable<LeagueRequestDto>>;