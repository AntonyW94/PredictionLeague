using MediatR;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

public record GetSeasonRecapQuery(int LeagueId, string UserId) : IRequest<SeasonRecapDto?>;
