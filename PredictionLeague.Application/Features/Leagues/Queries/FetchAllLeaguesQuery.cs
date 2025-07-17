using MediatR;
using PredictionLeague.Contracts.Admin.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public class FetchAllLeaguesQuery : IRequest<IEnumerable<LeagueDto>>;