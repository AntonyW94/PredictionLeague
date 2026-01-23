using MediatR;
using PredictionLeague.Contracts.Dashboard;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public record GetActiveRoundsQuery(string UserId) : IRequest<IEnumerable<ActiveRoundDto>>;
