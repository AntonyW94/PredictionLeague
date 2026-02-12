using MediatR;
using PredictionLeague.Contracts.Leaderboards;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetMonthlyLeaderboardQuery(int LeagueId, int Month, string CurrentUserId) : IRequest<IEnumerable<LeaderboardEntryDto>>;