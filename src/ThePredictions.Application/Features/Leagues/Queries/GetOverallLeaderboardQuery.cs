using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Leaderboards;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetOverallLeaderboardQuery(int LeagueId, string CurrentUserId) : IRequest<IEnumerable<LeaderboardEntryDto>>;
