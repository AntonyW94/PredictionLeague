using MediatR;
using ThePredictions.Contracts.Admin.RunningCosts;

namespace ThePredictions.Application.Features.Admin.RunningCosts.Queries;

public record GetRunningCostsQuery : IRequest<IEnumerable<RunningCostDto>>;
