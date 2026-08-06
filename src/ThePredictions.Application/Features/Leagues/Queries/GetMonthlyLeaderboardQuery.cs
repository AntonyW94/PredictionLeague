using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Leaderboards;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetMonthlyLeaderboardQuery(int LeagueId, int Month, string CurrentUserId) : IRequest<IEnumerable<LeaderboardEntryDto>>;
