using MediatR;
using PredictionLeague.Contracts.Dashboard;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public record GetUpcomingRoundsQuery(string UserId) : IRequest<IEnumerable<UpcomingRoundDto>>;