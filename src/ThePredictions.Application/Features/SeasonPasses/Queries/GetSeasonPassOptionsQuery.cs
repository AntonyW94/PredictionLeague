using MediatR;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

public record GetSeasonPassOptionsQuery(string UserId, int SeasonId) : IRequest<SeasonPassOptionsDto?>;
