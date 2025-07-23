using MediatR;
using PredictionLeague.Contracts.Leaderboards;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetRoundLeaderboardQuery(int LeagueId, int RoundId) : IRequest<IEnumerable<LeaderboardEntryDto>>;