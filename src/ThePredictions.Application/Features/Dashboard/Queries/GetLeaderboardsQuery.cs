using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Leaderboards;

namespace ThePredictions.Application.Features.Dashboard.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetLeaderboardsQuery(string UserId) : IRequest<IEnumerable<LeagueLeaderboardDto>>;
