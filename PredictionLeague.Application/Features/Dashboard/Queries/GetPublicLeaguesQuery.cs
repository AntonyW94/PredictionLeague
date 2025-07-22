using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public record GetPublicLeaguesQuery(string UserId) : IRequest<IEnumerable<PublicLeagueDto>>;