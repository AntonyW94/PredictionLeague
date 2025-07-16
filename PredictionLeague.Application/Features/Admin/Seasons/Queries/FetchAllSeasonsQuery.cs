using MediatR;
using PredictionLeague.Contracts.Admin.Seasons;

namespace PredictionLeague.Application.Features.Admin.Seasons.Queries;

public class FetchAllSeasonsQuery : IRequest<IEnumerable<SeasonDto>>;