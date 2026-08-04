using MediatR;
using ThePredictions.Contracts.Admin.Seasons;

namespace ThePredictions.Application.Features.Admin.Seasons.Queries;

public record GetSeasonPassHoldersQuery(int SeasonId) : IRequest<IEnumerable<SeasonPassHolderDto>>;
