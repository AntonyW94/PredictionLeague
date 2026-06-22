using MediatR;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

public record GetStageLeaderboardQuery(int LeagueId, TournamentStageGroup Stage, string CurrentUserId) : IRequest<IEnumerable<LeaderboardEntryDto>>;
