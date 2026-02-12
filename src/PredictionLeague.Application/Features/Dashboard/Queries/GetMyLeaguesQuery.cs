using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public record GetMyLeaguesQuery(string UserId) : IRequest<IEnumerable<MyLeagueDto>>;