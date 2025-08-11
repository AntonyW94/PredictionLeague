using MediatR;
using PredictionLeague.Contracts.Leaderboards;

namespace PredictionLeague.Application.Features.Leagues.Queries;

public record GetExactScoresLeaderboardQuery(int LeagueId) : IRequest<ExactScoresLeaderboardDto>;