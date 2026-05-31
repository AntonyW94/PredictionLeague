using MediatR;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

public record GetMySeasonPassesQuery(string UserId) : IRequest<IEnumerable<MySeasonPassDto>>;
