using System.Diagnostics.CodeAnalysis;
using MediatR;
using ThePredictions.Contracts.Leaderboards;
using ThePredictions.Domain.Common.Enumerations;

namespace ThePredictions.Application.Features.Leagues.Queries;

[ExcludeFromCodeCoverage(Justification = "MediatR request record: properties only, no logic to test.")]
public record GetStageLeaderboardQuery(int LeagueId, TournamentStageGroup Stage, string CurrentUserId) : IRequest<IEnumerable<LeaderboardEntryDto>>;
