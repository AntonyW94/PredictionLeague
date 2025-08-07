using MediatR;
using PredictionLeague.Contracts.Leaderboards;

namespace PredictionLeague.Application.Features.Dashboard.Queries;

public record GetLeaderboardsQuery(string UserId) : IRequest<IEnumerable<LeagueLeaderboardDto>>;