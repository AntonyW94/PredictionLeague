using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public record GetAvailableLeaguesQuery(string UserId) : IRequest<IEnumerable<AvailableLeagueDto>>;