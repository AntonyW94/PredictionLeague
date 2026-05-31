using MediatR;
using ThePredictions.Contracts.SeasonPasses;

namespace ThePredictions.Application.Features.SeasonPasses.Queries;

public record GetSeasonTeamsQuery(int SeasonId) : IRequest<IEnumerable<SeasonTeamDto>>;
