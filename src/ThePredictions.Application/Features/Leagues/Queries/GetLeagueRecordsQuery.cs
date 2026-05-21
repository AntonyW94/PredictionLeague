using MediatR;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

public record GetLeagueRecordsQuery(int LeagueId, string UserId) : IRequest<LeagueRecordsDto?>;
