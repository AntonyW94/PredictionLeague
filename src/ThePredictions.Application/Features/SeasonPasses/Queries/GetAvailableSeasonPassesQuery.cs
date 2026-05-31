using MediatR;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

public record GetAvailableSeasonPassesQuery(string UserId) : IRequest<IEnumerable<AvailableSeasonPassDto>>;
