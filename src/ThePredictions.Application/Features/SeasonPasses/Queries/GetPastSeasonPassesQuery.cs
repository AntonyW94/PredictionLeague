using MediatR;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

public record GetPastSeasonPassesQuery(string UserId) : IRequest<IEnumerable<PastSeasonPassDto>>;
