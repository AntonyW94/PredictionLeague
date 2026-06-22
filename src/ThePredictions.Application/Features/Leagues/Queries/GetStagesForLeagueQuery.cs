using MediatR;
using ThePredictions.Contracts.Leagues;

namespace ThePredictions.Application.Features.Leagues.Queries;

public record GetStagesForLeagueQuery(int LeagueId, string CurrentUserId) : IRequest<IEnumerable<StageDto>>;
