using MediatR;
using PredictionLeague.Contracts.Leagues;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetCreateLeaguePageDataQuery : IRequest<CreateLeaguePageData>;