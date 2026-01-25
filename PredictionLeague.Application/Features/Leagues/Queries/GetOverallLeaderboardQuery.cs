using MediatR;
using PredictionLeague.Contracts.Leaderboards;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetOverallLeaderboardQuery(int LeagueId, string CurrentUserId) : IRequest<IEnumerable<LeaderboardEntryDto>>;